namespace Core.Entities.DefectCount
{
    public class DefectCountRequest
    {
        public string Environment { get; set; }
        public string Action { get; set; }
        public string Programename { get; set; }
        public string Lotno { get; set; }
        public string StepCode { get; set; }
    }
}
