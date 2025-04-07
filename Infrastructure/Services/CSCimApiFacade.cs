using Core.Interfaces;
using Core.Entities.DboEmap;
using Core.Entities.Public;
using Core.Entities.LaserMarking;
using Core.Entities.LeakageCheck;
using Core.Entities.MailSender;
using Core.Entities.TeamsAlarm;

namespace Infrastructure.Services
{
	public class CSCimAPIFacade : ICSCimAPIFacade
	{
		private readonly IInsertWipDataService _insertWipDataService;
		private readonly ILaserMarkingService _laserMarkingService;
		private readonly ILeakageCheckService _leakageCheckService;
		private readonly ITeamsAlarmService _teamsAlarmService;
		private readonly IMailSenderService _mailSenderService;
		public CSCimAPIFacade(
			IInsertWipDataService insertWipDataService,
			ILaserMarkingService laserMarkingService,
			ILeakageCheckService leakageCheckService,
			ITeamsAlarmService teamsAlarmService,
			IMailSenderService mailSenderService)
		{
			_insertWipDataService = insertWipDataService;
			_laserMarkingService = laserMarkingService;
			_leakageCheckService = leakageCheckService;
			_teamsAlarmService = teamsAlarmService;
			_mailSenderService = mailSenderService;
		}

		public Task<ApiReturn<int>> InsertWipDataAsync(string environment, string tableName, TblMesWipData_Record request)
		{
			return _insertWipDataService.InsertWipDataAsync(environment, tableName, request);
		}

		public Task<ApiReturn<IEnumerable<Config>>> GetConfigDataAsync(LaserMarkingRequest request)
		{
			return _laserMarkingService.GetConfigDataAsync(request);
		}

        public Task<ApiReturn<string>> GenerateTileIdsAsync(LaserMarkingRequest request)
        {
            return _laserMarkingService.GenerateTileIdsAsync(request);
        }
		
		//public Task<ApiReturn<string>> LeakageCheckAsync(LeakageCheck request)
		public Task<ApiReturn<List<LeakageAnomalyDto>>> LeakageCheckAsync(LeakageCheckRequest request)
		{
			return _leakageCheckService.LeakageCheckAsync(request);
		}

		public async Task<ApiReturn<bool>> SendTeamsAlarmAsync(TeamsAlarmRequest request)
		{
			return await _teamsAlarmService.SendTeamsAlarmAsync(request);
		}

		public async Task<ApiReturn<bool>> SendTeamsAlarmByGroupAsync(TeamsAlarmByGroupRequest request)
		{
			return await _teamsAlarmService.SendTeamsAlarmByGroupAsync(request);
		}

		public async Task<ApiReturn<bool>> SendEmailAsync(MailSenderRequest request)
		{
			return await _mailSenderService.SendEmailAsync(request);
		}
	}
}
