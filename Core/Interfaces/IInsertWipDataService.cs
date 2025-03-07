using Core.Entities.Public;
using Core.Entities.DboEmap;

namespace Core.Interfaces
{
	public interface IInsertWipDataService
	{
		Task<ApiReturn<int>> InsertWipDataAsync(string environment, string tableName, TblMesWipData_Record request);
	}
}
