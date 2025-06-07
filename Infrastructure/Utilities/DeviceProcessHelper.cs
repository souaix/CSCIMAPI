// DeviceProcessHelper.cs
using Core.Interfaces;
using Infrastructure.Data.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Utilities
{
	public static class DeviceProcessHelper
	{
		// 單台 DeviceId 查 Process（保留原本功能）
		public static async Task<string> GetProcessByDeviceIdAsync(IRepository repository, string deviceId)
		{
			return await repository.QueryFirstOrDefaultAsync<string>(
				"SELECT DISTINCT PROCESS FROM DBO.TBLMESDEVICELIST WHERE DEVICEID = :deviceid",
				new { deviceid = deviceId });
		}

		// 🔥 新增：多台 DeviceIds 查 Process
		public static async Task<Dictionary<string, string>> GetProcessByDeviceIdsAsync(IRepository repository, List<string> deviceIds)
		{
			if (deviceIds == null || deviceIds.Count == 0)
				return new Dictionary<string, string>();

			var sql = @"
				SELECT DEVICEID, PROCESS
				FROM DBO.TBLMESDEVICELIST
				WHERE DEVICEID IN :deviceids";

			var mappings = await repository.QueryAsync<DeviceProcessMapping>(sql, new { deviceids = deviceIds });

			return mappings.ToDictionary(x => x.DeviceId, x => x.Process);
		}

		// 用於接 Query 結果的內部 class
		private class DeviceProcessMapping
		{
			public string DeviceId { get; set; }
			public string Process { get; set; }
		}
	}
}