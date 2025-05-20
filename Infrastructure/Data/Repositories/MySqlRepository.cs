using System.Data;
using MySql.Data.MySqlClient;
using Dapper;
using Core.Interfaces;

namespace Infrastructure.Data.Repositories
{
	public class MySqlRepository : IRepository
	{
		private readonly string _connectionString;

		public MySqlRepository(string connectionString)
		{
			_connectionString = connectionString;
		}

		private IDbConnection CreateConnection()
		{
			return new MySqlConnection(_connectionString);
		}

		// [New] 20250520 Julie: 支援使用跨表交易控制
		public IDbConnection CreateOpenConnection()
		{
			var conn = new MySqlConnection(_connectionString);
			conn.Open();
			return conn;
		}

		public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
		{
			using (var connection = CreateConnection())
			{
				return await connection.QueryAsync<T>(sql, parameters);
			}
		}

		public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null)
		{
			using (var connection = CreateConnection())
			{
				return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
			}
		}

		public async Task<int> ExecuteAsync(string sql, object? parameters = null)
		{
			using (var connection = CreateConnection())
			{
				return await connection.ExecuteAsync(sql, parameters);
			}
		}
	}
}
