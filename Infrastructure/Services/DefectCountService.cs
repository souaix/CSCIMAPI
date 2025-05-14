using Core.Entities.ArgoCim;
using Core.Entities.DefectCount;
using Core.Entities.Public;
using Core.Interfaces;
using Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
namespace Infrastructure.Services
{
    public class DefectCountService : IDefectCountService
    {
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly ILogger<DefectCountService> _logger;

        public DefectCountService(IRepositoryFactory repositoryFactory, ILogger<DefectCountService> logger)
        {
            _repositoryFactory = repositoryFactory;
            _logger = logger;
        }

        public async Task<ApiReturn<DefectCountResponse>> CountDefectsAsync(DefectCountRequest request)
        {
            try
            {
                _logger.LogInformation($"[DefectCount] 開始處理 Request: Env={request.Environment}, PN={request.Programename}, Lot={request.Lotno}, OpNo={request.OpNo}");
                //var repo = _repositoryFactory.CreateRepository(request.Environment);
                var (oracleRepo, repo, _) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);

                var (opnos, deviceIds) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repo, request.OpNo, "");

                _logger.LogInformation($"[DefectCount] 解析出 Steps: {string.Join(", ", opnos)}");
                _logger.LogInformation($"[DefectCount] 解析出 Devices: {string.Join(", ", deviceIds)}");

                var fileSettingList = await repo.QueryAsync<ARGOCIMDEVICEFILESETTING>(
                    "SELECT * FROM ARGOCIMDEVICEFILESETTING WHERE DEVICENO IN :deviceIds",
                    new { deviceIds });

                var deviceGroupNos = fileSettingList
                .Where(s => deviceIds.Contains(s.DeviceNo))
                .Select(s => s.DeviceGroupNo)
                .Distinct()
                .ToList();

                var ruleList = await repo.QueryAsync<ARGOCIMDEVICEFILERULE>(
                @"SELECT * FROM ARGOCIMDEVICEFILERULE 
                  WHERE ENABLED = 'Y'
                    AND (DEVICENO IN :deviceIds OR DEVICENO = 'None')
                    AND DEVICEGROUPNO IN :deviceGroupNos",
                new { deviceIds, deviceGroupNos });

