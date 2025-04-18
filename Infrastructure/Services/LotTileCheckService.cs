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
			var repo = _repositoryFactory.CreateRepository(request.Environment);
			var result = new List<TileCheckResultDto>();

			// 1. 查詢規則表
			var rules = (await repo.QueryAsync<RuleCheckDefinition>(
				@"SELECT STEP, DEVICEIDS, EVAL_FORMULA AS EvalFormula, REASON, DAYS_RANGE 
				  FROM ARGOAPILOTTILERULECHECK
				  WHERE STEP = :step AND IS_ENABLED = 'Y'
				  ORDER BY PRIORITY",
				new { step = request.Step })).ToList();

			if (!rules.Any())
				return ApiReturn<List<TileCheckResultDto>>.Warning("無對應規則", result);

			// 2. 推算最大天數範圍（多條規則可能不一樣，取最大）
			var maxDays = rules.Max(r => r.DaysRange ?? 90);

			// 3. 查詢最新設備資料
			var records = (await repo.QueryAsync<TblMesWipData_Record>(
				@$"SELECT *
				   FROM (
					   SELECT TILEID, LOTNO, STEP, DEVICEID, RECORDDATE,
							  V001, V002, V003, V004, V005, V006, V007, V008,
							  V010, V011, V014, V015, V036, V037,
							  ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY RECORDDATE DESC) AS RN
					   FROM TBLMESWIPDATA_BACKEND_001
					   WHERE LOTNO = :lotno
						 AND STEP = :step
						 AND TILEID IS NOT NULL
						 AND RECORDDATE >= TRUNC(SYSDATE) - :days
				   ) T
				   WHERE RN = 1",
				new { lotno = request.LotNo, step = request.Step, days = maxDays }
			)).ToList();



			// 4. 建立雷射蓋印清單（供 MissingWork / MixLot 判斷）
			var laserTiles = (await repo.QueryAsync<TBLWIPLOTMARKINGDATA>(
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
	}
}
