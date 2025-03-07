namespace Core.Interfaces
{
	public interface IRepositoryFactory
	{
		IRepository CreateRepository(string environment);
	}
}
