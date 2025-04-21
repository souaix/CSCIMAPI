using Core.Entities.DboEmap;
using Core.Entities.LaserMarking;
using Core.Entities.LeakageCheck;
using Core.Entities.LotTileCheck;
using Core.Entities.MailSender;
using Core.Entities.Public;
using Core.Entities.Recipe2DCodeGenerator;
using Core.Entities.TeamsAlarm;

namespace Core.Interfaces
{
	public interface ICimApiFacade
	{
		Task<ApiReturn<int>> InsertWipDataAsync(string environment, string tableName, TblMesWipData_Record request);
		Task<ApiReturn<IEnumerable<Config>>> GetConfigDataAsync(LaserMarkingRequest request);
        Task<ApiReturn<string>> GenerateTileIdsAsync(LaserMarkingRequest request);		
		Task<ApiReturn<List<LeakageAnomalyDto>>> LeakageCheckAsync(LeakageCheckRequest request);
		Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request);
		Task<ApiReturn<bool>> SendTeamsAlarmAsync(TeamsAlarmRequest request);
		Task<ApiReturn<bool>> SendTeamsAlarmByGroupAsync(TeamsAlarmByGroupRequest request);
		Task<ApiReturn<bool>> SendEmailAsync(MailSenderRequest request);
		Task<ApiReturn<List<TileCheckResultDto>>> LotTileCheckAsync(LotTileCheckRequest request);
		Task<ApiReturn<int>> Save2DCodeAsync(Recipe2DCodeRequest request);


	}
}
