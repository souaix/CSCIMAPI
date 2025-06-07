namespace Core.Entities.RecycleLotCopy
{
    public class RecycleLotCopyRequest
    {
        public string Environment { get; set; }
        public string Action { get; set; }
        public string ProductNo { get; set; }
        public string LotNo { get; set; }       // 回收批號
        public string M_LotNo { get; set; }     // 原始母批號
        public string Emapping { get; set; } // 是否做過 Emapping（"Y" 或 "N"）
        public List<string> TileID { get; set; } = new();
    }
}
