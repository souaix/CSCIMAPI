namespace Core.Entities.ArgoCim
{
	public class ARGOCIMDEVICEFILERULE
	{
		// 設備群組編號
		public string DeviceGroupNo { get; set; }

		// 設備編號
		public string DeviceNo { get; set; }

		// 規則類型
		public string RuleType { get; set; }

		// 規則定義（JSON格式）
		public string RuleJson { get; set; }

		// 是否啟用（'Y' 或 'N'）
		public string Enabled { get; set; }

		// 建立時間
		public DateTime? CreateTime { get; set; }

		// 建立者
		public string Creator { get; set; }

		// 修改時間
		public DateTime? UpdateTime { get; set; }

		// 修改者
		public string Updater { get; set; }

		// 規則編號
		public string RuleNo { get; set; }
	}
}
