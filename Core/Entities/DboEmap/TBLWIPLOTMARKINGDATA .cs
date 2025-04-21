
namespace Core.Entities.DboEmap
{
	public class TBLWIPLOTMARKINGDATA
	{
		public string LotNo { get; set; }          // 批號
		public string TileGroup { get; set; }      // Tile 群組
		public decimal TileGroup_Item { get; set; } // 群組內項次（NUMBER(38,0) 對應 decimal）
		public string TileId { get; set; }         // TileID 編號
		public DateTime? OracleDate { get; set; }  // Oracle 紀錄時間（可為 null）
		public DateTime? RecordDate { get; set; }  // 系統紀錄時間（可為 null）
	}
}
