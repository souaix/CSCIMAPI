using Core.Entities.DefectCount;
using Core.Entities.Public;

namespace Core.Interfaces
{
    public interface IDefectCountService
    {
        Task<ApiReturn<DefectCountResponse>> CountDefectsAsync(DefectCountRequest request);
    }
}
