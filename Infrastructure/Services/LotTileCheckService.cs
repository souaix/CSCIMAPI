using Core.Entities.DboEmap;
using Core.Entities.LotTileCheck;
using Core.Entities.Public;
using Core.Interfaces;
using Infrastructure.Utilities;

namespace Infrastructure.Services
{
	public class LotTileCheckService : ILotTileCheckService
	{
		private readonly IRepositoryFactory _repositoryFactory;

		public LotTileCheckService(IRepositoryFactory repositoryFactory)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<ApiReturn<List<TileCheckResultDto>>> CheckLotTileAsync(LotTileCheckRequest request)
		{
			var (repoDbo, repoCim) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);


			var result = new List<TileCheckResultDto>();
			
			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repoDbo, request.DeviceIds[0]);
			string tableName = $"TBLMESWIPDATA_{process}";

			// 1. 查詢規則表
			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				@"SELECT STEP, DEVICEIDS, EVALFORMULA AS EvalFormula, REASON, DAYSRANGE, PRIORITY
				  FROM ARGOAPILOTTILERULECHECK
				  WHERE STEP IN :steps
					AND ISENABLED = 'Y'
				  ORDER BY PRIORITY",
				new { steps = request.Steps })).ToList();


			if (!rules.Any())
				return ApiReturn<List<TileCheckResultDto>>.Warning("無對應規則", result);

			// ✅ 若有雷射規則，優先處理
			var laserOnly = rules.All(r => r.EvalFormula.Contains("LaserCheck"));
			if (laserOnly)
			{
				var rule = rules.First();
				var deviceIds = rule.DeviceIds?.Split(',').Select(d => d.Trim()).ToArray() ?? Array.Empty<string>();
				var days = rule.DaysRange ?? 120;
				var laserResults = await LaserInkAsync(repoDbo, request.LotNo, deviceIds, days);
				return ApiReturn<List<TileCheckResultDto>>.Success("完成雷射站比對", laserResults
					.Select(x => new TileCheckResultDto
					{
						TileId = x.TileId,
						LotNo = x.LotNo,
						ResultList = x.ResultList,
						Reason = x.Reason,
						RecordDate = x.RecordDate
					}).ToList());
			}


			// 2. 推算最大天數範圍（多條規則可能不一樣，取最大）
			var maxDays = rules.Max(r => r.DaysRange ?? 90);

			// 3. 查詢最新設備資料
			var records = (await repoDbo.QueryAsync<TblMesWipData_Record>(
				@$"SELECT *
				   FROM (
					   SELECT TILEID, LOTNO, STEP, DEVICEID, RECORDDATE,
							  V001, V002, V003, V004, V005, V006, V007, V008,
							  V010, V011, V014, V015, V036, V037,
							  ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY RECORDDATE DESC) AS RN
					   FROM {tableName}
					   WHERE LOTNO = :lotno
						 AND STEP IN :steps
						 AND TILEID IS NOT NULL
						 AND RECORDDATE >= TRUNC(SYSDATE) - :days
				   ) T
				   WHERE RN = 1",
				new { lotno = request.LotNo, steps = request.Steps, days = maxDays }
			)).ToList();




			// 4. 建立雷射蓋印清單（供 MissingWork / MixLot 判斷）
			var laserTiles = (await repoDbo.QueryAsync<TBLWIPLOTMARKINGDATA>(
				@"SELECT TILEID FROM TBLWIPLOTMARKINGDATA WHERE LOTNO = :lotno",
				new { lotno = request.LotNo })).Select(x => x.TileId).ToHashSet();

			var producedTileSet = records.Select(x => x.TileId).ToHashSet();
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

			// 5. 檢查每筆設備資料
			foreach (var record in records)
			{
				var matchRule = rules.FirstOrDefault(r => DeviceMatch(r.DeviceIds, record.DeviceId));
				var context = ToEvalContext(record);

				// ✅ 根據規則表是否需要 LaserCheck 決定是否加上
				if (matchRule != null && matchRule.EvalFormula.Contains("LaserCheck"))
				{
					context["LaserCheck"] = true;
					// 👉 呼叫專門的雷射站處理邏輯
					continue;
				}
				else
				{
					if (matchRule != null && EvalHelper.Evaluate(matchRule.EvalFormula, context))
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
					else if (!laserTiles.Contains(record.TileId)) // mix lot
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

			return ApiReturn<List<TileCheckResultDto>>.Success("完成比對", result);
		}

		private static bool DeviceMatch(string ruleDeviceIds, string actualDeviceId)
		{
			if (ruleDeviceIds == "*") return true;
			return ruleDeviceIds.Split(',').Any(d => d.Trim() == actualDeviceId);
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
				WITH unpivot_d AS (
					SELECT
						SN,
						PANEL_ID1, PANEL_ID2, PANEL_ID3, PANEL_ID4,
						tilevalue
					FROM (
						SELECT * FROM DBO.TBLMES2DREAD_D
					) d
					UNPIVOT(tilevalue FOR tile IN (
						CS_TILEID, CS_TILEID2, CS_TILEID3, CS_TILEID4
					))
				),
				joined AS (
					SELECT
						m.CREATEDATE AS RecordDate,
						m.LOTNO AS LotNo,
						m.DEVICEID,
						d.tilevalue AS TileId,
						d.PANEL_ID1, d.PANEL_ID2, d.PANEL_ID3, d.PANEL_ID4,
						ROW_NUMBER() OVER (PARTITION BY d.tilevalue ORDER BY m.CREATEDATE DESC) AS row_num,
						DECODE(d.tilevalue, NULL, 0, 1) AS chk1
					FROM DBO.TBLMES2DREAD_M m
					JOIN unpivot_d d ON m.SN = d.SN
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
					j.LotNo,
					j.RecordDate,
					CASE
						WHEN NVL(PANEL_ID1,'*') || NVL(PANEL_ID2,'*') || NVL(PANEL_ID3,'*') || NVL(PANEL_ID4,'*') <> '****'
							AND (PANEL_ID1 IS NULL OR PANEL_ID2 IS NULL OR PANEL_ID3 IS NULL OR PANEL_ID4 IS NULL)
						THEN 'Black'
						WHEN chk1 NOT IN (1)
						THEN 'Black'
						ELSE 'White'
					END AS ResultList,
					CASE
						WHEN NVL(PANEL_ID1,'*') || NVL(PANEL_ID2,'*') || NVL(PANEL_ID3,'*') || NVL(PANEL_ID4,'*') <> '****'
							AND (PANEL_ID1 IS NULL OR PANEL_ID2 IS NULL OR PANEL_ID3 IS NULL OR PANEL_ID4 IS NULL)
						THEN 'NG'
						WHEN chk1 NOT IN (1)
						THEN 'NG'
						ELSE 'PASS'
					END AS Reason
				FROM joined j
				LEFT JOIN zz ON j.TileId = zz.TileId AND j.LotNo = zz.LotNo
				WHERE j.row_num = 1 AND j.TileId IS NOT NULL
				ORDER BY j.TileId";

			var data = await repo.QueryAsync<TileCheckLaserInkDto>(sql, new { lotNo, daysRange });
			return data.ToList();
		}
	}
}
