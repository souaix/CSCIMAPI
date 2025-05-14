using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace Infrastructure.Utilities
{
	public class NetworkShareAccesser : IDisposable
	{
		private readonly string _networkName;
		private readonly bool _disconnectOnDispose;

		public NetworkShareAccesser(string networkName, string user, string password, int retryCount = 3, int retryDelayMs = 1000, bool disconnectOnDispose = true)
		{
			_networkName = networkName;
			_disconnectOnDispose = disconnectOnDispose;

			var netResource = new NetResource
			{
				Scope = ResourceScope.GlobalNetwork,
				ResourceType = ResourceType.Disk,
				DisplayType = ResourceDisplaytype.Share,
				RemoteName = networkName
			};

			for (int i = 0; i < retryCount; i++)
			{
				var result = WNetAddConnection2(netResource, password, user, 0);

				if (result == 0)
					return;

				if (result == 1219) // 重複登入衝突
				{
					WNetCancelConnection2(networkName, 0, true);
					result = WNetAddConnection2(netResource, password, user, 0);
					if (result == 0) return;
				}

				if (result == 1326 || result == 1312 || result == 55)
				{
					if (i < retryCount - 1)
						Thread.Sleep(retryDelayMs);
					else
						ThrowFriendlyError(result);
				}
				else
				{
					ThrowFriendlyError(result);
				}
			}
		}

		public void Dispose()
		{
			if (_disconnectOnDispose)
			{
				WNetCancelConnection2(_networkName, 0, true);
			}
		}

		private void ThrowFriendlyError(int code)
		{
			string message = code switch
			{
				55 => "錯誤 55：找不到網路資源，請確認 NAS 或路徑是否存在。",
				1219 => "錯誤 1219：該資源已有其他使用者連線，請確認是否已重複登入。",
				1312 => "錯誤 1312：登入 session 不存在，請檢查服務執行身份是否有網路權限。",
				1326 => "錯誤 1326：登入失敗，請確認帳號與密碼是否正確。",
				_ => $"網路連線失敗，錯誤碼：{code}"
			};
			throw new Win32Exception(code, message);
		}

		[DllImport("mpr.dll")]
		private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

		[DllImport("mpr.dll")]
		private static extern int WNetCancelConnection2(string name, int flags, bool force);

		[StructLayout(LayoutKind.Sequential)]
		public class NetResource
		{
			public ResourceScope Scope;
			public ResourceType ResourceType;
			public ResourceDisplaytype DisplayType;
			public int Usage;
			public string LocalName;
			public string RemoteName;
			public string Comment;
			public string Provider;
		}

		public enum ResourceScope : int
		{
			Connected = 1,
			GlobalNetwork,
			Remembered,
			Recent,
			Context
		}

		public enum ResourceType : int
		{
			Any = 0,
			Disk = 1,
			Print = 2,
		}

		public enum ResourceDisplaytype : int
		{
			Generic = 0x0,
			Domain = 0x01,
			Server = 0x02,
			Share = 0x03,
			File = 0x04,
			Group = 0x05,
			Network = 0x06,
			Root = 0x07,
			Shareadmin = 0x08,
			Directory = 0x09,
			Tree = 0x0a,
			Ndscontainer = 0x0b
		}
	}
}
