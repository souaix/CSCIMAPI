using Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services // ✅ 加上 namespace
{
	public class FindUrlService : IFindUrlService
	{
		private readonly IRepositoryFactory _repositoryFactory;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly string _environment;
		

		public FindUrlService(IRepositoryFactory repositoryFactory, IHttpContextAccessor httpContextAccessor)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<string?> GetUrlByIdAsync(string urlId)
		{
			var repository = _repositoryFactory.CreateRepository("iotIMesprod");
			string query = "SELECT URL FROM ARGOCIMURLMAPPING WHERE URLID = :UrlId";
			return await repository.QueryFirstOrDefaultAsync<string>(query, new { UrlId = urlId });
		}




	}
}
