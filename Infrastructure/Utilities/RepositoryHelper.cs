using Core.Interfaces;

namespace Infrastructure.Utilities
{
	public static class RepositoryHelper
	{
		public static (IRepository repo, IRepository repoCim, IRepository repoLaser) CreateRepositories(string environment, IRepositoryFactory factory)
		{
			return environment switch
			{
				"Production" => (
					factory.CreateRepository("dboEmapProd"),
					factory.CreateRepository("csCimEmapProd"),
					factory.CreateRepository("laserMarkingProd")
				),
				"Test" => (
					factory.CreateRepository("dboEmapTest"),
					factory.CreateRepository("csCimEmapTest"),
					factory.CreateRepository("laserMarkingTest")
				),
                "Develop" => (
                    factory.CreateRepository("dboEmapTest"),
                    factory.CreateRepository("cim28"),
					factory.CreateRepository("laserMarkingProd")
				),
                _ => throw new Exception("Unknown environment: " + environment)
			};
		}
	}

}
