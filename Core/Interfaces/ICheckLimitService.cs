using Core.Entities.CheckLimit;
using Core.Entities.Public;
using System.Threading.Tasks;

namespace Core.Interfaces
{
    public interface ICheckLimitService
    {
        Task<ApiReturn<CheckLimitResponse>> CheckLimitAsync(CheckLimitRequest request);
    }
}
