namespace Core.Entities.Recipe2DCodeGenerator
{
	public class Recipe2DCodeRequest
	{
		public string Environment { get; set; }
		public string Action { get; set; }
		public int Length { get; set; } // 300 or 500
		public string Step { get; set; }
		public string Pn { get; set; }
		public string Lotno { get; set; }
		public string Gbom { get; set; }
		public string Sequence { get; set; }
		public string Recipe { get; set; }
	}

}
