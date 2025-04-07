using Core.Entities.TeamsAlarm;
using Core.Entities.Public;
using System.Threading.Tasks;

namespace Core.Interfaces
{
	public interface ITeamsAlarmService
	{
		Task<ApiReturn<bool>> SendTeamsAlarmAsync(TeamsAlarmRequest request);
		Task<ApiReturn<bool>> SendTeamsAlarmByGroupAsync(TeamsAlarmByGroupRequest request);
	}
}
