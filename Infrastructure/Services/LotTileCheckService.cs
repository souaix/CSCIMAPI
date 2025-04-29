using Core.Entities.DboEmap;
using Core.Entities.LotTileCheck;
using Core.Entities.Public;
using Core.Interfaces;
using Dapper;
using Infrastructure.Utilities;
using System.Text;

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

			//解析查詢規則(S1~S4)
			var (stepsToQuery, deviceIdsToQuery) = await DataQueryModeAsync(repoCim, request.Step, request.DeviceId);

			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				@"SELECT STEP, DEVICEIDS, EVALFORMULA AS EvalFormula, REASON, PRIORITY, DAYSRANGE, ENABLEMISSINGWORK, ENABLEMIXLOT, ENABLENG
				  FROM ARGOAPILOTTILERULECHECK 
				  WHERE STEP IN :steps 
				  ORDER BY PRIORITY",
				new { steps = stepsToQuery })).ToList();

			if (!rules.Any())
				return ApiReturn<List<TileCheckResultDto>>.Warning("無對應規則", result);

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

			// 3. 撈 WIP 資料 (UNION ALL 多表)
			var unionSql = new StringBuilder();
			var parameters = new DynamicParameters();
			parameters.Add("lotno", request.LotNo);
			parameters.Add("steps", stepsToQuery);
			parameters.Add("days", maxDays);

			int idx = 0;
			foreach (var group in processGroups)
			{
				string process = group.Key;
				var devs = group.Value;

				string paramName = $"deviceids{idx}";
				parameters.Add(paramName, devs);

				if (idx > 0) unionSql.AppendLine("UNION ALL");

				unionSql.AppendLine($@"
				SELECT * FROM (
				SELECT TILEID, LOTNO, STEP, DEVICEID, RECORDDATE,
					   V001, V002, V003, V004, V005, V006, V007, V008,
					   V010, V011, V014, V015, V036, V037,
					   ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY RECORDDATE DESC) AS RN
				FROM TBLMESWIPDATA_{process}
				WHERE LOTNO = :lotno
				  AND STEP IN :steps
				  AND DEVICEID IN :{paramName}
				  AND TILEID IS NOT NULL
				  AND RECORDDATE >= TRUNC(SYSDATE) - :days
				) WHERE RN = 1");

				idx++;
			}

			var records = (await repoDbo.QueryAsync<TblMesWipData_Record>(unionSql.ToString(), parameters)).ToList();

			// 4. 建立雷射蓋印清單
			var laserTiles = (await repoDbo.QueryAsync<TBLWIPLOTMARKINGDATA>(
				"SELECT TILEID FROM TBLWIPLOTMARKINGDATA WHERE LOTNO = :lotno",
				new { lotno = request.LotNo })).Select(x => x.TileId).ToHashSet();

			var producedTileSet = records.Select(x => x.TileId).ToHashSet();
			var allowMissingWork = rules.Any(r => r.EnableMissingWork == "Y");
			if (request.DisableMissingWorkFlag == 1)
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

			return ApiReturn<List<TileCheckResultDto>>.Success("完成比對", result);
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


		//此段為查詢設定表，依站點決定要走單站單機或是單站多機, 多站單機或是多站多機
		public async Task<(List<string> Steps, List<string> DeviceIds)> DataQueryModeAsync(
			IRepository repo, string step, string deviceId)
		{
			// 查詢模式與群組
			var modeRow = await repo.QueryFirstOrDefaultAsync<(string QueryMode, string StepGroup)>(
				@"SELECT QUERYMODE, STEPGROUP 
          FROM ARGOAPILOTTILESTEPMODE 
          WHERE STEP = :step", new { step });

			if (modeRow.QueryMode == null)
			{
				throw new Exception("未設定黑白名單站點對應邏輯，請洽詢工程師");
			}

			var queryMode = modeRow.QueryMode;
			var stepGroup = modeRow.StepGroup;

			List<string> steps = new();
			List<string> deviceIds = new();

			switch (queryMode)
			{
				case "S1": // 單站 + 單機
					steps.Add(step);
					deviceIds.Add(deviceId);
					break;

				case "S2": // 單站 + 多機
					steps.Add(step);
					var rulesS2 = await repo.QueryAsync<string>(
						@"SELECT DISTINCT DEVICEIDS 
                  FROM ARGOAPILOTTILERULECHECK 
                  WHERE STEP = :step", new { step });
					deviceIds = rulesS2
						.SelectMany(s => s.Split(','))
						.Select(x => x.Trim())
						.Distinct()
						.ToList();
					break;

				case "S3": // 同群組多站 + 單機
					steps = (await repo.QueryAsync<string>(
						@"SELECT STEP 
                  FROM ARGOAPILOTTILESTEPMODE 
                  WHERE STEPGROUP = :stepgroup", new { stepgroup = stepGroup }))
						.ToList();
					deviceIds.Add(deviceId);
					break;

				case "S4": // 同群組多站 + 多機
					steps = (await repo.QueryAsync<string>(
						@"SELECT STEP 
                  FROM ARGOAPILOTTILESTEPMODE 
                  WHERE STEPGROUP = :stepgroup", new { stepgroup = stepGroup }))
						.ToList();

					var rulesS4 = await repo.QueryAsync<string>(
						@"SELECT DISTINCT DEVICEIDS 
                  FROM ARGOAPILOTTILERULECHECK 
                  WHERE STEP IN :steps", new { steps });

					deviceIds = rulesS4
						.SelectMany(s => s.Split(','))
						.Select(x => x.Trim())
						.Distinct()
						.ToList();
					break;

				default:
					throw new Exception($"未知的查詢模式：{queryMode}");
			}

			return (steps, deviceIds);
		}

	}
}
