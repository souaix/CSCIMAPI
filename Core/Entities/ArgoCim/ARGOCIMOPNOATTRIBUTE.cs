namespace Core.Entities.ArgoCim
{
	public class ARGOCIMOPNOATTRIBUTE
	{
		// 作業編號（站點）
		public string Opno { get; set; }

		// 屬性名稱（如：QUERYMODE、DEVICEIDS、OPNOGROUP）
		public string Item { get; set; }

		// 屬性值（可能是逗號分隔的裝置清單）
		public string Value { get; set; }

		// 額外說明
		public string Description { get; set; }

		// 建立時間
		public DateTime? CreateDate { get; set; }

		// 建立者
		public string Creator { get; set; }

		// 修改時間
		public DateTime? UpdateDate { get; set; }

		// 修改者
		public string Updater { get; set; }
	}
}
