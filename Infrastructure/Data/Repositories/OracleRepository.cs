using System.Data;
using System.Data.OracleClient;
using Dapper;
using Core.Interfaces;
using MySql.Data.MySqlClient;

namespace Infrastructure.Data.Repositories;
public class OracleRepository : IRepository
{
	private readonly string _connectionString;

	public OracleRepository(string connectionString)
	{
		_connectionString = connectionString;
	}

	private IDbConnection CreateConnection()
	{
		return new OracleConnection(_connectionString);
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
        // 打印參數
        if (parameters != null)
        {
            Console.WriteLine("Parameters:");
            foreach (var property in parameters.GetType().GetProperties())
            {
                var name = property.Name;
                var value = property.GetValue(parameters);
                Console.WriteLine($"    {name}: {value}");
            }
        }
        else
        {
            Console.WriteLine("Parameters: None");
        }		
			return await connection.ExecuteAsync(sql, parameters);
		}
	}
}
