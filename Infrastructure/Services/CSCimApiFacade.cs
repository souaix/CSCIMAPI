using Core.Interfaces;
using Core.Entities.DboEmap;
using Core.Entities.Public;
using Core.Entities.LaserMarking;
using Core.Entities.LeakageCheck;

namespace Infrastructure.Services
{
	public class CSCimAPIFacade : ICSCimAPIFacade
	{
		private readonly IInsertWipDataService _insertWipDataService;
		private readonly ILaserMarkingService _laserMarkingService;
		private readonly ILeakageCheckService _leakageCheckService;
		public CSCimAPIFacade(
			IInsertWipDataService insertWipDataService,
			ILaserMarkingService laserMarkingService,
			ILeakageCheckService leakageCheckService)
		{
			_insertWipDataService = insertWipDataService;
			_laserMarkingService = laserMarkingService;
			_leakageCheckService = leakageCheckService;
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

	}
}
