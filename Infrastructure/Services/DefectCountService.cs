using Core.Entities.ArgoCim;
using Core.Entities.DefectCount;
using Core.Entities.Public;
using Core.Interfaces;
using Infrastructure.Utilities;
using System.Text.Json;
namespace Infrastructure.Services
{
    public class DefectCountService : IDefectCountService
    {
        private readonly IRepositoryFactory _repositoryFactory;

        public DefectCountService(IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public async Task<ApiReturn<DefectCountResponse>> CountDefectsAsync(DefectCountRequest request)
        {
            try
            {
                //var repo = _repositoryFactory.CreateRepository(request.Environment);
                var (oracleRepo, repo, _) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);

                var (steps, deviceIds) = await OpnoQueryModelHelper.ResolveQueryModeAsync(repo, request.StepCode, "");

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
                    if (setting == null) continue;

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

                        if (!ruleMap.ContainsKey(rule.RuleType))
                        {
                            var entry = JsonSerializer.Deserialize<RuleEntry>(rule.RuleJson);
                            ruleMap[rule.RuleType] = entry;
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
                    if (!Directory.Exists(dir)) continue;

                    foreach (var file in Directory.GetFiles(dir, "*.txt"))
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
                }

                return ApiReturn<DefectCountResponse>.Success("計算完成", new DefectCountResponse
                {
                    PASS = pass,
                    OPEN = open,
                    SHORT = shorts,
                    _4W = fourW
                });
            }
            catch (Exception ex)
            {
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
    }
}
