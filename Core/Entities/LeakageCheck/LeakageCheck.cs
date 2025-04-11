namespace Core.Entities.LeakageCheck
{
	public class LeakageCheckRequest
	{
		public string Environment { get; set; }
		public string Action { get; set; }
		public string Opno { get; set; }
		public string Lotno { get; set; }
		public string Deviceid { get; set; }
		public string Deviceids { get; set; }
		public double Diff { get; set; }
	}

	public class LeakageAnomalyDto
	{
		public DateTime RECORDDATE { get; set; }
		public string TILEID { get; set; }
		public string V007 { get; set; }
		public string V008 { get; set; }
		public double DIFF_V008 { get; set; }

		// 以下欄位僅供除錯時使用，正常回傳只包含 TILEID, V008, DIFF_V008, RECORDDATE
		//public string NG_V008 { get; set; }
		//public string OK_V008 { get; set; }
		//public DateTime NG_RECORDDATE { get; set; }
		//public DateTime OK_RECORDDATE { get; set; }	
	}
	public class LeakageRawDataDto
	{
		public string TILEID { get; set; }
		public string V007 { get; set; }
		public string V008 { get; set; }
		public DateTime RECORDDATE { get; set; }
	}
}
