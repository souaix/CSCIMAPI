namespace Core.Entities.LotTileCheck
{

	public class LotTileCheckRequest
	{
		public string Environment { get; set; }
		public string Action { get; set; }
		public string LotNo { get; set; }
		public string Step { get; set; }

	}

	public class TileCheckResultDto
	{
		public string TileId { get; set; }
		public string LotNo { get; set; }		
		public string Reason { get; set; }  // "NG" / "MissingWork" / "MixLot"
		public string ResultList { get; set; }  // "BlackList" / "WhiteList"
		public DateTime? RecordDate { get; set; }
	}

	public class RuleCheckDefinition
	{
		public string Step { get; set; }
		public string DeviceIds { get; set; }
		public string EvalFormula { get; set; }
		public string Reason { get; set; }
		public int? DaysRange { get; set; }
	}

}
