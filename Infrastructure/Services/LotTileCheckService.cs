using Core.Entities.DboEmap;
using Core.Entities.LotTileCheck;
using Core.Entities.Public;
using Core.Interfaces;
using Dapper;
using Infrastructure.Utilities;
using Mysqlx.Crud;
using System.Collections.Concurrent;
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

		public async Task<ApiReturn<object>> CheckLotTileAsync(LotTileCheckRequest request)
		{
			var (repoDbo, repoCim) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
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
				return ApiReturn<object>.Success("完成雷射站比對", laserResults
					.Select(x => new TileCheckLaserInkDto
					{
						TileId = x.TileId,
						LotNo = x.LotNo,
						ResultList = x.ResultList,
						Reason = x.Reason,
						RecordDate = x.RecordDate
						//,
						//CS_TileId = x.CS_TileId,
						//CS_TileId2 = x.CS_TileId2,
						//CS_TileId3 = x.CS_TileId3,
						//CS_TileId4 = x.CS_TileId4
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
				  AND STEP IN :opnos
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
			/* === 備註 ===
   1) UNPIVOT 只做兩件事：取 SN、tilevalue
   2) 需要的 PANEL_IDx 及 CS_TILEIDx，都在最後再用 SN 回原表撈
*/
			var sql = $@"
					WITH src AS (
    SELECT
        SN,
        PANEL_ID1, PANEL_ID2, PANEL_ID3, PANEL_ID4,
        CS_TILEID, CS_TILEID2, CS_TILEID3, CS_TILEID4,
        TH_TILEID, TH_TILEID_2
    FROM DBO.TBLMES2DREAD_D
),
unpivot_d AS (
    SELECT
        SN,
        tilevalue
    FROM src
    UNPIVOT(tilevalue FOR tile IN (
        CS_TILEID, CS_TILEID2, CS_TILEID3, CS_TILEID4
    ))
),
joined AS (
    SELECT
        m.SN,
        m.CREATEDATE AS RecordDate,
        m.LOTNO AS LotNo,
        m.DEVICEID,
        u.tilevalue AS TileId,
        s.CS_TILEID, s.CS_TILEID2, s.CS_TILEID3, s.CS_TILEID4,
        s.TH_TILEID, s.TH_TILEID_2,
        s.PANEL_ID1, s.PANEL_ID2, s.PANEL_ID3, s.PANEL_ID4,
        ROW_NUMBER() OVER (PARTITION BY u.tilevalue ORDER BY m.CREATEDATE DESC) AS row_num,
        DECODE(u.tilevalue, NULL, 0, 1) AS chk1
    FROM DBO.TBLMES2DREAD_M m
    JOIN unpivot_d u ON m.SN = u.SN
    LEFT JOIN src s ON m.SN = s.SN
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
    j.CS_TILEID, j.CS_TILEID2, j.CS_TILEID3, j.CS_TILEID4,
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




			//var sql = $@"
			//	WITH unpivot_d AS (
			//		SELECT
			//			SN,
			//			PANEL_ID1, PANEL_ID2, PANEL_ID3, PANEL_ID4,
			//			tilevalue
			//		FROM (
			//			SELECT * FROM DBO.TBLMES2DREAD_D
			//		) d
			//		UNPIVOT(tilevalue FOR tile IN (
			//			CS_TILEID, CS_TILEID2, CS_TILEID3, CS_TILEID4
			//		))
			//	),
			//	joined AS (
			//		SELECT
			//			m.CREATEDATE AS RecordDate,
			//			m.LOTNO AS LotNo,
			//			m.DEVICEID,
			//			d.tilevalue AS TileId,
			//			d.PANEL_ID1, d.PANEL_ID2, d.PANEL_ID3, d.PANEL_ID4,
			//			ROW_NUMBER() OVER (PARTITION BY d.tilevalue ORDER BY m.CREATEDATE DESC) AS row_num,
			//			DECODE(d.tilevalue, NULL, 0, 1) AS chk1
			//		FROM DBO.TBLMES2DREAD_M m
			//		JOIN unpivot_d d ON m.SN = d.SN
			//		WHERE m.LOTNO = :lotNo
			//		  AND m.DEVICEID IN ({deviceIdSql})
			//		  AND m.CREATEDATE > (TRUNC(SYSDATE) - :daysRange)
			//	),
			//	zz AS (
			//		SELECT TILEID, LOTNO, TILEGROUP
			//		FROM DBO.TBLWIPLOTMARKINGDATA
			//		WHERE LOTNO = :lotNo
			//		  AND ORACLEDATE > (TRUNC(SYSDATE) - :daysRange)
			//	)
			//	SELECT
			//		j.TileId,
			//		j.LotNo,
			//		j.RecordDate,
			//		CASE
			//			WHEN NVL(PANEL_ID1,'*') || NVL(PANEL_ID2,'*') || NVL(PANEL_ID3,'*') || NVL(PANEL_ID4,'*') <> '****'
			//				AND (PANEL_ID1 IS NULL OR PANEL_ID2 IS NULL OR PANEL_ID3 IS NULL OR PANEL_ID4 IS NULL)
			//			THEN 'Black'
			//			WHEN chk1 NOT IN (1)
			//			THEN 'Black'
			//			ELSE 'White'
			//		END AS ResultList,
			//		CASE
			//			WHEN NVL(PANEL_ID1,'*') || NVL(PANEL_ID2,'*') || NVL(PANEL_ID3,'*') || NVL(PANEL_ID4,'*') <> '****'
			//				AND (PANEL_ID1 IS NULL OR PANEL_ID2 IS NULL OR PANEL_ID3 IS NULL OR PANEL_ID4 IS NULL)
			//			THEN 'NG'
			//			WHEN chk1 NOT IN (1)
			//			THEN 'NG'
			//			ELSE 'PASS'
			//		END AS Reason
			//	FROM joined j
			//	LEFT JOIN zz ON j.TileId = zz.TileId AND j.LotNo = zz.LotNo
			//	WHERE j.row_num = 1 AND j.TileId IS NOT NULL
			//	ORDER BY j.TileId";

			Console.WriteLine($@"
🟢 Final SQL:

{sql.Replace(":lotNo", $"'{lotNo}'")
				.Replace(":daysRange", $"{daysRange}")
}");  // ← 用你的 deviceIdSql 實際內容替代


			var data = await repo.QueryAsync<TileCheckLaserInkDto>(sql, new { lotNo, daysRange });
			return data.ToList();
		}


	}
}
