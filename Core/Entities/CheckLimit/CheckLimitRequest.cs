namespace Core.Entities.CheckLimit
{
    public class CheckLimitRequest
    {
        public string Environment { get; set; }
        public string Action { get; set; }
        public string DeviceId { get; set; }
        public string Opno { get; set; }
        public string Lotno { get; set; }
    }
}
