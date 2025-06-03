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
			//var (repoDbo, repoCim, _) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			// 使用某個特定的資料庫
			var repoDbo = repositories["DboEmap"];
			var repoCim = repositories["CsCimEmap"];
			var result = new List<TileCheckResultDto>();

			//解析查詢規則(S1~S4)			
			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.DeviceId);


			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				@"SELECT OPNO, DEVICEIDS, EVALFORMULA AS EvalFormula, REASON, PRIORITY, DAYSRANGE, ENABLEMISSINGWORK, ENABLEMIXLOT, ENABLENG
				  FROM ARGOAPILOTTILERULECHECK 
				  WHERE OPNO IN :opnos 
				  ORDER BY PRIORITY",
				new { opnos = opnosToQuery })).ToList();

			if (!rules.Any())
				return ApiReturn<object>.Warning("無對應規則", result);

			// 🔥 多台 deviceids 查出各自的 process
			var deviceProcessMap = await DeviceProcessHelper.GetProcessByDeviceIdsAsync(repoDbo, deviceIdsToQuery);

			// 🔥 GroupBy process
			var processGroups = deviceProcessMap.GroupBy(x => x.Value)
				.ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

			// 2. 推算最大天數範圍
			var maxDays = rules.Max(r => r.DaysRange ?? 90);

			// ✅ 若有雷射規則，優先處理
			var laserOnly = rules.All(r => r.EvalFormula.Contains("LaserCheck"));
			if (laserOnly)
			{
				var rule = rules.First();
				var deviceIds = rule.DeviceIds?.Split(',').Select(d => d.Trim()).ToArray() ?? Array.Empty<string>();

				var laserResults = await LaserInkAsync(repoDbo, request.LotNo, deviceIds, maxDays);

				// 加上 Opno
				foreach (var row in laserResults)
				{
					row.OpNo = request.Opno;
				}
				await LotTileIdListHelper.UpsertLotTileIdListAsync(repoCim, laserResults, "MES");


				return ApiReturn<object>.Success("完成雷射站比對", laserResults
					.Select(x => new TileCheckLaserInkDto
					{
						TileId = x.TileId,
						LotNo = x.LotNo,
						ResultList = x.ResultList,
						Reason = x.Reason,
						RecordDate = x.RecordDate				

					}).ToList());
			}

			// 3. 撈 WIP 資料 (UNION ALL 多表)
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

				parameters.Add(paramName, devs.ToArray());   // 保證用陣列

				string inClause = $":{paramName}"; // ← 這是重點，要把 :deviceids0 展開變成文字

				if (idx > 0) unionSql.AppendLine("UNION ALL");

				unionSql.AppendLine($@"
				SELECT * FROM (
				SELECT TILEID, LOTNO, STEP, DEVICEID, RECORDDATE,
					   V001, V002, V003, V004, V005, V006, V007, V008,
					   V010, V011, V014, V015, V036, V037,
					   ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY RECORDDATE DESC) AS RN
				FROM TBLMESWIPDATA_{process}
				WHERE LOTNO = :lotno
				  AND STEP IN :opnos
				  AND DEVICEID IN {inClause}
				  AND TILEID IS NOT NULL
				  AND RECORDDATE >= TRUNC(SYSDATE) - :days
				) WHERE RN = 1");

				idx++;
			}
			//// 🔍 印出組好的 SQL 與參數值
			//Console.WriteLine("===== Final SQL Statement =====");
			//Console.WriteLine(unionSql.ToString());

			//Console.WriteLine("===== Parameters =====");
			//foreach (var name in parameters.ParameterNames)
			//{
			//	var val = parameters.Get<object>(name);

			//	if (val is IEnumerable<string> list && !(val is string))
			//	{
			//		Console.WriteLine($"  {name} = ({string.Join(", ", list.Select(x => $"'{x}'"))})");
			//	}
			//	else
			//	{
			//		Console.WriteLine($"  {name} = {val}");
			//	}
			//}

			var records = (await repoDbo.QueryAsync<TblMesWipData_Record>(unionSql.ToString(), parameters)).ToList();

			// 4. 建立雷射蓋印清單
			var laserTiles = (await repoCim.QueryAsync<ARGOCIMLOTTILEIDLIST>(
				"SELECT TILEID FROM ARGOCIMLOTTILEIDLIST WHERE LOTNO = :lotno",
				new { lotno = request.LotNo })).Select(x => x.TileId).ToHashSet();

			var producedTileSet = records.Select(x => x.TileId).ToHashSet();
			var allowMissingWork = rules.Any(r => r.EnableMissingWork == "Y");
			if (request.DisableMissingWork == 1)
			{
				allowMissingWork = false;
			}

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

			// 5. 檢查每筆資料
			var allowMixLot = rules.Any(r => r.EnableMixLot == "Y");

			foreach (var record in records)
			{
				var matchRule = rules.FirstOrDefault(r => DeviceMatch(r.DeviceIds, record.DeviceId));
				var context = ToEvalContext(record);

				if (matchRule != null && matchRule.EvalFormula.Contains("LaserCheck"))
				{
					context["LaserCheck"] = true;
					continue;
				}
				else
				{
					if (matchRule != null && matchRule.EnableNg == "Y" && EvalHelper.Evaluate(matchRule.EvalFormula, context))
					{
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

			var converted = LotTileIdListHelper.ConvertToLaserInkFormat(result);
			// 加上 Opno
			foreach (var row in converted)
			{
				row.OpNo = request.Opno;
			}
			await LotTileIdListHelper.UpsertLotTileIdListAsync(repoCim, converted, "MES");

			return ApiReturn<object>.Success("完成比對", result);
		}

		private static bool DeviceMatch(string ruleDeviceIds, string actualDeviceId)
		{
			if (string.IsNullOrWhiteSpace(ruleDeviceIds) || string.IsNullOrWhiteSpace(actualDeviceId))
				return false;

			if (ruleDeviceIds.Trim() == "*")
				return true;

			return ruleDeviceIds.Split(',')
				.Select(d => d.Trim())
				.Any(d => string.Equals(d, actualDeviceId, StringComparison.OrdinalIgnoreCase));
		}

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
						FROM DBO.TBLMES2DREAD_D
						WHERE CS_TILEID IS NOT NULL
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
							d.TH_TILEID, d.TH_TILEID_2,
							d.PANEL_ID1, d.PANEL_ID2, d.PANEL_ID3, d.PANEL_ID4,
							ROW_NUMBER() OVER (PARTITION BY t.TileId ORDER BY m.CREATEDATE DESC) AS row_num,
							DECODE(t.TileId, NULL, 0, 1) AS chk1
						FROM cs_tile_union t
						JOIN DBO.TBLMES2DREAD_M m ON t.SN = m.SN
						JOIN DBO.TBLMES2DREAD_D d ON t.SN = d.SN
						WHERE m.LOTNO = :lotNo
						  AND m.DEVICEID IN ({deviceIdSql})
						  AND m.CREATEDATE > (TRUNC(SYSDATE) - :daysRange)
					),
					zz AS (
						SELECT TILEID, LOTNO, TILEGROUP
						FROM DBO.TBLWIPLOTMARKINGDATA
						WHERE LOTNO = :lotNo
						  AND ORACLEDATE > (TRUNC(SYSDATE) - :daysRange)
					)
					SELECT
						j.TileId,
						j.SourceCol,
						j.LotNo,
						j.RecordDate,
						j.TH_TILEID, j.TH_TILEID_2,
						j.PANEL_ID1, j.PANEL_ID2, j.PANEL_ID3, j.PANEL_ID4,
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
					LEFT JOIN zz ON j.TileId = zz.TILEID AND j.LotNo = zz.LOTNO
					WHERE j.row_num = 1 AND j.TileId IS NOT NULL
					ORDER BY j.TileId";

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
