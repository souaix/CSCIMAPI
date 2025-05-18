namespace Core.Entities.LaserMarkingFrontend
{
	public class LotNo
	{
		public string? TileID { get; set; }                  // Tile ID
		public string? MachineID { get; set; }               // 機台 ID
		public string? MarkDate { get; set; }                // 標記日期
		public string? MarkTime { get; set; }                // 標記時間
		public string? ReworkDate { get; set; }              // 重工日期
		public string? ReworkTime { get; set; }              // 重工時間
		public string? TileText01 { get; set; }              // Tile 額外資料
		public string? ChangeMachineID { get; set; }         // 異動機台 ID
		public string? ChangeMarkDate { get; set; }          // 異動標記日期
		public string? ChangeMarkTime { get; set; }          // 異動標記時間
		public string? ChangeReworkDate { get; set; }        // 異動重工日期
		public string? ChangeReworkTime { get; set; }        // 異動重工時間
		public string? ChangeTileText01 { get; set; }        // 異動 Tile 額外資料
	}
}
