// LeakageCheckService.cs
using Core.Entities.DboEmap;
using Core.Entities.LeakageCheck;
using Core.Entities.Public;
using Core.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Utilities;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Services
{
	public class LeakageCheckService : ILeakageCheckService
	{
		private readonly IRepositoryFactory _repositoryFactory;
		private readonly ILogger<LeakageCheckService> _logger;

		public LeakageCheckService(IRepositoryFactory repositoryFactory, ILogger<LeakageCheckService> logger)
		{
			_repositoryFactory = repositoryFactory;
			_logger = logger;
		}

		public async Task<ApiReturn<List<LeakageAnomalyDto>>> LeakageCheckAsync(LeakageCheckRequest request)
		{
			_logger.LogInformation($"[LeakageCheck] Request - lotno: {request.Lotno}, opno: {request.Opno}, deviceid: {request.Deviceid}");

			LeakageContext context;
			try
			{
				context = await BuildLeakageContextAsync(request);
			}
			catch (Exception ex)
			{
				return ApiReturn<List<LeakageAnomalyDto>>.Failure(ex.Message);
			}

			string sql = $@"
		WITH XX AS (
			SELECT TILEID, RECORDDATE, V007, V008
			FROM {context.TableName}
			WHERE LOTNO = :lotno
			  AND STEP IN :steps
			  AND DEVICEID IN :deviceids
			  AND CSTYPE = 'PD'
			  AND TILEID IS NOT NULL
			  AND RECORDDATE > (TRUNC(SYSDATE) - :daysRange)
		),
		NG AS (
			SELECT TILEID, V008 AS NG_V008, RECORDDATE AS NG_DATE
			FROM (
				SELECT TILEID, V008, RECORDDATE,
					   ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY TO_NUMBER(V008) DESC) AS RN
				FROM XX WHERE V007 = 'NG'
			) WHERE RN = 1
		),
		OK AS (
			SELECT TILEID, V008 AS OK_V008, RECORDDATE AS OK_DATE
			FROM (
				SELECT TILEID, V008, RECORDDATE,
					   ROW_NUMBER() OVER (PARTITION BY TILEID ORDER BY TO_NUMBER(V008) ASC) AS RN
				FROM XX WHERE V007 = 'OK'
			) WHERE RN = 1
		)
		SELECT 
			NG.TILEID,
			NG.NG_DATE AS NG_RECORDDATE,
			OK.OK_DATE AS OK_RECORDDATE,
			'NG↔OK' AS V007,
			NG.NG_V008,
			OK.OK_V008,
			NG.NG_DATE AS RECORDDATE,
			NG.NG_V008 AS V008,      
			ABS(TO_NUMBER(NG.NG_V008) - TO_NUMBER(OK.OK_V008)) AS DIFF_V008
		FROM NG
		JOIN OK ON NG.TILEID = OK.TILEID
		WHERE ABS(TO_NUMBER(NG.NG_V008) - TO_NUMBER(OK.OK_V008)) > :diff";

			var records = (await context.RepoDbo.QueryAsync<LeakageAnomalyDto>(sql, new
			{
				lotno = request.Lotno,
				steps = context.OpnosToQuery,
				deviceids = context.DeviceIdsToQuery,
				daysRange = context.DaysRange,
				diff = context.Diff
			}))?.ToList();

			if (records != null && records.Any())
			{
				string msg = $"TILEID 數 {records.Count}，存在 NG/OK 差異超過 {context.Diff}";
				_logger.LogWarning("[LeakageCheck] Fail - " + msg);
				return ApiReturn<List<LeakageAnomalyDto>>.Failure(msg, records);
			}

			_logger.LogInformation("[LeakageCheck] Pass");
			return ApiReturn<List<LeakageAnomalyDto>>.Success("檢查通過", null);
		}

		public async Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request)
		{
			_logger.LogInformation($"[LeakageSelect] Request - lotno: {request.Lotno}, opno: {request.Opno}, deviceid: {request.Deviceid}");

			LeakageContext context;
			try
			{
				context = await BuildLeakageContextAsync(request);
			}
			catch (Exception ex)
			{
				return ApiReturn<List<LeakageRawDataDto>>.Failure(ex.Message);
			}

			string sql = $@"
				SELECT TILEID, V007, V008, RECORDDATE
				FROM {context.TableName}
				WHERE LOTNO = :lotno
				  AND STEP IN :opnos
				  AND DEVICEID IN :deviceids
				  AND CSTYPE = 'PD'
				  AND TILEID IS NOT NULL
				  AND RECORDDATE > (TRUNC(SYSDATE) - :daysRange)
				ORDER BY TILEID, RECORDDATE";

			var rows = (await context.RepoDbo.QueryAsync<LeakageRawDataDto>(sql, new
			{
				lotno = request.Lotno,
				opnos = context.OpnosToQuery,
				deviceids = context.DeviceIdsToQuery,
				daysRange = context.DaysRange
			}))?.ToList();

			return ApiReturn<List<LeakageRawDataDto>>.Success("查詢成功", rows);
		}

		private class LeakageContext
		{
			public IRepository RepoDbo { get; set; }
			public IRepository RepoCim { get; set; }
			public List<string> OpnosToQuery { get; set; }
			public List<string> DeviceIdsToQuery { get; set; }
			public int DaysRange { get; set; }
			public float Diff { get; set; }
			public string TableName { get; set; }
		}

		private async Task<LeakageContext> BuildLeakageContextAsync(LeakageCheckRequest request)
		{
			var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			var repoDbo = repositories["DboEmap"];
			var repoCim = repositories["CsCimEmap"];

			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.Deviceid);

			int daysRange = 30;
			float diff = 0.05f;

			try
			{
				(daysRange, diff) = await GetLeakageCheckParametersAsync(repoCim, request.Opno);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[BuildLeakageContext] Failed to get parameters");
				throw new Exception("查詢規則時發生錯誤：" + ex.Message);
			}

			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repoDbo, request.Deviceid);
			string tableName = $"TBLMESWIPDATA_{process}";

			return new LeakageContext
			{
				RepoDbo = repoDbo,
				RepoCim = repoCim,
				OpnosToQuery = opnosToQuery,
				DeviceIdsToQuery = deviceIdsToQuery,
				DaysRange = daysRange,
				Diff = diff,
				TableName = tableName
			};
		}


		private async Task<(int daysRange, float diff)> GetLeakageCheckParametersAsync(IRepository repoCim, string opno)
		{
			// 一次性查詢 OPNOGROUP、DAYSRANGE、DIFF
			var results = await repoCim.QueryAsync<dynamic>(
				@"
					WITH OPNOGROUP_CTE AS (
						SELECT VALUE AS OPNOGROUP
						FROM ARGOCIMOPNOATTRIBUTE
						WHERE OPNO = :opno AND ITEM = 'OPNOGROUP'
					)
					SELECT 
						p.ITEM,
						p.VALUE
					FROM OPNOGROUP_CTE g
					JOIN ARGOAPIOPNOGROUPPARAMETER p
						ON p.OPNOGROUP = g.OPNOGROUP
					WHERE p.ITEM IN ('DAYSRANGE', 'DIFF')
					",
				new { opno });

			int daysRange = 30;
			float diff = 0.05f;

			if (results == null || !results.Any())
				throw new Exception($"站點 {opno} 查無 OPNOGROUP 或相關參數資料");

			foreach (var attr in results)
			{
				string item = attr.ITEM?.ToString()?.ToUpper();
				string value = attr.VALUE?.ToString();

				if (item == "DAYSRANGE" && int.TryParse(value, out int d))
					daysRange = d;

				if (item == "DIFF" && float.TryParse(value, out float f))
					diff = f;
			}


			return (daysRange, diff);
		}
	}
}