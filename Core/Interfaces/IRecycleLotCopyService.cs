using Core.Entities.Public;
using Core.Entities.RecycleLotCopy;

namespace Core.Interfaces
{
    public interface IRecycleLotCopyService
    {
        Task<ApiReturn<string>> ProcessRecycleLotCopyAsync(RecycleLotCopyRequest request);
    }
    
}
