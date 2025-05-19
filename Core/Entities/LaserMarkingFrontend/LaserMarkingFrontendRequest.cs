namespace Core.Entities.LaserMarkingFrontend
{
	public class LaserMarkingFrontendRequest
	{
		public string Environment { get; set; }
		public string Action { get; set; }
		public string LotNo { get; set; }
		public string EqNo { get; set; }
		public int Qty { get; set; }
		public string ProductNo { get; set; }
		public DateTime CheckoutTime { get; set; }
		
		
	}
}
