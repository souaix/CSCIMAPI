namespace Core.Interfaces
{
	public interface IRepositoryFactory
	{
		//IRepository CreateRepository(string environment);

		IRepository CreateRepository(string environment, string repositoryName);
		Dictionary<string, IRepository> CreateRepositories(string environment);
	}
}
