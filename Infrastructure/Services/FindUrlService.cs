using Core.Interfaces;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Org.BouncyCastle.Asn1.Ocsp;
using System;

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

		public async Task<string?> GetUrlByIdAsync(string urlId, string environment)
		{
			var (_, repository, _) = RepositoryHelper.CreateRepositories(environment, _repositoryFactory);//cim			
			string query = "SELECT URL FROM ARGOCIMURLMAPPING WHERE URLID = :UrlId";
			return await repository.QueryFirstOrDefaultAsync<string>(query, new { UrlId = urlId });
		}




	}
}
