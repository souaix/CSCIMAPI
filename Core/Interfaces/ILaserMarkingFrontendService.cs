using Core.Entities.LaserMarkingFrontend;
using Core.Entities.Public;

namespace Core.Interfaces
{
	public interface ILaserMarkingFrontendService
	{
		Task<ApiReturn<bool>> GenerateFrontendTileIdsAsync(LaserMarkingFrontendRequest request);
	}
}
