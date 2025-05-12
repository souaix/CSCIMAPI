using Core.Entities.DefectCount;

namespace Infrastructure.Utilities
{
    public static class DefectFileParser
    {
        public static Dictionary<string, int> ParseFile(string[] lines, Dictionary<string, RuleEntry> ruleMap)
        {
            var result = new Dictionary<string, int>();

            foreach (var kv in ruleMap)
            {
                var key = kv.Key;
                var rule = kv.Value;

                if (rule.SourceLine > lines.Length)
                    continue;

                string line = lines[rule.SourceLine - 1];
                var parts = line.Split(rule.Delimiter);

                if (parts.Length < 2)
                    continue;

                if (int.TryParse(parts[1].Trim(), out int value))
                    result[key] = value;
            }

            return result;
        }
    }
}
