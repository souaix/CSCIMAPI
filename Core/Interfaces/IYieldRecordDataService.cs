using Core.Entities.Public;
using Core.Entities.YieldRecordData;

namespace Core.Interfaces
{
	public interface IYieldRecordDataService
	{
		Task<ApiReturn<YieldRecordDataResult>> LoadYieldRecordDataAsync(YieldRecordDataRequest request);

	}
}
