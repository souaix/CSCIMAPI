﻿using System.Data;

namespace Core.Interfaces
{
	public interface IRepository
	{
		Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null);
		Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null);
		Task<int> ExecuteAsync(string sql, object? parameters = null);

		// 控制交易
		IDbConnection CreateOpenConnection();
	}
}
