namespace Core.Entities.CheckLimit
{
    public class CheckLimitDetailDto
    {
        public string DeviceId { get; set; }
        public string Opno { get; set; }
        public string Lotno { get; set; }
        public string CreateDate { get; set; }
        public string ColName { get; set; }
        public string ColCname { get; set; }
        public double Value { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }
}
