using Core.Entities.LeakageCheck;
using Core.Entities.Public;
using System.Threading.Tasks;

namespace Core.Interfaces
{
	public interface ILeakageCheckService
	{
		//Task<ApiReturn<string>> LeakageCheckAsync(LeakageCheck request);
		Task<ApiReturn<List<LeakageAnomalyDto>>> LeakageCheckAsync(LeakageCheckRequest request);
		Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request);
	}
}
