namespace Core.Entities.YieldRecordData
{
	public class YieldRecordDataRequest
	{

		public string Environment { get; set; }
		public string Action { get; set; }
		public string ProductNo { get; set; }
		public string LotNo { get; set; }
	}

	public class YieldRecordDataDto
	{
		public string LotNo { get; set; }
		public string TileId { get; set; }
		public int GoodQty { get; set; }
		public int BadQty { get; set; }
		public int TotalQty { get; set; }
	}

	public class YieldRecordDataResult
	{
		public List<YieldRecordDataDto> Records { get; set; }

		public int GoodQtyTotal { get; set; }

		public int BadQtyTotal { get; set; }

		public int TotalQtyTotal { get; set; }
	}
}
