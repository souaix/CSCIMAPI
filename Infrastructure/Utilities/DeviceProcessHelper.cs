// DeviceProcessHelper.cs
using Core.Interfaces;
using Infrastructure.Data.Repositories;
using System.Threading.Tasks;

namespace Infrastructure.Utilities
{
	public static class DeviceProcessHelper
	{
		public static async Task<string> GetProcessByDeviceIdAsync(IRepository repository, string deviceId)
		{
			return await repository.QueryFirstOrDefaultAsync<string>(
				"SELECT DISTINCT PROCESS FROM DBO.TBLMESDEVICELIST WHERE DEVICEID = :deviceid",
				new { deviceid = deviceId });
		}
	}
}
