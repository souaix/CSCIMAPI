using Core.Interfaces;

namespace Infrastructure.Utilities
{
	public static class RepositoryHelper
	{
		public static Dictionary<string, IRepository> CreateRepositories(string environment, IRepositoryFactory factory)
		{
			// 直接從 Factory 獲取所有資料庫的連線字典
			return factory.CreateRepositories(environment);
		}

		//public static (IRepository repo, IRepository repoCim, IRepository repoLaser, IRepository repoLaserCO2) CreateRepositories(string environment, IRepositoryFactory factory)
		//{
		//	return environment switch
		//	{
		//		"Production" => (
		//			factory.CreateRepository("dboEmapProd"),
		//			factory.CreateRepository("csCimEmapProd"),
		//			factory.CreateRepository("laserMarkingProd"),
		//			factory.CreateRepository("laserMarkingCO2Prod")
		//		),
		//		"Test" => (
		//			factory.CreateRepository("dboEmapTest"),
		//			factory.CreateRepository("csCimEmapTest"),
		//			factory.CreateRepository("laserMarkingTest"),
		//			factory.CreateRepository("laserMarkingCO2Prod")
		//		),
		//              "Develop" => (
		//                  factory.CreateRepository("dboEmapTest"),
		//                  factory.CreateRepository("cim28"),
		//			factory.CreateRepository("laserMarkingProd"),
		//			factory.CreateRepository("cim28")
		//		),
		//              _ => throw new Exception("Unknown environment: " + environment)
		//	};
		//}
	}

}
