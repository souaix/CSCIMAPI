using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Interfaces;

using Core.Entities.Public;
using Microsoft.Extensions.Logging;
using Core.Entities.TeamsAlarm;
using Infrastructure.Utilities;

namespace Infrastructure.Services
{
	public class TeamsAlarmService : ITeamsAlarmService
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<TeamsAlarmService> _logger;
		private readonly IRepositoryFactory _repositoryFactory;
		private readonly IFindUrlService _findUrlService;

		public TeamsAlarmService(IHttpClientFactory httpClientFactory, ILogger<TeamsAlarmService> logger, IRepositoryFactory repositoryFactory, IFindUrlService findUrlService)
		{
			_httpClientFactory = httpClientFactory;
			_logger = logger;
			_repositoryFactory = repositoryFactory;
			_findUrlService = findUrlService;
		}

		public async Task<ApiReturn<bool>> SendTeamsAlarmAsync(TeamsAlarmRequest request)
		{
			try
			{
				if (string.IsNullOrEmpty(request.Uri))
				{
					_logger.LogError("Teams Alarm API URI 不能為空");
					return ApiReturn<bool>.Failure("API URI 不能為空", false);
				}

				_logger.LogInformation("發送 Teams Alarm 請求到: {Uri}，訊息內容: {Message}", request.Uri, request.Message);

				var payload = new { message = request.Message };
				var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

				var client = _httpClientFactory.CreateClient();
				HttpResponseMessage response = await client.PostAsync(request.Uri, jsonContent);

				if (response.IsSuccessStatusCode)
				{
					_logger.LogInformation("Teams Alarm 呼叫成功");
					return ApiReturn<bool>.Success("Teams Alarm 呼叫成功", true);
				}
				else
				{
					_logger.LogError("Teams Alarm 失敗，狀態碼: {StatusCode}", response.StatusCode);
					return ApiReturn<bool>.Failure($"Teams Alarm 呼叫失敗，狀態碼: {response.StatusCode}", false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError("Teams Alarm 呼叫時發生錯誤: {ErrorMessage}", ex.Message);
				return ApiReturn<bool>.Failure($"Teams Alarm 呼叫錯誤: {ex.Message}", false);
			}
		}


		public async Task<ApiReturn<bool>> SendTeamsAlarmByGroupAsync(TeamsAlarmByGroupRequest request)
		{
			var notifyGroup = request.NotifyGroup;
			var message = request.Message;
			var (_, repository) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);//cim			

			var config = await repository.QueryFirstOrDefaultAsync<(string TEAMS, string TEAMSAPIURI)>(
				"SELECT TEAMS, TEAMSAPIURI FROM ARGOCIMNOTIFYCONFIG WHERE NOTIFYGROUP = :notifyGroup",
				new { notifyGroup });

			if ((config.TEAMS?.Trim() ?? "") != "1")
			{
				_logger.LogWarning("⚠ Teams 告警已關閉，NOTIFYGROUP={notifyGroup}");
				return ApiReturn<bool>.Failure("Teams disabled", false);
			}

			var teamsUri = config.TEAMSAPIURI;
			


			if (string.IsNullOrEmpty(teamsUri))
			{
				_logger.LogError("❌ Teams API URI 設定不完整: teamsUri 或 apiUri 為空");
				return ApiReturn<bool>.Failure("Teams URI 設定不完整", false);
			}

			try
			{
				//var payload = new { uri = teamsUri, message = message };
				var payload = new { message = message };
				var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
				var client = _httpClientFactory.CreateClient();

				_logger.LogInformation("📡 發送 Teams 告警 JSON: {Json}", JsonSerializer.Serialize(payload));

				HttpResponseMessage response = await client.PostAsync(teamsUri, jsonContent);

				//var response = await client.PostAsync(apiUri, jsonContent);

				if (response.IsSuccessStatusCode)
				{
					_logger.LogInformation("✅ Teams 告警發送成功");
					return ApiReturn<bool>.Success("Teams sent", true);
				}
				else
				{
					_logger.LogError("❌ Teams 告警發送失敗: {StatusCode}", response.StatusCode);
					return ApiReturn<bool>.Failure("Teams 發送失敗", false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError("❌ Teams API 呼叫錯誤: {Error}", ex.Message);
				return ApiReturn<bool>.Failure($"Teams 呼叫錯誤: {ex.Message}", false);
			}
		}

	}
}
