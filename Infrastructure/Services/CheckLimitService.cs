using Core.Entities.CheckLimit;
using Core.Entities.Public;
using Core.Interfaces;
using Infrastructure.Utilities;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class CheckLimitService : ICheckLimitService
    {
        private readonly IRepositoryFactory _repositoryFactory;

        public CheckLimitService(IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public async Task<ApiReturn<CheckLimitResponse>> CheckLimitAsync(CheckLimitRequest request)
        {
            // MESPD003 設備參數異常   ->MESPD-HOLD
            // MESPD001 無生產批       ->MESPD-HOLD
            // MESPD005 WIP設備錯誤    ->MESPD-HOLD
            // MESPD002 設備參數正常   ->可正常CHECK-OUT
            // MESPD004 MES無設備主檔  ->無法CHECK-OUT請通知IT

            var result = new CheckLimitResponse();
            var repoDict = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);


            if (!repoDict.TryGetValue("DboEmap", out var repoDbo))
                return ApiReturn<CheckLimitResponse>.Failure("找不到 DboEmap 資料庫設定");

            // 檢查設備是否存在於 TBLMESDEVICELIST
            //Step 1：查設備主檔與製程名
            var device = await repoDbo.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT DEVICEID, PROCESS FROM DBO.TBLMESDEVICELIST WHERE DEVICEID = :DeviceId",
                new { DeviceId = request.DeviceId });

            if (device == null)
            {
                result.Status = "MESPD004";
                result.Message = "MES 無設備主檔";
                return ApiReturn<CheckLimitResponse>.Failure(result.Message, result);
            }

            string process = device.PROCESS;

            //Step 2：查詢站別對應的設備群（多台）
            var eqNos = await repoDbo.QueryAsync<string>(
                @"SELECT EQ_NO 
                  FROM THWIP.TBLWIPOPDATA_EQ@THWIP 
                  WHERE OPNO = :Opno AND ISENABLE = 1",
                new { request.Opno });

            //這邊一定會有值，因為MES先判斷有值才會呼叫
            //if (!eqNos.Any())
            //{
            //    result.Status = "MESPD001";
            //    result.Message = "查無站別對應設備群";
            //    return ApiReturn<CheckLimitResponse>.Failure(result.Message, result);
            //}


            //Step 3：查 7 天內該 LOT 生產紀錄

            string wipTable = $"TBLMESWIPDATA_{process}";
            var rawData = await repoDbo.QueryAsync<dynamic>(
                $@"
                SELECT * FROM {wipTable}
                WHERE TRIM(LOTNO) = :Lotno 
                  AND TRIM(STEP) = :Step 
                  AND CSTYPE = 'PD'
                  AND TRIM(DEVICEID) IN ({string.Join(",", eqNos.Select(e => $"'{e}'"))})
                  AND TRIM(DEVICEID) NOT IN ('IS-012')
                  AND RecordDate > SYSDATE - 7",
                new { Lotno = request.Lotno, Step = request.Opno });

            if (!rawData.Any())
            {
                result.Status = "MESPD001";
                result.Message = "無此批號的生產資料";
                return ApiReturn<CheckLimitResponse>.Failure(result.Message, result);
            }


            //Step 4：查上下限設定（先撈站別設定，再撈 group = 1）
            var limits = await repoDbo.QueryAsync<dynamic>(
                @"
                SELECT DT.DEVICEID, DT.EQ_PARAMNAME, DT.EQ_PARAM, 
                       DT.EQ_SPECRANGE_MIN, DT.EQ_SPECRANGE_MAX
                FROM THWIP.TBLMESMACHINE_PARAM_OPTDT@THWIP DT
                JOIN (
                    SELECT DISTINCT DEVICEID, OPNO_GROUP, OPNO
                    FROM DBO.TBLMESDEVICELIST_OPNO
                ) OL 
                  ON DT.DEVICEID = OL.DEVICEID AND DT.EQ_GROUP = OL.OPNO_GROUP
                WHERE OL.OPNO = :Opno
                  AND DT.DEVICEID = :DeviceId
                  AND DT.EQ_PARAM NOT IN ('PARTNO','LOTNO','TILEID','STEP')
                  AND DT.EQ_SPECVAL IS NOT NULL
                  AND NVL(DT.EQ_RUNCHECK,0) = 1
                ",
                new { Opno = request.Opno, DeviceId = request.DeviceId });

                if (!limits.Any())
                {
                    // Fallback：查 EQ_GROUP = 1 的設定
                    limits = await repoDbo.QueryAsync<dynamic>(
                        @"
                        SELECT DT.DEVICEID, DT.EQ_PARAMNAME, DT.EQ_PARAM, 
                               DT.EQ_SPECRANGE_MIN, DT.EQ_SPECRANGE_MAX
                        FROM THWIP.TBLMESMACHINE_PARAM_OPTDT@THWIP DT
                        JOIN (
                            SELECT DISTINCT DEVICEID, OPNO_GROUP, OPNO
                            FROM DBO.TBLMESDEVICELIST_OPNO
                        ) OL 
                          ON DT.DEVICEID = OL.DEVICEID AND DT.EQ_GROUP = OL.OPNO_GROUP
                        WHERE DT.DEVICEID = :DeviceId
                          AND DT.EQ_GROUP = 1
                          AND DT.EQ_PARAM NOT IN ('PARTNO','LOTNO','TILEID','STEP')
                          AND DT.EQ_SPECVAL IS NOT NULL
                          AND NVL(DT.EQ_RUNCHECK,0) = 1
                        ",
                        new { DeviceId = request.DeviceId });
                }


            // Step 5：執行逐欄位比對（動態欄位 + 判斷是否超規）
            foreach (var row in rawData)
            {
                var rowDict = (IDictionary<string, object>)row;

                foreach (var limit in limits)
                {
                    string col = limit.EQ_PARAM;
                    if (!rowDict.ContainsKey(col)) continue;

                    double val = 0, min = 0, max = 0;

                    if (double.TryParse(Convert.ToString(rowDict[col]), out val) &&
                        double.TryParse(Convert.ToString(limit.EQ_SPECRANGE_MIN), out min) &&
                        double.TryParse(Convert.ToString(limit.EQ_SPECRANGE_MAX), out max))
                    {
                        if (val < min || val > max)
                        {
                            result.Details.Add(new CheckLimitDetailDto
                            {
                                DeviceId = Convert.ToString(rowDict["DEVICEID"]),
                                Opno = Convert.ToString(rowDict["STEP"]),
                                Lotno = Convert.ToString(rowDict["LOTNO"]),
                                CreateDate = Convert.ToDateTime(rowDict["RECORDDATE"]).ToString("yyyy/MM/dd HH:mm:ss"),
                                ColName = col,
                                ColCname = limit.EQ_PARAMNAME,
                                Value = val,
                                Min = min,
                                Max = max
                            });
                        }
                    }
                }
            }


            //回傳結果
            if (result.Details.Any())
            {
                result.Status = "MESPD003";
                result.Message = "設備參數異常";
                return ApiReturn<CheckLimitResponse>.Failure(result.Message, result);
            }
            else
            {
                result.Status = "MESPD002";
                result.Message = "設備參數正常";
                return ApiReturn<CheckLimitResponse>.Success(result.Message, result);
            }


            // 👉 實際上下限邏輯暫時略過，先回傳樣板結果
            //result.Status = "MESPD002";
            //result.Message = "設備參數正常";
            //return ApiReturn<CheckLimitResponse>.Success(result.Message, result);
        }
    }
}
