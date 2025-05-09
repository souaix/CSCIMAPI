using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Infrastructure.Utilities
{
	public class NetworkShareAccesser : IDisposable
	{
		private readonly string _networkName;

		public NetworkShareAccesser(string networkName, string user, string password)
		{
			_networkName = networkName;

			var netResource = new NetResource()
			{
				Scope = ResourceScope.GlobalNetwork,
				ResourceType = ResourceType.Disk,
				DisplayType = ResourceDisplaytype.Share,
				RemoteName = networkName
			};

			var result = WNetAddConnection2(
				netResource,
				password,
				user,
				0);

			if (result != 0)
			{
				// 1219 = 已使用其他帳號連線（略過）
				if (result == 1219)
				{
					Console.WriteLine("已掛載其他帳號的網路磁碟，共用連線略過掛載。");
				}
				else
				{
					throw new Win32Exception(result, $"Error connecting to remote share, code: {result}");
				}
			}


		}

		public void Dispose()
		{
			WNetCancelConnection2(_networkName, 0, true);
		}

		[DllImport("mpr.dll")]
		private static extern int WNetAddConnection2(NetResource netResource,
			string password, string username, int flags);

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
		};

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
