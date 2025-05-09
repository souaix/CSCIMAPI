using Core.Interfaces;
using Core.Entities.DboEmap;
using Core.Entities.Public;
using Core.Entities.LaserMarking;
using Core.Entities.LeakageCheck;
using Core.Entities.MailSender;
using Core.Entities.TeamsAlarm;
using Core.Entities.LotTileCheck;
using Core.Entities.Recipe2DCodeGenerator;
using Core.Entities.YieldRecordData;

namespace Infrastructure.Services
{
	public class CimApiFacade : ICimApiFacade
	{
		private readonly IInsertWipDataService _insertWipDataService;
		private readonly ILaserMarkingService _laserMarkingService;
		private readonly ILeakageCheckService _leakageCheckService;
		private readonly ITeamsAlarmService _teamsAlarmService;
		private readonly IMailSenderService _mailSenderService;
		private readonly ILotTileCheckService _lotTileCheckService;
		private readonly IRecipe2DCodeService _recipe2DCodeService;
		private readonly IYieldRecordDataService _yieldRecordDataService;

		public CimApiFacade(
			IInsertWipDataService insertWipDataService,
			ILaserMarkingService laserMarkingService,
			ILeakageCheckService leakageCheckService,
			ITeamsAlarmService teamsAlarmService,
			IMailSenderService mailSenderService,
			ILotTileCheckService lotTileCheckService,
			IRecipe2DCodeService recipe2DCodeService,
			IYieldRecordDataService yieldRecordDataService)
		{
			_insertWipDataService = insertWipDataService;
			_laserMarkingService = laserMarkingService;
			_leakageCheckService = leakageCheckService;
			_teamsAlarmService = teamsAlarmService;
			_mailSenderService = mailSenderService;
			_lotTileCheckService = lotTileCheckService;
			_recipe2DCodeService = recipe2DCodeService;
			_yieldRecordDataService = yieldRecordDataService;
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
		
		public Task<ApiReturn<List<LeakageAnomalyDto>>> LeakageCheckAsync(LeakageCheckRequest request)
		{
			return _leakageCheckService.LeakageCheckAsync(request);
		}

		public Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request)
		{
			return _leakageCheckService.LeakageSelectAsync(request);
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

		public async Task<ApiReturn<List<TileCheckResultDto>>> LotTileCheckAsync(LotTileCheckRequest request)
		{
			return await _lotTileCheckService.CheckLotTileAsync(request);
		}

		public Task<ApiReturn<int>> Save2DCodeAsync(Recipe2DCodeRequest request)
		{
			return _recipe2DCodeService.Save2DCodeAsync(request);
		}

		public Task<ApiReturn<YieldRecordDataResult>> LoadYieldRecordDataAsync(YieldRecordDataRequest request)
		{
			return _yieldRecordDataService.LoadYieldRecordDataAsync(request);
		}


	}
}
