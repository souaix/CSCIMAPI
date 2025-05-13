namespace Core.Entities.LotTileCheck
{

	public class LotTileCheckRequest
	{
		public string Environment { get; set; }
		public string Action { get; set; }
		public string LotNo { get; set; }
		public string Opno { get; set; }  // 改成多個 Step
		public string DeviceId { get; set; }  // 改成多個 deviceid
		public int? DisableMissingWork { get; set; } // null or 1


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
		public string Opno { get; set; }
		public string DeviceIds { get; set; }
		public string EvalFormula { get; set; }
		public string Reason { get; set; }
		public int? DaysRange { get; set; }

		public string EnableNg { get; set; } // 'Y' or 'N'
		public string EnableMissingWork { get; set; }
		public string EnableMixLot { get; set; }
	}

	public class TileCheckLaserInkDto
	{
		public string TileId { get; set; }
		public string LotNo { get; set; }
		public DateTime? RecordDate { get; set; }

		public string TH_TileId { get; set; }
		public string TH_TileId_2 { get; set; }

		public string CS_TileId { get; set; }
		public string CS_TileId2 { get; set; }
		public string CS_TileId3 { get; set; }
		public string CS_TileId4 { get; set; }

		//public string Panel_Id1 { get; set; }
		//public string Panel_Id2 { get; set; }
		//public string Panel_Id3 { get; set; }
		//public string Panel_Id4 { get; set; }

		public string ResultList { get; set; }
		public string Reason { get; set; }
	}


}
