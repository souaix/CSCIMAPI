using Core.Entities.LaserMarking;
using Core.Entities.Public;

namespace Core.Interfaces
{
	public interface ILaserMarkingService
	{
		Task<ApiReturn<IEnumerable<Config>>> GetConfigDataAsync(LaserMarkingRequest request);

        /// <summary>
        /// 產生 Tile ID 並存入資料庫
        /// </summary>
        Task<ApiReturn<string>> GenerateTileIdsAsync(LaserMarkingRequest request);
    }
}
