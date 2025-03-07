using Core.Entities.DboEmap;
using Core.Entities.LaserMarking;
using Core.Entities.Public;

namespace Core.Interfaces
{
	public interface ICSCimAPIFacade
	{
		Task<ApiReturn<int>> InsertWipDataAsync(string environment, string tableName, TblMesWipData_Record request);
		Task<ApiReturn<IEnumerable<Config>>> GetConfigDataAsync(LaserMarkingRequest request);
        Task<ApiReturn<string>> GenerateTileIdsAsync(LaserMarkingRequest request);

    }
}
