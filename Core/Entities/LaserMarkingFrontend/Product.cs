using System.ComponentModel.DataAnnotations;

namespace Core.Entities.LaserMarkingFrontend
{
	public class Product
	{
		[Key]
		public string? LotNO { get; set; }              // 批號
		public string? Customer { get; set; }           // 客戶
		public string? ProductName { get; set; }        // 產品名稱
		public string? TileID { get; set; }             // Tile ID
		public string? LastSN { get; set; }             // 最後序號
		public string? Quantity { get; set; }           // 數量
		public string? ManualVLM { get; set; }          // 手動體積數據
		public string? ManualCount { get; set; }        // 手動計數
		public string? ManualStart { get; set; }        // 手動起始
		public string? ManualEnd { get; set; }          // 手動結束
		public string? CreateDate { get; set; }         // 建立日期 (格式可能為 yymmdd)
		public string? CreateTime { get; set; }         // 建立時間 (格式可能為 hhmmss)
		public string? NoteData { get; set; }           // 備註資料

	}
}
