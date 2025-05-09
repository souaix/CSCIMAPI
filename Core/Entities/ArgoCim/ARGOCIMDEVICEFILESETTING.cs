namespace Core.Entities.ArgoCim
{
	public class ARGOCIMDEVICEFILESETTING
	{
		// 設備群組編號
		public string DeviceGroupNo { get; set; }

		// 設備編號
		public string DeviceNo { get; set; }

		// 檔案路徑
		public string FilePath { get; set; }

		// 檔案名稱
		public string FileName { get; set; }

		// 檔案副檔名
		public string FileExt { get; set; }

		// FTP帳號（或檔案存取帳號）
		public string PathAccount { get; set; }

		// FTP密碼（或路徑存取密碼）
		public string PathPassword { get; set; }

		// 建立時間
		public DateTime? CreateDate { get; set; }

		// 建立者
		public string Creator { get; set; }

		// 修改時間
		public DateTime? UpdateDate { get; set; }

		// 修改者
		public string Updater { get; set; }

		// 角色編號（可作為權限區分用途）
		public string RoleNo { get; set; }
	}
}
