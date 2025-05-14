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

                //var ruleList = await repo.QueryAsync<ARGOCIMDEVICEFILERULE>(
                //    "SELECT * FROM ARGOCIMDEVICEFILERULE WHERE DEVICENO IN :deviceIds AND ENABLED = 'Y'",
                //    new { deviceIds });
                var ruleList = await repo.QueryAsync<ARGOCIMDEVICEFILERULE>(
                @"SELECT * FROM ARGOCIMDEVICEFILERULE 
                  WHERE ENABLED = 'Y'
                    AND (DEVICENO IN :deviceIds OR DEVICENO = 'None')
                    AND DEVICEGROUPNO IN :deviceGroupNos",
                new { deviceIds, deviceGroupNos });

                int pass = 0, open = 0, shorts = 0, fourW = 0;

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

                        //if (!ruleMap.ContainsKey(rule.RuleType))
                        //{
                        //    var entry = JsonSerializer.Deserialize<RuleEntry>(rule.RuleJson);
                        //    ruleMap[rule.RuleType] = entry;
                        //}
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
                    //var rule = ruleList.FirstOrDefault(r => r.DeviceNo == device);

                    //if (setting == null || rule == null || string.IsNullOrWhiteSpace(rule.RuleJson))
                    //    continue;

                    //var ruleMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, RuleEntry>>(rule.RuleJson);
                    //var ruleMap = ruleList
                    //    .Where(r => !string.IsNullOrWhiteSpace(r.RuleJson) && !string.IsNullOrWhiteSpace(r.RuleType))
                    //    .ToDictionary(
                    //        r => r.RuleType,
                    //        r => JsonSerializer.Deserialize<RuleEntry>(r.RuleJson)
                    //    );
                    //var dir = Path.Combine(setting.FilePath, request.Lotno);

                    var dir = ResolveFilePath(setting.FilePath, device, request.Programename, request.Lotno);
                    //var shareRoot = setting.FilePath; // 必須為登入用的根目錄，不可加 LOTNO 子層
                    string shareRoot = ExtractShareRoot(dir);// 必須為登入用的根目錄，不可加 LOTNO 子層
                    //var shareRoot = dir; 
                    _logger.LogInformation($"[DefectCount] Device={device} 嘗試登入共享目錄: {shareRoot} 使用帳號: {setting.PathAccount}");

                    try
                    {
						NetworkShareManager.EnsureConnected(shareRoot, setting.PathAccount, setting.PathPassword);

						if (!Directory.Exists(dir))
                            {
                                _logger.LogWarning($"[DefectCount] 目錄不存在：{dir}");
                                continue;
                            }

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


                    //_logger.LogInformation($"[DefectCount] Device={device} 目錄：{dir}");
                    //if (!Directory.Exists(dir))
                    //{
                    //    _logger.LogWarning($"[DefectCount] 目錄不存在：{dir}");
                    //    continue; 
                    //}

                    //foreach (var file in Directory.GetFiles(dir, "*.txt"))
                    //{
                    //    _logger.LogInformation($"[DefectCount] 處理檔案：{file}");
                    //    try
                    //    {
                    //        var lines = File.ReadAllLines(file);
                    //        var values = DefectFileParser.ParseFile(lines, ruleMap);

                    //        values.TryGetValue("PASS", out int p);
                    //        values.TryGetValue("OPEN", out int o);
                    //        values.TryGetValue("SHORT", out int s);
                    //        values.TryGetValue("HVSHORT", out int hs);
                    //        values.TryGetValue("OPENSHORT", out int os);
                    //        values.TryGetValue("OPENHVSHORT", out int ohs);
                    //        values.TryGetValue("FOURLINEERROR", out int f);

                    //        pass += p;
                    //        open += o + os + ohs;
                    //        shorts += s + hs;
                    //        fourW += f;
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        _logger.LogError($"[DefectCount] 檔案處理錯誤：{file}, 錯誤: {ex.Message}");
                    //    }
                    //    //var lines = File.ReadAllLines(file);
                    //    //var values = DefectFileParser.ParseFile(lines, ruleMap);

                    //    //values.TryGetValue("PASS", out int p);
                    //    //values.TryGetValue("OPEN", out int o);
                    //    //values.TryGetValue("SHORT", out int s);
                    //    //values.TryGetValue("HVSHORT", out int hs);
                    //    //values.TryGetValue("OPENSHORT", out int os);
                    //    //values.TryGetValue("OPENHVSHORT", out int ohs);
                    //    //values.TryGetValue("FOURLINEERROR", out int f);

                    //    //pass += p;
                    //    //open += o + os + ohs;
                    //    //shorts += s + hs;
                    //    //fourW += f;
                    //}
                }
                var response = new DefectCountResponse
                {
                    PASS = pass,
                    OPEN = open,
                    SHORT = shorts,
                    _4W = fourW
                };
                _logger.LogInformation($"[DefectCount] 計算完成: PASS={pass}, OPEN={open}, SHORT={shorts}, 4W={fourW}");
                return ApiReturn<DefectCountResponse>.Success("計算完成", response);
                //return ApiReturn<DefectCountResponse>.Success("計算完成", new DefectCountResponse
                //{
                //    PASS = pass,
                //    OPEN = open,
                //    SHORT = shorts,
                //    _4W = fourW
                //});
            }
            catch (Exception ex)
            {
                _logger.LogError($"[DefectCount] 發生例外錯誤: {ex.Message}");
                return ApiReturn<DefectCountResponse>.Failure("錯誤: " + ex.Message);
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
