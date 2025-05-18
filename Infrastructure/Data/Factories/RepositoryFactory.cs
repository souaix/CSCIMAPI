using Core.Interfaces;
using Infrastructure.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
namespace Infrastructure.Data.Factories;

public class RepositoryFactory : IRepositoryFactory
{
	private readonly Dictionary<string, Dictionary<string, (string ConnectionString, string DbType)>> _databaseConfigs;

	public RepositoryFactory(Dictionary<string, Dictionary<string, (string, string)>> databaseConfigs)
	{
		_databaseConfigs = databaseConfigs;
	}

	public IRepository CreateRepository(string environment, string repositoryName)
	{
		// 根據 environment 和 repositoryName 獲取對應的連線字串
		if (_databaseConfigs.TryGetValue(environment, out var repositories) &&
			repositories.TryGetValue(repositoryName, out var config))
		{
			var connectionString = config.ConnectionString;
			var dbType = config.DbType;

			// 根據資料庫類型創建對應的 Repository
			return dbType switch
			{
				"Oracle" => new OracleRepository(connectionString),
				"MySQL" => new MySqlRepository(connectionString),
				_ => throw new ArgumentException($"Invalid database type: {dbType}")
			};
		}

		throw new ArgumentException($"Invalid repository name: {repositoryName} for environment: {environment}");
	}

	public Dictionary<string, IRepository> CreateRepositories(string environment)
	{
		// 創建資料庫字典
		var repositories = new Dictionary<string, IRepository>();

		if (_databaseConfigs.TryGetValue(environment, out var environmentRepositories))
		{
			// 根據配置創建所有資料庫的連線
			foreach (var repoConfig in environmentRepositories)
			{
				var repositoryName = repoConfig.Key;
				repositories[repositoryName] = CreateRepository(environment, repositoryName);
			}
		}

		return repositories;
	}

	//private readonly string _dboEmapProdConnectionString;
	//private readonly string _dboEmapTestConnectionString;
	//private readonly string _csCimEmapProdConnectionString;
	//private readonly string _csCimEmapTestConnectionString;
	//private readonly string _laserMarkingNormalProdConnectionString;
	//private readonly string _laserMarkingNormalTestConnectionString;
	//private readonly string _laserMarkingCO2ProdConnectionString;
	//private readonly string _cim28ConnectionString;


	//public RepositoryFactory(
	//	string dboEmapProdConnectionString,
	//	string dboEmapTestConnectionString,
	//	string csCimEmapProdConnectionString,
	//	string csCimEmapTestConnectionString,
	//	string laserMarkingNormalProdConnectionString,
	//	string laserMarkingNormalTestConnectionString,
	//	string laserMarkingCO2ProdConnectionString,
	//	string cim28ConnectionString

	//	)
	//{
	//	_dboEmapProdConnectionString = dboEmapProdConnectionString;
	//	_dboEmapTestConnectionString = dboEmapTestConnectionString;
	//	_laserMarkingNormalProdConnectionString = laserMarkingNormalProdConnectionString;
	//	_laserMarkingNormalTestConnectionString = laserMarkingNormalTestConnectionString;
	//	_laserMarkingCO2ProdConnectionString = laserMarkingCO2ProdConnectionString;
	//	_cim28ConnectionString = cim28ConnectionString;
	//	_csCimEmapProdConnectionString = csCimEmapProdConnectionString;
	//	_csCimEmapTestConnectionString = csCimEmapTestConnectionString;
	//}

	//public IRepository CreateRepository(string environment)
	//{
	//	return environment switch
	//	{
	//		"dboEmapProd" => new OracleRepository(_dboEmapProdConnectionString),
	//		"dboEmapTest" => new OracleRepository(_dboEmapTestConnectionString),
	//		"csCimEmapProd" => new OracleRepository(_csCimEmapProdConnectionString),
	//		"csCimEmapTest" => new OracleRepository(_csCimEmapTestConnectionString),
	//		"Prod" => new OracleRepository(_dboEmapProdConnectionString), //舊版 暫時保留
	//		"Test" => new OracleRepository(_dboEmapTestConnectionString), //舊版 暫時保留
	//		"laserMarkingProd" => new MySqlRepository(_laserMarkingNormalProdConnectionString),
	//		"laserMarkingTest" => new MySqlRepository(_laserMarkingNormalTestConnectionString),
	//		"laserMarkingCO2Prod" => new MySqlRepository(_laserMarkingCO2ProdConnectionString),
	//		"cim28" => new MySqlRepository(_cim28ConnectionString),
	//		_ => throw new ArgumentException($"Invalid environment: {environment}")
	//	};
	//}
}