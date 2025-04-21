using Core.Interfaces;

namespace Infrastructure.Utilities
{
	public static class RepositoryHelper
	{
		public static (IRepository repo, IRepository repoCim) CreateRepositories(string environment, IRepositoryFactory factory)
		{
			return environment switch
			{
				"Production" => (
					factory.CreateRepository("dboEmapProd"),
					factory.CreateRepository("csCimEmapProd")
				),
				"Test" => (
					factory.CreateRepository("dboEmapTest"),
					factory.CreateRepository("csCimEmapTest")
				),
				_ => throw new Exception("Unknown environment: " + environment)
			};
		}
	}

}
