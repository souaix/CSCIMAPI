using Core.Interfaces;
using Infrastructure.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
namespace Infrastructure.Data.Factories;

public class RepositoryFactory : IRepositoryFactory
{
	private readonly string _dboEmapProdConnectionString;
	private readonly string _dboEmapTestConnectionString;
	private readonly string _laserMarkingNormalProdConnectionString;
	private readonly string _laserMarkingNormalTestConnectionString;
	private readonly string _cim28ConnectionString;

	public RepositoryFactory(
		string dboEmapProdConnectionString,
		string dboEmapTestConnectionString,
		string laserMarkingNormalProdConnectionString,
		string laserMarkingNormalTestConnectionString,
		string cim28ConnectionString
		)
	{
		_dboEmapProdConnectionString = dboEmapProdConnectionString;
		_dboEmapTestConnectionString = dboEmapTestConnectionString;
		_laserMarkingNormalProdConnectionString = laserMarkingNormalProdConnectionString;
		_laserMarkingNormalTestConnectionString = laserMarkingNormalTestConnectionString;
		_cim28ConnectionString = cim28ConnectionString;
	}

	public IRepository CreateRepository(string environment)
	{
		return environment switch
		{
			"dboEmapProd" => new OracleRepository(_dboEmapProdConnectionString),
			"dboEmapTest" => new OracleRepository(_dboEmapTestConnectionString),
			"Prod" => new OracleRepository(_dboEmapProdConnectionString), //舊版 暫時保留
			"Test" => new OracleRepository(_dboEmapTestConnectionString), //舊版 暫時保留
			"laserMarkingProd" => new MySqlRepository(_laserMarkingNormalProdConnectionString),
			"laserMarkingTest" => new MySqlRepository(_laserMarkingNormalTestConnectionString),
			"cim28" => new MySqlRepository(_cim28ConnectionString),
			_ => throw new ArgumentException($"Invalid environment: {environment}")
		};
	}
}