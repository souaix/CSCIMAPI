namespace Core.Entities.CheckLimit
{
    public class CheckLimitResponse
    {
        public string Status { get; set; } // MESPD002 / MESPD003 等
        public string Message { get; set; } // 設備參數正常 / 異常
        public List<CheckLimitDetailDto> Details { get; set; } = new();
    }
}
