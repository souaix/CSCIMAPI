using Core.Entities.LaserMarking;
using Core.Entities.Public;
using Core.Utilities;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Infrastructure.Utilities
{
    public static class FormulaResolver
    {
        public static HashSet<string> ExtractUsedVariables(string formula)
        {
            var matches = Regex.Matches(formula, @"@([A-Za-z][A-Za-z0-9_]*)");
            return matches.Select(m => m.Groups[1].Value).ToHashSet();
        }

        public static Dictionary<string, string> BuildVariablesFromUsage(HashSet<string> usedVars, Config config, LaserMarkingRequest request)
        {
            var dict = new Dictionary<string, string>();
            var mapping = DateCodeHelper.BuildFullDateCodeMapping(config);

            foreach (var varName in usedVars)
            {
                switch (varName)
                {
                    case "TilePrefix":
                        // ✅ 改為 true：包含 DateCodeMapping（如 YC → H）
                        dict[$"@{varName}"] = DateCodeHelper.BuildTileIdPrefixFromConfig(config, request.LotNo, true);
                        break;
                    case "Today":
                        dict[$"@{varName}"] = DateTime.Now.ToString("yyMMdd");
                        break;
                    case "Customer":
                        dict[$"@{varName}"] = config.Customer ?? "";
                        break;
                    case "YY":
                    case "MM":
                    case "DD":
                    case "WW":
                    case "YC":
                    case "MC":
                    case "DC":
                        if (mapping.TryGetValue(varName, out var val))
                            dict[$"@{varName}"] = val;
                        break;
                    default:
                        dict[$"@{varName}"] = "";
                        break;
                }
            }

            return dict;
        }

        public static string ConvertFormulaToSql(string formula, Dictionary<string, string> context)
        {
            string sql = formula;

            // StartsWith(x + y) → LIKE 'xy%'
            sql = Regex.Replace(sql, @"(\w+)\.StartsWith\(([^\)]+)\)", match =>
            {
                string field = match.Groups[1].Value;
                string expression = match.Groups[2].Value;

                var tokens = expression.Split('+')
                                       .Select(token => token.Trim())
                                       .Select(token => context.TryGetValue(token, out var val) ? val : token.Trim('"'))
                                       .ToList();

                string combined = string.Join("", tokens);
                return $"{field} LIKE '{combined}%'";
            });

            // Field == 'Value' → Field = 'Value'
            sql = Regex.Replace(sql, @"(\w+)\s*==\s*'([^']+)'", "$1 = '$2'");

            // Field == @Variable → Field = 'Value'
            sql = Regex.Replace(sql, @"(\w+)\s*==\s*(@[A-Za-z0-9_]+)", match =>
            {
                string field = match.Groups[1].Value;
                string var = match.Groups[2].Value;
                return $"{field} = '{(context.TryGetValue(var, out var val) ? val : "")}'";
            });

            // Length → CHAR_LENGTH
            sql = sql.Replace("TILEID.Length", "CHAR_LENGTH(TILEID)");

            // CompareTo(x) >= 0 → >= x
            sql = Regex.Replace(sql, @"CreateDate\.CompareTo\((.*?)\)\s*>=\s*0", match =>
            {
                string val = match.Groups[1].Value;
                foreach (var kv in context)
                    val = val.Replace(kv.Key, kv.Value);
                return $"CreateDate >= '{val}'";
            });

            foreach (var kv in context)
                sql = sql.Replace(kv.Key, kv.Value);

            sql = sql.Replace("&&", "AND").Replace("||", "OR");

            return "WHERE " + sql + " ORDER BY LastSN DESC";
        }
    }

    internal static class DateCodeHelper
    {
        public static Dictionary<string, string> BuildFullDateCodeMapping(Config config)
        {
            var mapping = new Dictionary<string, string>();

            var yearMap = BuildCodeMapping(config.Year_Code);
            if (yearMap.Any()) mapping["YC"] = DateCodeMapping.Convert("YC", yearMap);

            var monthMap = BuildCodeMapping(config.Month_Code);
            if (monthMap.Any()) mapping["MC"] = DateCodeMapping.Convert("MC", monthMap);

            var dayMap = BuildCodeMapping(config.Day_Code);
            if (dayMap.Any()) mapping["DC"] = DateCodeMapping.Convert("DC", dayMap);

            mapping["YY"] = DateTime.Now.ToString("yy");
            mapping["MM"] = DateTime.Now.ToString("MM");
            mapping["DD"] = DateTime.Now.ToString("dd");
            mapping["WW"] = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstDay, DayOfWeek.Sunday).ToString("D2");

            return mapping;
        }

        public static string BuildTileIdPrefixFromConfig(Config config, string lotNo, bool useDateCodeMapping)
        {
            var tileIdParts = config.TileId?.Split(',').Select(p => p.Trim()).ToList() ?? new List<string>();
            var prefixBuilder = new List<string>();
            var mapping = BuildFullDateCodeMapping(config);

            foreach (var part in tileIdParts)
            {
                if (part == "SN" || part == "GSC") break;

                if (useDateCodeMapping && mapping.TryGetValue(part, out var val))
                    prefixBuilder.Add(val);
                else
                    prefixBuilder.Add(StringCodeMapping.Convert(part, lotNo));
            }

            return string.Join("", prefixBuilder);
        }

        private static Dictionary<string, string> BuildCodeMapping(string raw)
        {
            return (raw ?? "")
                .Split(',')
                .Select(s => s.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        }
    }
}
