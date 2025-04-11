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

			var repository = _repositoryFactory.CreateRepository(request.Environment);
			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repository, request.Deviceid);
			string tableName = $"TBLMESWIPDATA_{process}";

			var deviceList = request.Deviceids
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Trim())
				.ToArray();

			//--查詢說明：
			//--1.撈近 30 天內相同 LOTNO 的資料
			//-- 2.每個 TILEID 抓最大值的 NG 與最小值的 OK（根據 V008）
			//--3.若同時存在，計算 V008 差值並比較是否大於指定門檻

			string sql = $@"
                WITH XX AS (
                    SELECT TILEID, RECORDDATE, V007, V008
                    FROM {tableName}
                    WHERE LOTNO = :lotno
                      AND STEP = :opno
                      AND DEVICEID IN :deviceids
                      AND CSTYPE = 'PD'
                      AND TILEID IS NOT NULL
                      AND RECORDDATE > (TRUNC(SYSDATE) - 30)
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
                    'NG←→OK' AS V007,
                    NG.NG_V008,
                    OK.OK_V008,
                    NG.NG_DATE AS RECORDDATE,
                    NG.NG_V008 AS V008,      
                    ABS(TO_NUMBER(NG.NG_V008) - TO_NUMBER(OK.OK_V008)) AS DIFF_V008
                FROM NG
                JOIN OK ON NG.TILEID = OK.TILEID
                WHERE ABS(TO_NUMBER(NG.NG_V008) - TO_NUMBER(OK.OK_V008)) > :diff";

			var records = (await repository.QueryAsync<LeakageAnomalyDto>(sql, new
			{
				lotno = request.Lotno,
				opno = request.Opno,
				deviceids = deviceList,
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
			var repository = _repositoryFactory.CreateRepository(request.Environment);
			var process = await DeviceProcessHelper.GetProcessByDeviceIdAsync(repository, request.Deviceid);			
			string tableName = $"TBLMESWIPDATA_{process}";

			var deviceList = request.Deviceids
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Trim())
				.ToArray();

			string sql = $@"
                SELECT TILEID, V007, V008, RECORDDATE
                FROM {tableName}
                WHERE LOTNO = :lotno
                  AND STEP = :opno
                  AND DEVICEID IN :deviceids
                  AND CSTYPE = 'PD'
                  AND TILEID IS NOT NULL
                  AND RECORDDATE > (TRUNC(SYSDATE) - 30)
                ORDER BY TILEID, RECORDDATE";

			var rows = (await repository.QueryAsync<LeakageRawDataDto>(sql, new
			{
				lotno = request.Lotno,
				opno = request.Opno,
				deviceids = deviceList
			}))?.ToList();

			return ApiReturn<List<LeakageRawDataDto>>.Success("查詢成功", rows);
		}
	}
}
