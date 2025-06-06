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

			//var (repoDbo, repoCim, _) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			// 使用某個特定的資料庫
			var repoDbo = repositories["DboEmap"];
			var repoCim = repositories["CsCimEmap"];
			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.Deviceid);


			var (daysRange, diff) = await GetLeakageCheckParametersAsync(repoCim, request.Opno);


			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repoDbo, request.Deviceid);
			string tableName = $"TBLMESWIPDATA_{process}";

			string sql = $@"
                WITH XX AS (
                    SELECT TILEID, RECORDDATE, V007, V008
                    FROM {tableName}
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

			var records = (await repoDbo.QueryAsync<LeakageAnomalyDto>(sql, new
			{
				lotno = request.Lotno,
				steps = opnosToQuery,
				deviceids = deviceIdsToQuery,
				daysRange = daysRange,
				diff = diff
			}))?.ToList();

			if (records != null && records.Any())
			{
				string msg = $"TILEID 數 {records.Count}，存在 NG/OK 差異超過 {diff}";
				_logger.LogWarning("[LeakageCheck] Fail - " + msg);
				return ApiReturn<List<LeakageAnomalyDto>>.Failure(msg, records);
			}

			_logger.LogInformation("[LeakageCheck] Pass");
			return ApiReturn<List<LeakageAnomalyDto>>.Success("檢查通過", null);
		}

		public async Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request)
		{
			_logger.LogInformation($"[LeakageSelect] Request - lotno: {request.Lotno}, opno: {request.Opno}, deviceid: {request.Deviceid}");

			//var (repoDbo, repoCim, _) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			// 使用某個特定的資料庫
			var repoDbo = repositories["DboEmap"];
			var repoCim = repositories["CsCimEmap"];
			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.Deviceid);

			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				@"SELECT DAYSRANGE 
				  FROM ARGOAPILEAKAGERULECHECK 
				  WHERE OPNO IN :opnos",
				new { opnos = opnosToQuery })).ToList();
			var maxDays = rules.Max(r => r.DaysRange ?? 90);
			maxDays = 120;


			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repoDbo, request.Deviceid);
			string tableName = $"TBLMESWIPDATA_{process}";

			string sql = $@"
                SELECT TILEID, V007, V008, RECORDDATE
                FROM {tableName}
                WHERE LOTNO = :lotno
                  AND STEP IN :opnos
                  AND DEVICEID IN :deviceids
                  AND CSTYPE = 'PD'
                  AND TILEID IS NOT NULL
                  AND RECORDDATE > (TRUNC(SYSDATE) - :daysRange)
                ORDER BY TILEID, RECORDDATE";

			var rows = (await repoDbo.QueryAsync<LeakageRawDataDto>(sql, new
			{
				lotno = request.Lotno,
				opnos = opnosToQuery,
				deviceids = deviceIdsToQuery,
				daysRange = maxDays
			}))?.ToList();

			return ApiReturn<List<LeakageRawDataDto>>.Success("查詢成功", rows);
		}

		private async Task<(int daysRange, float diff)> GetLeakageCheckParametersAsync(IRepository repoCim, string opno)
		{
			// 查詢 OPNOGROUP
			var groupAttr = await repoCim.QueryFirstOrDefaultAsync<dynamic>(
				"SELECT VALUE FROM ARGOCIMOPNOATTRIBUTE WHERE OPNO = :opno AND ITEM = 'OPNOGROUP'",
				new { opno });

			if (groupAttr == null || string.IsNullOrEmpty(groupAttr.VALUE?.ToString()))
				throw new Exception($"站點 {opno} 查無 OPNOGROUP 資料");

			string opnoGroup = groupAttr.VALUE.ToString();

			// 查詢 DAYSRANGE
			var dayAttr = await repoCim.QueryFirstOrDefaultAsync<dynamic>(
				"SELECT VALUE FROM ARGOCIMOPNOGROUPPARAMETER WHERE OPNOGROUP = :opnoGroup AND ITEM = 'DAYSRANGE'",
				new { opnoGroup });

			int d;
			int daysRange = int.TryParse(dayAttr?.VALUE?.ToString(), out d) ? d : 30;


			// 查詢 DIFF
			var diffAttr = await repoCim.QueryFirstOrDefaultAsync<dynamic>(
				"SELECT VALUE FROM ARGOCIMOPNOGROUPPARAMETER WHERE OPNOGROUP = :opnoGroup AND ITEM = 'DIFF'",
				new { opnoGroup });

			float f;
			float diff = float.TryParse(diffAttr?.VALUE?.ToString(), out f) ? f : 0.05f;

			return (daysRange, diff);
		}
	}
}