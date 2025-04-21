namespace Core.Interfaces
{
	public interface IFindUrlService
	{
		//找尋Url
		Task<string?> GetUrlByIdAsync(string urlId,string environment);

	}
}
