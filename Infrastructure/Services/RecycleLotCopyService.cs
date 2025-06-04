using Core.Entities.Public;
using Core.Entities.RecycleLotCopy;
using Core.Interfaces;
using Dapper;

//using CSCimAPI.Models;
using Infrastructure.Utilities;
using System.Net;

namespace Infrastructure.Services
{
    public class RecycleLotCopyService : IRecycleLotCopyService
    {
        private readonly IRepositoryFactory _repositoryFactory;
        //private readonly IFtpService _ftpService;
        //private readonly IFtpPathResolver _ftpPathResolver;

        public RecycleLotCopyService( IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;

        }

        public async Task<ApiReturn<string>> ProcessRecycleLotCopyAsync(RecycleLotCopyRequest request)
        {
            string tempRoot = null;
            try
            {
                
                var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
                //var repoDbo = repositories["DboEmap"];
                var repoCim = repositories["CsCimEmap"];

                var lotNo = request.LotNo;
                var mLotNo = request.M_LotNo;
                tempRoot = Path.Combine("C:\\TEMP", lotNo);

                // Step 1: 檢查是否已搬過檔（從 DB 查詢）
                var existSql = "SELECT COUNT(*) FROM ARGOAPILOTRECYCLEFILERECORD WHERE LOTNO = :LotNo";
                var count = await repoCim.QueryFirstOrDefaultAsync<int>(existSql, new { LotNo = lotNo });
                if (count > 0)
                    //return ApiReturn<string>.Failure("該回收批號已經做過搬檔作業。");
                    return ApiReturn<string>.Failure("This Recycle LotNo already done the Copy Job.");

                // Step 2: 取得 FTP 設定資料
                var ftpSql = @"SELECT * FROM ARGOCIMDEVICEFILESETTING 
                            WHERE DEVICEGROUPNO = 'RecycleLot' AND TYPE = 'FTP'";
                var ftpSettings = await repoCim.QueryAsync<FtpPathSetting>(ftpSql);

                // Step 3: 檢查回收批 FTP 路徑是否已存在
                //測試時檢查ITTEST 目錄
                string[] checkTargets;
                if (request.Environment == "Production")
                {
                    checkTargets = new[]
                    {
                        "K_DIRNO_T_EMP_FTP_TEST",
                        "K_DIRNO_T_EMP_FTP_TOP",
                        "K_DIRNO_T_EMP_FTP_BOT",
                        "K_DIRNO_T_EMP_FTP_MERGE"
                    };
                }
                else //if (request.Environment == "Test")
                {
                    checkTargets = new[]
                    {
                        "IT_TEST_T_EMP_FTP_TEST",
                        "IT_TEST_T_EMP_FTP_TOP",
                        "IT_TEST_T_EMP_FTP_BOT",
                        "IT_TEST_T_EMP_FTP_MERGE"
                    };
                }

                

                foreach (var devNo in checkTargets)
                {
                    var setting = ftpSettings.FirstOrDefault(x => x.DeviceNo == devNo);
                    if (setting == null) continue;
                    var ftpPath =  FtpPathResolver.ResolvePath(setting, lotNo);
                    var exists = await FtpService.DirectoryExistsAsync(setting.FtpSite, ftpPath, setting.PathAccount, setting.PathPassword);
                    if (exists)
                        //return ApiReturn<string>.Failure($"回收批 FTP 目錄已存在：{ftpPath}");
                        return ApiReturn<string>.Failure($"Recycle LotNo FTP directory already exist：{ftpPath}");
                }

                // Step 4: 檢查母批 FTP 檔案是否存在（Y：TOP/BOT/TEST，N：MERGE）
                var folders = new[] { "AOIT", "AOIB", "TEST", "MERGE" };
                if (request.Emapping == "Y")
                {
                    var setting = ftpSettings.FirstOrDefault(x => x.DeviceNo == "H_DIRNO_T_EMP_MOUNT");
                    if (setting == null)
                        //return ApiReturn<string>.Failure("無法取得 H_DIRNO_T_EMP_MOUNT 的 FTP 設定");
                        return ApiReturn<string>.Failure("Cannot get H_DIRNO_T_EMP_MOUNT FTP configure");

                    var mountCode = GetMountCode(mLotNo);
                    var mapFolder = "MAP" + mLotNo.Substring(2, 5);
                    var baseFtpPath = setting.FilePath.TrimEnd('/') + mountCode + "/" + mapFolder + "/BACKUP/";
                    var sourceFolders = new Dictionary<string, string>
                        {
                            {"AOIT", "AOIT"},
                            {"AOIB", "AOIB"},
                            {"TEST", "TEST"},
                            {"MERGE", "AOIB"} // MERGE 與 AOIB 同目錄來源
                        };

                    foreach (var tileId in request.TileID)
                    {
                        foreach (var folder in folders)
                        {
                            var path = baseFtpPath + sourceFolders[folder] + "/" + mLotNo + "/" + tileId + ".txt";
                            var fileBytes = await FtpService.DownloadFileAsync(setting.FtpSite, path, setting.PathAccount, setting.PathPassword);
                            if (fileBytes == null || fileBytes.Length == 0)
                                //return ApiReturn<string>.Failure($"母批 FTP 檔案不存在或為空：{path}");
                                return ApiReturn<string>.Failure($"Mother LotNo FTP file didn't exist or empty：{path}");
                        }
                    }
                }
                else if (request.Emapping == "N")
                {
                    var mergeSetting = ftpSettings.FirstOrDefault(x => x.DeviceNo == "K_DIRNO_T_EMP_FTP_MERGE");
                    if (mergeSetting == null)
                        return ApiReturn<string>.Failure("無法取得 FTP MERGE 設定");

                    foreach (var tileId in request.TileID)
                    {
                        var ftpPath = FtpPathResolver.ResolvePath(mergeSetting, mLotNo).TrimEnd('/') + "/" + tileId + ".txt";
                        var fileBytes = await FtpService.DownloadFileAsync(mergeSetting.FtpSite, ftpPath, mergeSetting.PathAccount, mergeSetting.PathPassword);
                        if (fileBytes == null || fileBytes.Length == 0)
                            //return ApiReturn<string>.Failure($"母批 FTP 檔案不存在或為空：{ftpPath}");
                            return ApiReturn<string>.Failure($"Mother LotNo FTP file didn't exist or empty：{ftpPath}");
                    }
                }

                // Step 4.1: 檢查 TXT YIELD RECORD 是否已存在
                //測試時先檢查 ITTEST目錄
                //var yieldSetting = ftpSettings.FirstOrDefault(x => x.DeviceNo == "K_DIRNO_T_EMP_FTP_TXTYIELD");
                string yieldDeviceNo = request.Environment == "Production"
                    ? "K_DIRNO_T_EMP_FTP_TXTYIELD"
                    : "IT_TEST_T_EMP_FTP_TXTYIELD";
                //var yieldSetting = ftpSettings.FirstOrDefault(x => x.DeviceNo == "IT_TEST_T_EMP_FTP_TXTYIELD");
                var yieldSetting = ftpSettings.FirstOrDefault(x => x.DeviceNo == yieldDeviceNo);

                if (yieldSetting == null)
                    //return ApiReturn<string>.Failure("無法取得 TXT YIELD RECORD 測試目錄 FTP 設定");
                    return ApiReturn<string>.Failure("Cannot get TXT YIELD RECORD test directory FTP configure");

                var yieldFilePath = FtpPathResolver.ResolvePath(yieldSetting, request.LotNo, request.ProductNo).TrimEnd('/') + "/" + request.LotNo + ".txt";
                //var yieldExists = await FtpService.DirectoryExistsAsync(yieldSetting.FtpSite, yieldFilePath, yieldSetting.PathAccount, yieldSetting.PathPassword);
                var yieldExists = await FtpService.FileExistsAsync(yieldSetting.FtpSite, yieldFilePath, yieldSetting.PathAccount, yieldSetting.PathPassword);

                if (yieldExists)
                    //return ApiReturn<string>.Failure("TXT YIELD RECORD 檔案已存在，禁止覆蓋：" + yieldFilePath);
                    return ApiReturn<string>.Failure("TXT YIELD RECORD file already exist,not allow overwrite：" + yieldFilePath);

                // Step 5: 建立暫存資料夾結構 C:\TEMP\{LOTNO}\...

                foreach (var folder in folders)
                {
                    var fullPath = Path.Combine(tempRoot, folder);
                    if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                }

                // Step 6: 下載母批檔案（FTP）至暫存資料夾
                if (request.Emapping == "Y")
                {
                    var setting = ftpSettings.FirstOrDefault(x => x.DeviceNo == "H_DIRNO_T_EMP_MOUNT");
                    if (setting == null)
                        //return ApiReturn<string>.Failure("無法取得 H_DIRNO_T_EMP_MOUNT 的 FTP 設定");
                        return ApiReturn<string>.Failure("Cannot get  H_DIRNO_T_EMP_MOUNT FTP configure");

                    var mountCode = GetMountCode(mLotNo);
                    var mapFolder = "MAP" + mLotNo.Substring(2, 5);
                    var baseFtpPath = setting.FilePath.TrimEnd('/') + mountCode + "/" + mapFolder + "/BACKUP/";
                    var sourceFolders = new Dictionary<string, string>
                        {
                            {"AOIT", "AOIT"},
                            {"AOIB", "AOIB"},
                            {"TEST", "TEST"},
                            {"MERGE", "AOIB"}
                        };
                    foreach (var tileId in request.TileID)
                    {
                        foreach (var folder in folders)
                        {
                            var ftpPath = baseFtpPath + sourceFolders[folder] + "/" + mLotNo + "/" + tileId + ".txt";
                            var fileBytes = await FtpService.DownloadFileAsync(setting.FtpSite, ftpPath, setting.PathAccount, setting.PathPassword);
                            var dstPath = Path.Combine(tempRoot, folder, tileId + ".txt");
                            await File.WriteAllBytesAsync(dstPath, fileBytes);
                        }
                    }
                }
                else if (request.Emapping == "N")
                {
                    var mergeSetting = ftpSettings.FirstOrDefault(x => x.DeviceNo == "K_DIRNO_T_EMP_FTP_MERGE");
                    if (mergeSetting == null)
                        //return ApiReturn<string>.Failure("無法取得 FTP MERGE 設定");
                        return ApiReturn<string>.Failure("Cannot get FTP MERGE configure");

                    foreach (var tileId in request.TileID)
                    {
                        var ftpPath = FtpPathResolver.ResolvePath(mergeSetting, mLotNo).TrimEnd('/') + "/" + tileId + ".txt";
                        var fileBytes = await FtpService.DownloadFileAsync(mergeSetting.FtpSite, ftpPath, mergeSetting.PathAccount, mergeSetting.PathPassword);
                        if (fileBytes == null || fileBytes.Length == 0)
                            //return ApiReturn<string>.Failure($"FTP 檔案不存在或為空：{ftpPath}");
                            return ApiReturn<string>.Failure($"FTP file didn't exist or empty：{ftpPath}");

                        foreach (var folder in folders)
                        {
                            var dstPath = Path.Combine(tempRoot, folder, tileId + ".txt");
                            await File.WriteAllBytesAsync(dstPath, fileBytes);
                        }
                    }
                }
                else
                {
                    //return ApiReturn<string>.Failure("Emapping 欄位格式錯誤，請傳入 'Y' 或 'N'");
                    return ApiReturn<string>.Failure("Emapping culomn error，please write in  'Y' or 'N'");
                }

                // Step 7: TODO - 上傳回收批資料至 FTP 目錄

                foreach (var folder in folders)
                {
                    //測試時，上傳到ITTEST目錄
                    //var setting = ftpSettings.FirstOrDefault(x =>
                    //    folder == "AOIT" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_TOP" ||
                    //    folder == "AOIB" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_BOT" ||
                    //    folder == "TEST" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_TEST" ||
                    //    folder == "MERGE" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_MERGE");
                    //var setting = ftpSettings.FirstOrDefault(x =>
                    //   folder == "AOIT" && x.DeviceNo == "IT_TEST_T_EMP_FTP_TOP" ||
                    //   folder == "AOIB" && x.DeviceNo == "IT_TEST_T_EMP_FTP_BOT" ||
                    //   folder == "TEST" && x.DeviceNo == "IT_TEST_T_EMP_FTP_TEST" ||
                    //   folder == "MERGE" && x.DeviceNo == "IT_TEST_T_EMP_FTP_MERGE");
                    FtpPathSetting? setting;
                    if (request.Environment == "Production")
                    {
                        setting = ftpSettings.FirstOrDefault(x =>
                            folder == "AOIT" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_TOP" ||
                            folder == "AOIB" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_BOT" ||
                            folder == "TEST" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_TEST" ||
                            folder == "MERGE" && x.DeviceNo == "K_DIRNO_T_EMP_FTP_MERGE");
                    }
                    else 
                    {
                        setting = ftpSettings.FirstOrDefault(x =>
                          folder == "AOIT" && x.DeviceNo == "IT_TEST_T_EMP_FTP_TOP" ||
                          folder == "AOIB" && x.DeviceNo == "IT_TEST_T_EMP_FTP_BOT" ||
                          folder == "TEST" && x.DeviceNo == "IT_TEST_T_EMP_FTP_TEST" ||
                          folder == "MERGE" && x.DeviceNo == "IT_TEST_T_EMP_FTP_MERGE");
                    }

                    if (setting == null)
                        //return ApiReturn<string>.Failure($"找不到上傳目的 FTP 設定：{folder}");
                        return ApiReturn<string>.Failure($"Cannot fine upload FTP configure：{folder}");

                    var baseFtpPath = FtpPathResolver.ResolvePath(setting, lotNo).TrimEnd('/') + "/";
                    // Step 7.0.1: 若 FTP 回收批目錄不存在，則先建立
                    var createPath = baseFtpPath; // 通常是 /.../{LOTNO}/
                    bool exists = await FtpService.DirectoryExistsAsync(setting.FtpSite, createPath, setting.PathAccount, setting.PathPassword);
                    if (!exists)
                    {
                        await FtpService.CreateDirectoryAsync(setting.FtpSite, createPath, setting.PathAccount, setting.PathPassword);
                    }

                    foreach (var tileId in request.TileID)
                    {
                        var filePath = Path.Combine(tempRoot, folder, tileId + ".txt");
                        if (!File.Exists(filePath))
                            //return ApiReturn<string>.Failure($"暫存檔案遺失：{filePath}");
                            return ApiReturn<string>.Failure($"Temp file Lost：{filePath}");

                        var content = await File.ReadAllBytesAsync(filePath);
                        var ftpFullPath = baseFtpPath + tileId + ".txt";
                        await FtpService.UploadFileAsync(setting.FtpSite, ftpFullPath, setting.PathAccount, setting.PathPassword, content);
                    }
                }

                // Step 7.1: 上傳 TXT YIELD RECORD 檔案
                {


                    var txtSourcePath = FtpPathResolver.ResolvePath(yieldSetting, request.LotNo, request.ProductNo).TrimEnd('/') + "/" + mLotNo + ".txt";
                    var txtTargetPath = FtpPathResolver.ResolvePath(yieldSetting, request.LotNo, request.ProductNo).TrimEnd('/') + "/" + lotNo + ".txt";

                    var motherTxtBytes = await FtpService.DownloadFileAsync(yieldSetting.FtpSite, txtSourcePath, yieldSetting.PathAccount, yieldSetting.PathPassword);
                    if (motherTxtBytes == null || motherTxtBytes.Length == 0)
                        //return ApiReturn<string>.Failure("無法下載母批 TXT YIELD 檔案：" + txtSourcePath);
                        return ApiReturn<string>.Failure("Cannot Download Mother Lotno TXT YIELD File：" + txtSourcePath);

                    var originalMotherPath = Path.Combine("C:\\TEMP", lotNo, "mother_" + mLotNo + ".txt");
                    await File.WriteAllBytesAsync(originalMotherPath, motherTxtBytes);

                    //var lines = System.Text.Encoding.UTF8.GetString(motherTxtBytes)
                    //    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    //    .ToList();

                    var lines = (await File.ReadAllLinesAsync(originalMotherPath)).ToList();

                    if (lines.Count < 4)
                        //return ApiReturn<string>.Failure("母批 TXT 格式錯誤，行數不足：" + txtSourcePath);
                        return ApiReturn<string>.Failure("Mother LotNo txt file error，row count too low：" + txtSourcePath);

                    var tileSet = new HashSet<string>(request.TileID);
                    var fixedLines = lines.Take(3).ToList();
                    fixedLines.AddRange(lines.Skip(3).Where(line => tileSet.Contains(line.Split(',')[1]?.Trim())));

                    var localFilteredPath = Path.Combine("C:\\TEMP", lotNo, lotNo + ".txt");
                    await File.WriteAllLinesAsync(localFilteredPath, fixedLines);
                    var newTxtBytes = await File.ReadAllBytesAsync(localFilteredPath);
                    await FtpService.UploadFileAsync(yieldSetting.FtpSite, txtTargetPath, yieldSetting.PathAccount, yieldSetting.PathPassword, newTxtBytes);
                    //var newTxtBytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", fixedLines));
                    //await FtpService.UploadFileAsync(yieldSetting.FtpSite, txtTargetPath, yieldSetting.PathAccount, yieldSetting.PathPassword, newTxtBytes);
                }


                // Step 8: 寫入 ARGOAPILOTRECYCLEFILERECORD（每筆 Tile）
                var now = DateTime.Now;
                //foreach (var tileId in request.TileID)
                //{
                //    var insertSql = @"INSERT INTO ARGOAPILOTRECYCLEFILERECORD 
                //    (LOTNO, M_LOTNO, TITLENO, SCANDATE, COPYSTATUS)
                //    VALUES (:LotNo, :MLotNo, :TileId, :ScanDate, :CopyStatus)";

                //    await repoCim.ExecuteAsync(insertSql, new
                //    {
                //        LotNo = lotNo,
                //        MLotNo = mLotNo,
                //        TileId = tileId,
                //        ScanDate = now,
                //        CopyStatus = "Y"
                //    });
                //}

                using (var conn = repoCim.CreateOpenConnection())
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var tileId in request.TileID)
                        {
                            var sql = @"INSERT INTO ARGOAPILOTRECYCLEFILERECORD 
                        (LOTNO, M_LOTNO, TITLENO, SCANDATE, COPYSTATUS)
                        VALUES (:LotNo, :MLotNo, :TileId, :ScanDate, :CopyStatus)";

                            await conn.ExecuteAsync(sql, new
                            {
                                LotNo = lotNo,
                                MLotNo = mLotNo,
                                TileId = tileId,
                                ScanDate = DateTime.Now,
                                CopyStatus = "Y"
                            }, transaction: tran);
                        }

                        tran.Commit();
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        //return ApiReturn<string>.Failure("Step 8 寫入資料庫失敗，已回滾交易：" + ex.Message);
                        return ApiReturn<string>.Failure("Step 8 Insert DB Fail，Already Rollback Transaction：" + ex.Message);
                    }
                }

                //return ApiReturn<string>.Success("搬檔成功，所有 Tile 檔案已上傳並寫入資料表", lotNo);
                return ApiReturn<string>.Success("Copy File Success", lotNo);
            }
            catch (Exception ex)
            {
                return ApiReturn<string>.Failure("例外錯誤：" + ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true); // true = recursive
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"[清除暫存資料夾失敗] {cleanupEx.Message}");
                }
            }

            //return ApiReturn<string>.Success("所有原始檔案下載完成 (FTP)，等待上傳", lotNo);
            //return ApiReturn<string>.Success("搬檔成功，所有 Tile 檔案已上傳至回收批 FTP 路徑", lotNo);
        }

       

        private string GetMountCode(string lotNo)
        {
            char c = lotNo[6];
            return c switch
            {
                >= '0' and <= '9' => "0" + c,
                'A' => "10",
                'B' => "11",
                'C' => "12",
                _ => throw new Exception("無效的 LOTNO 第七碼")
            };
        }
    }
}
