namespace Core.Entities.DefectCount
{
    public class RuleEntry
    {
        public int SourceLine { get; set; }
        public string Delimiter { get; set; } = "=";
    }
}
