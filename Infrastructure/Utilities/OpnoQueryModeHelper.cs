using Core.Entities.ArgoCim;
using Core.Interfaces;

namespace Infrastructure.Utilities
{
	public static class OpnoQueryModelHelper
	{
		public static async Task<(List<string> Steps, List<string> DeviceIds)> ResolveQueryModeAsync(
			IRepository repo, string opno, string deviceId)
		{
			// 查 QUERYMODE
			var queryModeAttr = await repo.QueryFirstOrDefaultAsync<ARGOCIMOPNOATTRIBUTE>(
				@"SELECT * FROM ARGOCIMOPNOATTRIBUTE WHERE OPNO = :opno AND ITEM = 'QUERYMODE'",
				new { opno });

			if (queryModeAttr == null || string.IsNullOrEmpty(queryModeAttr.Value))
				throw new Exception($"站點 {opno} 未設定 QUERYMODE");

			var queryMode = queryModeAttr.Value;

			// 查 OPNOGROUP
			var opGroupAttr = await repo.QueryFirstOrDefaultAsync<ARGOCIMOPNOATTRIBUTE>(
				@"SELECT * FROM ARGOCIMOPNOATTRIBUTE WHERE OPNO = :opno AND ITEM = 'OPNOGROUP'",
				new { opno });

			var opGroup = opGroupAttr?.Value;

			List<string> steps = new();
			List<string> deviceIds = new();

			switch (queryMode)
			{
				case "S1": // 單站單機
					steps.Add(opno);
					deviceIds.Add(deviceId);
					break;
				case "S2": // 單站多機
					steps.Add(opno);
					var devsS2 = await repo.QueryAsync<ARGOCIMOPNOATTRIBUTE>(
						@"SELECT * FROM ARGOCIMOPNOATTRIBUTE WHERE OPNO = :opno AND ITEM = 'DEVICEIDS'",
						new { opno });

					deviceIds = devsS2
						.SelectMany(x => x.Value?.Split(',') ?? Array.Empty<string>())
						.Select(x => x.Trim())
						.Distinct()
						.ToList();
					break;

				case "S3": // 多站單機
					if (string.IsNullOrEmpty(opGroup))
						throw new Exception($"站點 {opno} 無 OPNOGROUP 設定");

					steps = (await repo.QueryAsync<ARGOCIMOPNOATTRIBUTE>(
						@"SELECT * FROM ARGOCIMOPNOATTRIBUTE 
                          WHERE ITEM = 'OPNOGROUP' AND VALUE = :opGroup",
						new { opGroup }))
						.Select(x => x.Opno)
						.Distinct()
						.ToList();
					deviceIds.Add(deviceId);
					break;

				case "S4": // 多站多機
					if (string.IsNullOrEmpty(opGroup))
						throw new Exception($"站點 {opno} 無 OPNOGROUP 設定");

					steps = (await repo.QueryAsync<ARGOCIMOPNOATTRIBUTE>(
						@"SELECT * FROM ARGOCIMOPNOATTRIBUTE 
                          WHERE ITEM = 'OPNOGROUP' AND VALUE = :opGroup",
						new { opGroup }))
						.Select(x => x.Opno)
					.Distinct()
					.ToList();

					var devsS4 = await repo.QueryAsync<ARGOCIMOPNOATTRIBUTE>(
						@"SELECT * FROM ARGOCIMOPNOATTRIBUTE 
                          WHERE OPNO IN :steps AND ITEM = 'DEVICEIDS'",
						new { steps });

					deviceIds = devsS4
						.SelectMany(x => x.Value?.Split(',') ?? Array.Empty<string>())
						.Select(x => x.Trim())
						.Distinct()
						.ToList();
					break;

				default:
					throw new Exception($"未知的查詢模式：{queryMode}");
			}

			return (steps, deviceIds);
		}
	}
}

