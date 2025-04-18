using Core.Entities.Public;
using Core.Entities.LotTileCheck;

namespace Core.Interfaces
{
	public interface ILotTileCheckService
	{
		Task<ApiReturn<List<TileCheckResultDto>>> CheckLotTileAsync(LotTileCheckRequest request);
	}
}
