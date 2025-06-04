using Core.Entities.DboEmap;
using Core.Entities.LotTileCheck;
using Core.Entities.Public;
using Core.Interfaces;
using Dapper;
using Infrastructure.Utilities;
using Mysqlx.Crud;
using System.Collections.Concurrent;
using System.Text;
using Infrastructure.Helpers;
using System.Data;
using Core.Entities.ArgoCim;

namespace Infrastructure.Services
{
	public class LotTileCheckService : ILotTileCheckService
	{
		private readonly IRepositoryFactory _repositoryFactory;

		public LotTileCheckService(IRepositoryFactory repositoryFactory)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<ApiReturn<object>> CheckLotTileAsync(LotTileCheckRequest request)
		{
			// 建立資料庫連線
			var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			var repoDbo = repositories["DboEmap"];
			var repoCim = repositories["CsCimEmap"];
			var result = new List<TileCheckResultDto>();

			// 根據 OPNO + DeviceId 解析出應查詢的步驟與設備清單（支援單/多站多機）
			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.DeviceId);

			// 撈取對應規則（可包含多條件）
			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				"SELECT OPNO, DEVICEIDS, EVALFORMULA AS EvalFormula, REASON, PRIORITY, DAYSRANGE, ENABLEMISSINGWORK, ENABLEMIXLOT, ENABLENG, ENABLEGROUPSYNC\nFROM ARGOAPILOTTILERULECHECK \nWHERE OPNO IN :opnos \nORDER BY PRIORITY",
				new { opnos = opnosToQuery })).ToList();

			if (!rules.Any())
				return ApiReturn<object>.Warning("無對應規則", result);

			// 依照設備取得對應的製程（PD、AOI、INK 等），以查不同 WIP 資料表
			var deviceProcessMap = await DeviceProcessHelper.GetProcessByDeviceIdsAsync(repoDbo, deviceIdsToQuery);
			var processGroups = deviceProcessMap.GroupBy(x => x.Value).ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

			// 所有規則中的最大天數限制
			var maxDays = rules.Max(r => r.DaysRange ?? 90);

			// ✅ 若為雷射站（所有規則皆含 LaserCheck），走專屬處理流程
			var laserOnly = rules.All(r => r.EvalFormula.Contains("LaserCheck"));
			if (laserOnly)
			{
				var rule = rules.First();
				var deviceIds = rule.DeviceIds?.Split(',').Select(d => d.Trim()).ToArray() ?? Array.Empty<string>();

				// 執行雷射邏輯 SQL 查詢
				var laserResults = await LaserInkAsync(repoDbo, request.LotNo, deviceIds, maxDays);

				// 加上 OpNo 欄位
				foreach (var row in laserResults) row.OpNo = request.Opno;

				// 寫入結果表 ARGOCIMLOTTILEIDLIST
				await LotTileIdListHelper.UpsertLotTileIdListAsync(repoCim, laserResults, "MES");

				// 回傳標準格式
				return ApiReturn<object>.Success("完成雷射站比對", laserResults.Select(x => new TileCheckLaserInkDto
				{
					TileId = x.TileId,
					TileGroup = x.TileGroup,
					LotNo = x.LotNo,
					ResultList = x.ResultList,
					Reason = x.Reason,
					RecordDate = x.RecordDate
				}).ToList());
			}

			// 🔍 查詢多個 process 對應的 WIP 資料表，組成 unionAll 查詢
			var unionSql = new StringBuilder();
			var parameters = new DynamicParameters();
			parameters.Add("lotno", request.LotNo);
			parameters.Add("opnos", opnosToQuery);
			parameters.Add("days", maxDays);

			int idx = 0;
			foreach (var group in processGroups)
			{
				string process = group.Key;
				var devs = group.Value;
				string paramName = $"deviceids{idx}";
				parameters.Add(paramName, devs.ToArray());
				string inClause = $":{paramName}";
				if (idx > 0) unionSql.AppendLine("UNION ALL");

				// 查詢該 process WIP 表，並保留同 TileId 最新一筆紀錄
				unionSql.AppendLine($@"
				SELECT * FROM (
				SELECT TILEID, LOTNO, STEP, DEVICEID, RECORDDATE,
					   V001, V002, V003, V004, V005, V006, V007, V008,
					   V010, V011, V014, V015, V036, V037,
					   ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY RECORDDATE DESC) AS RN
				FROM TBLMESWIPDATA_{process}
				WHERE LOTNO = :lotno AND STEP IN :opnos AND DEVICEID IN {inClause}
				  AND TILEID IS NOT NULL AND RECORDDATE >= TRUNC(SYSDATE) - :days
				) WHERE RN = 1");
				idx++;
			}

			// 執行 SQL 查詢出所有站點 Tile 生產資料
			var records = (await repoDbo.QueryAsync<TblMesWipData_Record>(unionSql.ToString(), parameters)).ToList();


			// 查詢先前已寫入 ARGOCIMLOTTILEIDLIST 的 TileId + 狀態 + 原因（避免覆蓋）
			var existingMap = (await repoCim.QueryAsync<ARGOCIMLOTTILEIDLIST>(
				"SELECT TILEID, RESULTLIST, REASON FROM ARGOCIMLOTTILEIDLIST WHERE LOTNO = :lotno",
				new { lotno = request.LotNo })).ToDictionary(x => x.TileId, x => x);

			// 先記錄已為黑名單的 TileId 對應的紀錄（只顯示用，不更新）
			var existingBlackTiles = existingMap
				.Where(x => x.Value.ResultList == "Black")
				.Select(x => x.Key)
				.ToHashSet();

			var skippedRecords = records
				.Where(r => existingBlackTiles.Contains(r.TileId))
				.Select(r => new TileCheckResultDto
				{
					TileId = r.TileId,
					LotNo = r.LotNo,
					ResultList = "Black",
					Reason = existingMap[r.TileId].Reason,
					RecordDate = r.RecordDate
				})
				.ToList();

			// 移除這些已是 Black 的 TileId，不再進入後續邏輯
			records = records
				.Where(r => !existingBlackTiles.Contains(r.TileId))
				.ToList();


			var laserTiles = existingMap.Keys.ToHashSet(); // 雷射標記的 TileId（預設白名單）
			var producedTileSet = records.Select(x => x.TileId).ToHashSet(); // 生產出來的 TileId

			// 判斷是否允許 MissingWork（有雷射但沒生產）
			var allowMissingWork = rules.Any(r => r.EnableMissingWork == "Y");
			if (request.DisableMissingWork == 1) allowMissingWork = false;

			if (allowMissingWork)
			{
				var missingTiles = laserTiles.Except(producedTileSet);
				foreach (var tileId in missingTiles)
				{
					result.Add(new TileCheckResultDto
					{
						TileId = tileId,
						LotNo = request.LotNo,
						ResultList = "Black",
						Reason = "MissingWork",
						RecordDate = null
					});
				}
			}

			// 判斷 NG / MixLot / WhiteList
			var allowMixLot = rules.Any(r => r.EnableMixLot == "Y");
			foreach (var record in records)
			{
				var matchRule = rules.FirstOrDefault(r => DeviceMatch(r.DeviceIds, record.DeviceId));
				var context = ToEvalContext(record);

				if (matchRule != null && matchRule.EvalFormula.Contains("LaserCheck"))
				{
					context["LaserCheck"] = true; // 略過此筆（已於雷射站處理）
					continue;
				}
				else
				{
					if (matchRule != null && matchRule.EnableNg == "Y" && EvalHelper.Evaluate(matchRule.EvalFormula, context))
					{
						// 命中 NG 規則
						result.Add(new TileCheckResultDto
						{
							TileId = record.TileId,
							LotNo = record.LotNo,
							ResultList = "Black",
							Reason = matchRule.Reason,
							RecordDate = record.RecordDate
						});
					}
					else if (allowMixLot && !laserTiles.Contains(record.TileId))
					{
						// 多出來的 TileId 不屬於雷射源頭 → MixLot
						result.Add(new TileCheckResultDto
						{
							TileId = record.TileId,
							LotNo = record.LotNo,
							ResultList = "Black",
							Reason = "MixLot",
							RecordDate = record.RecordDate
						});
					}
					else
					{
						// 一般白名單結果
						result.Add(new TileCheckResultDto
						{
							TileId = record.TileId,
							LotNo = record.LotNo,
							ResultList = "White",
							Reason = "",
							RecordDate = record.RecordDate
						});
					}
				}
			}


			// 轉格式 → 過濾 skipSet → 補 OpNo
			var converted = LotTileIdListHelper.ConvertToLaserInkFormat(result);
			var skipSet = skippedRecords.Select(x => x.TileId).ToHashSet();
			converted = converted.Where(x => !skipSet.Contains(x.TileId)).ToList();
			foreach (var r in converted) r.OpNo = request.Opno;

			// 如啟用群組擴散，補上 TileGroup → 不再查 TBLWIPLOTMARKINGDATA，而是用 existingMap 補
			bool enableGroupSync = rules.Any(r => r.EnableGroupSync == "Y");
			if (enableGroupSync)
			{
				foreach (var row in converted)
				{
					if (string.IsNullOrWhiteSpace(row.TileGroup)
						&& existingMap.TryGetValue(row.TileId, out var old)
						&& !string.IsNullOrWhiteSpace(old.TileGroup))
					{
						row.TileGroup = old.TileGroup;
					}
				}
			}

			// 寫入 & 群組擴散
			await LotTileIdListHelper.UpsertLotTileIdListAsync(
				repoCim,
				converted,
				creator: "MES",
				enableGroupSync);

			// 回傳
			return ApiReturn<object>.Success("完成比對", result);

		}
		// 比對設備是否符合規則中列出的 DeviceId
		private static bool DeviceMatch(string ruleDeviceIds, string actualDeviceId)
		{
			if (string.IsNullOrWhiteSpace(ruleDeviceIds) || string.IsNullOrWhiteSpace(actualDeviceId))
				return false;
			if (ruleDeviceIds.Trim() == "*") return true;
			return ruleDeviceIds.Split(',').Select(d => d.Trim()).Any(d => string.Equals(d, actualDeviceId, StringComparison.OrdinalIgnoreCase));
		}

		// 將 WIP 紀錄轉成 EvalContext（供公式比對使用）
		private static Dictionary<string, object> ToEvalContext(TblMesWipData_Record record)
		{
			var ctx = new Dictionary<string, object>
			{
				["V001"] = record.V001,
				["V002"] = record.V002,
				["V003"] = record.V003,
				["V004"] = record.V004,
				["V005"] = record.V005,
				["V006"] = record.V006,
				["V007"] = record.V007,
				["V008"] = record.V008,
				["V010"] = record.V010,
				["V011"] = record.V011,
				["V014"] = record.V014,
				["V015"] = record.V015,
				["V036"] = record.V036,
				["V037"] = record.V037
			};
			return ctx;
		}

		private async Task<List<TileCheckLaserInkDto>> LaserInkAsync(IRepository repo, string lotNo, string[] deviceIds, int daysRange)
		{
			var deviceIdSql = string.Join(",", deviceIds.Select(d => $"'{d}'"));

			var sql = $@"
					WITH cs_tile_union AS (
						SELECT SN, 'CS_TILEID' AS SourceCol, CS_TILEID AS TileId
						FROM DBO.TBLMES2DREAD_D WHERE CS_TILEID IS NOT NULL
						UNION ALL
						SELECT SN, 'CS_TILEID2', CS_TILEID2 FROM DBO.TBLMES2DREAD_D WHERE CS_TILEID2 IS NOT NULL
						UNION ALL
						SELECT SN, 'CS_TILEID3', CS_TILEID3 FROM DBO.TBLMES2DREAD_D WHERE CS_TILEID3 IS NOT NULL
						UNION ALL
						SELECT SN, 'CS_TILEID4', CS_TILEID4 FROM DBO.TBLMES2DREAD_D WHERE CS_TILEID4 IS NOT NULL
					),
					joined AS (
						SELECT
							t.TileId,
							t.SourceCol,
							m.LOTNO,
							m.DEVICEID,
							m.CREATEDATE AS RecordDate,
							d.TH_TILEID,
							d.TH_TILEID_2,
							d.PANEL_ID1, d.PANEL_ID2, d.PANEL_ID3, d.PANEL_ID4,
							ROW_NUMBER() OVER (PARTITION BY t.TileId ORDER BY m.CREATEDATE DESC) AS row_num,
							DECODE(t.TileId, NULL, 0, 1) AS chk1
						FROM cs_tile_union t
						JOIN DBO.TBLMES2DREAD_M m ON t.SN = m.SN
						JOIN DBO.TBLMES2DREAD_D d ON t.SN = d.SN
						WHERE m.LOTNO = :lotno
						  AND m.DEVICEID IN ({deviceIdSql})
						  AND m.CREATEDATE > (TRUNC(SYSDATE) - :daysRange)
					),
					zz AS (
						SELECT TILEID, LOTNO, TILEGROUP
						FROM DBO.TBLWIPLOTMARKINGDATA
						WHERE LOTNO = :lotno
						  AND ORACLEDATE > (TRUNC(SYSDATE) - :daysRange)
					)
					SELECT
						j.TileId,
						j.LotNo,
						j.RecordDate,
						j.TH_TILEID,
						j.TH_TILEID_2,
						j.SourceCol,
						z.TILEGROUP,
						CASE
							WHEN NVL(j.PANEL_ID1,'*') || NVL(j.PANEL_ID2,'*') || NVL(j.PANEL_ID3,'*') || NVL(j.PANEL_ID4,'*') <> '****'
							 AND (j.PANEL_ID1 IS NULL OR j.PANEL_ID2 IS NULL OR j.PANEL_ID3 IS NULL OR j.PANEL_ID4 IS NULL)
							THEN 'Black'
							WHEN j.chk1 <> 1
							THEN 'Black'
							ELSE 'White'
						END AS ResultList,
						CASE
							WHEN NVL(j.PANEL_ID1,'*') || NVL(j.PANEL_ID2,'*') || NVL(j.PANEL_ID3,'*') || NVL(j.PANEL_ID4,'*') <> '****'
							 AND (j.PANEL_ID1 IS NULL OR j.PANEL_ID2 IS NULL OR j.PANEL_ID3 IS NULL OR j.PANEL_ID4 IS NULL)
							THEN 'NG'
							WHEN j.chk1 <> 1
							THEN 'NG'
							ELSE 'PASS'
						END AS Reason
					FROM joined j
					LEFT JOIN zz z ON j.TileId = z.TILEID AND j.LotNo = z.LOTNO
					WHERE j.row_num = 1 AND j.TileId IS NOT NULL
					ORDER BY j.TileId
					";

			var displaySql = sql
				.Replace(":lotNo", $"'{lotNo}'")
				.Replace(":daysRange", $"{daysRange}");

			Console.WriteLine("🟢 Final SQL:");
			Console.WriteLine(displaySql);

			var data = await repo.QueryAsync<TileCheckLaserInkDto>(sql, new { lotNo, daysRange });
			return data.ToList();
		}



	}
}
