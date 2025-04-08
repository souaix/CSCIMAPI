using Core.Interfaces;
using Infrastructure.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
namespace Infrastructure.Data.Factories;

public class RepositoryFactory : IRepositoryFactory
{
	private readonly string _dboEmapProdConnectionString;
	private readonly string _dboEmapTestConnectionString;
	private readonly string _csCimEmapProdConnectionString;
	private readonly string _csCimEmapTestConnectionString;
	private readonly string _laserMarkingNormalProdConnectionString;
	private readonly string _laserMarkingNormalTestConnectionString;
	private readonly string _cim28ConnectionString;


	public RepositoryFactory(
		string dboEmapProdConnectionString,
		string dboEmapTestConnectionString,
		string csCimEmapProdConnectionString,
		string csCimEmapTestConnectionString,
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
		_csCimEmapProdConnectionString = csCimEmapProdConnectionString;
		_csCimEmapTestConnectionString = csCimEmapTestConnectionString;
	}

	public IRepository CreateRepository(string environment)
	{
		return environment switch
		{
			"dboEmapProd" => new OracleRepository(_dboEmapProdConnectionString),
			"dboEmapTest" => new OracleRepository(_dboEmapTestConnectionString),
			"csCimEmapProd" => new OracleRepository(_csCimEmapProdConnectionString),
			"csCimEmapTest" => new OracleRepository(_csCimEmapTestConnectionString),
			"Prod" => new OracleRepository(_dboEmapProdConnectionString), //舊版 暫時保留
			"Test" => new OracleRepository(_dboEmapTestConnectionString), //舊版 暫時保留
			"laserMarkingProd" => new MySqlRepository(_laserMarkingNormalProdConnectionString),
			"laserMarkingTest" => new MySqlRepository(_laserMarkingNormalTestConnectionString),
			"cim28" => new MySqlRepository(_cim28ConnectionString),
			_ => throw new ArgumentException($"Invalid environment: {environment}")
		};
	}
}