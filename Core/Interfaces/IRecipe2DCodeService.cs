using Core.Entities.LaserMarking;
using Core.Entities.Public;
using Core.Entities.Recipe2DCodeGenerator;
using System.Threading.Tasks;

namespace Core.Interfaces
{
	public interface IRecipe2DCodeService
	{
		Task<ApiReturn<int>> Save2DCodeAsync(Recipe2DCodeRequest request);
	}
}
