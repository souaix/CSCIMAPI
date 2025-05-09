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
using Core.Entities.LotTileCheck;

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
			_logger.LogInformation($"[LeakageCheck] Request - lotno: {request.Lotno}, opno: {request.Opno}, deviceid: {request.Deviceid}, diff: {request.Diff}");

			var (repoDbo, repoCim) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);			
			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.Deviceid);


			// 新增：查詢規則表決定最大天數
			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				@"SELECT DAYSRANGE 
				  FROM ARGOAPILOTTILERULECHECK 
				  WHERE OPNO IN :opnos",
				new { opnos = opnosToQuery })).ToList();
			var maxDays = rules.Max(r => r.DaysRange ?? 90);

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
				daysRange = maxDays,
				diff = request.Diff
			}))?.ToList();

			if (records != null && records.Any())
			{
				string msg = $"TILEID 數 {records.Count}，存在 NG/OK 差異超過 {request.Diff}";
				_logger.LogWarning("[LeakageCheck] Fail - " + msg);
				return ApiReturn<List<LeakageAnomalyDto>>.Failure(msg, records);
			}

			_logger.LogInformation("[LeakageCheck] Pass");
			return ApiReturn<List<LeakageAnomalyDto>>.Success("檢查通過", null);
		}

		public async Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request)
		{
			_logger.LogInformation($"[LeakageSelect] Request - lotno: {request.Lotno}, opno: {request.Opno}, deviceid: {request.Deviceid}");

			var (repoDbo, repoCim) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
			var (opnosToQuery, deviceIdsToQuery) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repoCim, request.Opno, request.Deviceid);

			var rules = (await repoCim.QueryAsync<RuleCheckDefinition>(
				@"SELECT DAYSRANGE 
				  FROM ARGOAPILOTTILERULECHECK 
				  WHERE OPNO IN :opnos",
				new { opnos = opnosToQuery })).ToList();
			var maxDays = rules.Max(r => r.DaysRange ?? 90);

			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repoDbo, request.Deviceid);
			string tableName = $"TBLMESWIPDATA_{process}";

			string sql = $@"
                SELECT TILEID, V007, V008, RECORDDATE
                FROM {tableName}
                WHERE LOTNO = :lotno
                  AND STEP IN :steps
                  AND DEVICEID IN :deviceids
                  AND CSTYPE = 'PD'
                  AND TILEID IS NOT NULL
                  AND RECORDDATE > (TRUNC(SYSDATE) - :daysRange)
                ORDER BY TILEID, RECORDDATE";

			var rows = (await repoDbo.QueryAsync<LeakageRawDataDto>(sql, new
			{
				lotno = request.Lotno,
				opno = opnosToQuery,
				deviceids = deviceIdsToQuery,
				daysRange = maxDays
			}))?.ToList();

			return ApiReturn<List<LeakageRawDataDto>>.Success("查詢成功", rows);
		}
	}
}