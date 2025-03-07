using Core.Interfaces;
using Core.Entities.DboEmap;
using Core.Entities.Public;
using Core.Entities.LaserMarking;

namespace Infrastructure.Services
{
	public class CSCimAPIFacade : ICSCimAPIFacade
	{
		private readonly IInsertWipDataService _insertWipDataService;
		private readonly ILaserMarkingService _laserMarkingService;

		public CSCimAPIFacade(
			IInsertWipDataService insertWipDataService,
			ILaserMarkingService laserMarkingService)
		{
			_insertWipDataService = insertWipDataService;
			_laserMarkingService = laserMarkingService;
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

    }
}
