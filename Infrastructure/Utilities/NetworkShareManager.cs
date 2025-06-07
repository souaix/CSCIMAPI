using System;
using System.Collections.Concurrent;

namespace Infrastructure.Utilities
{
	public static class NetworkShareManager
	{
		private static readonly ConcurrentDictionary<string, Lazy<NetworkShareAccesser>> _shareMap = new();

		public static void EnsureConnected(string sharePath, string user, string password)
		{
			var lazyAccesser = _shareMap.GetOrAdd(sharePath, key => new Lazy<NetworkShareAccesser>(() =>
			{
				try
				{
					Console.WriteLine($"[NAS] 嘗試掛載：{sharePath}");
					return new NetworkShareAccesser(sharePath, user, password, disconnectOnDispose: false);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[NAS] 掛載失敗：{ex.Message}，移除快取");
					_shareMap.TryRemove(sharePath, out _);
					throw;
				}
			}));

			try
			{
				_ = lazyAccesser.Value;
			}
			catch
			{
				_shareMap.TryRemove(sharePath, out _);
				throw;
			}
		}
	}
}