                int pass = 0, open = 0, shorts = 0, fourW = 0;
                bool anyDirFound = false;
                foreach (var device in deviceIds)
                {
                    var setting = fileSettingList.FirstOrDefault(f => f.DeviceNo == device);
                    if (setting == null)
                    {
                        _logger.LogWarning($"[DefectCount] 找不到 Device {device} 的設定 (ARGOCIMDEVICEFILESETTING)");
                        continue; 
                        
                    }

                    var group = setting.DeviceGroupNo;
                    // 優先找設備專屬規則，否則 fallback 用 DEVICEGROUP + DEVICENO=None
                    var deviceRules = ruleList.Where(r => r.DeviceNo == device).ToList();
                    if (!deviceRules.Any())
                    {
                        deviceRules = ruleList
                            .Where(r => r.DeviceGroupNo == group && r.DeviceNo == "None")
                            .ToList();
                    }
                    var ruleMap = new Dictionary<string, RuleEntry>();  
                    foreach (var rule in deviceRules)
                    {
                        if (string.IsNullOrWhiteSpace(rule.RuleType) || string.IsNullOrWhiteSpace(rule.RuleJson))
                            continue;

          
                        try
                        {
                            var entry = JsonSerializer.Deserialize<RuleEntry>(rule.RuleJson);
                            ruleMap[rule.RuleType] = entry;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[DefectCount] 解析 JSON 規則錯誤 Device={device}, Type={rule.RuleType}, Error={ex.Message}");
                        }
                    }
                    

                    var dir = ResolveFilePath(setting.FilePath, device, request.Programename, request.Lotno);
                    //var shareRoot = setting.FilePath; // 必須為登入用的根目錄，不可加 LOTNO 子層
                    string shareRoot = ExtractShareRoot(dir);// 必須為登入用的根目錄，不可加 LOTNO 子層
                    //var shareRoot = dir; 
                    _logger.LogInformation($"[DefectCount] Device={device} 嘗試登入共享目錄: {shareRoot} 使用帳號: {setting.PathAccount}");

                    try
                    {
//#if DEBUG
//						// 不做掛載，使用現有 Windows 憑證 session
//						_logger.LogInformation($"[DebugMode] 跳過 NetworkShareManager 掛載，假設已登入共享：{shareRoot}");
//#else
//                        // Production 模式才進行登入掛載
//                        NetworkShareManager.EnsureConnected(shareRoot, setting.PathAccount, setting.PathPassword);
//#endif


                        NetworkShareManager.EnsureConnected(shareRoot, setting.PathAccount, setting.PathPassword);




                        //NetworkShareManager.EnsureConnected(shareRoot, setting.PathAccount, setting.PathPassword);

                        if (!Directory.Exists(dir))
                            {
                                _logger.LogWarning($"[DefectCount] 目錄不存在：{dir}");
                                continue;
                            }

                        anyDirFound = true; // 至少有一個成功找到

                        foreach (var file in Directory.GetFiles(dir, "*.txt"))
                        {
                            _logger.LogInformation($"[DefectCount] 處理檔案：{file}");
                            try
                            {
                                var lines = File.ReadAllLines(file);
                                var values = DefectFileParser.ParseFile(lines, ruleMap);

                                values.TryGetValue("PASS", out int p);
                                values.TryGetValue("OPEN", out int o);
                                values.TryGetValue("SHORT", out int s);
                                values.TryGetValue("HVSHORT", out int hs);
                                values.TryGetValue("OPENSHORT", out int os);
                                values.TryGetValue("OPENHVSHORT", out int ohs);
                                values.TryGetValue("FOURLINEERROR", out int f);

                                pass += p;
                                open += o + os + ohs;
                                shorts += s + hs;
                                fourW += f;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"[DefectCount] 檔案處理錯誤：{file}, 錯誤: {ex.Message}");
                            }
                        }
                       
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[DefectCount] 登入共享目錄失敗 Device={device}, Path={shareRoot}, 錯誤: {ex.Message}");
                        continue;
                    }


                    
                }

                if (!anyDirFound)
                {
                    return ApiReturn<DefectCountResponse>.Failure("All device folders not found");
                }

                var response = new DefectCountResponse
                {
                    PASS = pass,
                    OPEN = open,
                    SHORT = shorts,
                    _4W = fourW
                };
                _logger.LogInformation($"[DefectCount] 計算完成: PASS={pass}, OPEN={open}, SHORT={shorts}, 4W={fourW}");
                return ApiReturn<DefectCountResponse>.Success("Calculation completed", response);
        
            }
            catch (Exception ex)
            {
                _logger.LogError($"[DefectCount] 發生例外錯誤: {ex.Message}");
                return ApiReturn<DefectCountResponse>.Failure("Error: " + ex.Message);
            }
        }

        private string ResolveFilePath(string template, string deviceNo, string pn, string lotno)
        {
            return template
                .Replace("{DEVICENO}", deviceNo)
                .Replace("{PN}", pn)
                .Replace("{LOTNO}", lotno);
        }

        private string ExtractShareRoot(string fullPath)
        {
            // fullPath 範例：\\10.10.22.80\TNSF-Backend\TNSF-INK\Internal...
            if (!fullPath.StartsWith(@"\\"))
                throw new ArgumentException("不是有效的 UNC 路徑");

            // 去掉前面的兩個反斜線，並拆分剩下部分
            var parts = fullPath.Substring(2).Split('\\');

            if (parts.Length < 2)
                throw new ArgumentException("UNC 路徑結構錯誤，需至少有主機與共享資料夾");

            return $@"\\{parts[0]}\{parts[1]}"; // 組回根目錄路徑
        }

    }
}
