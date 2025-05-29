using Core.Entities.CheckLimit;
using Core.Entities.DboEmap;
using Core.Entities.DefectCount;
using Core.Entities.LaserMarking;
using Core.Entities.LaserMarkingFrontend;
using Core.Entities.LeakageCheck;
using Core.Entities.LotTileCheck;
using Core.Entities.MailSender;
using Core.Entities.Public;
using Core.Entities.Recipe2DCodeGenerator;
using Core.Entities.RecycleLotCopy;
using Core.Entities.Scada;
using Core.Entities.TeamsAlarm;
using Core.Entities.YieldRecordData;
using System.Threading.Tasks;

namespace Core.Interfaces
{
	public interface ICimApiFacade
	{
		Task<ApiReturn<int>> InsertWipDataAsync(string environment, string tableName, TblMesWipData_Record request);
		Task<ApiReturn<IEnumerable<Config>>> GetConfigDataAsync(LaserMarkingRequest request);
        Task<ApiReturn<string>> GenerateTileIdsAsync(LaserMarkingRequest request);
		Task<ApiReturn<LaserMarkingFrontendProduct>> GenerateFrontendTileIdsAsync(LaserMarkingFrontendRequest request);
		Task<ApiReturn<List<LeakageAnomalyDto>>> LeakageCheckAsync(LeakageCheckRequest request);
		Task<ApiReturn<List<LeakageRawDataDto>>> LeakageSelectAsync(LeakageCheckRequest request);
		Task<ApiReturn<bool>> SendTeamsAlarmAsync(TeamsAlarmRequest request);
		Task<ApiReturn<bool>> SendTeamsAlarmByGroupAsync(TeamsAlarmByGroupRequest request);
		Task<ApiReturn<bool>> SendEmailAsync(MailSenderRequest request);
		Task<ApiReturn<object>> LotTileCheckAsync(LotTileCheckRequest request);
		Task<ApiReturn<int>> Save2DCodeAsync(Recipe2DCodeRequest request);
		Task<ApiReturn<YieldRecordDataResult>> LoadYieldRecordDataAsync(YieldRecordDataRequest request);
		Task<ApiReturn<DefectCountResponse>> CountDefectsAsync(DefectCountRequest request);
		Task<ApiReturn<bool>> WriteScadaTagAsync(ScadaWriteRequest request);
        Task<ApiReturn<string>> RecycleLotCopyAsync(RecycleLotCopyRequest request);
        Task<ApiReturn<CheckLimitResponse>> CheckLimitAsync(CheckLimitRequest request);

    }
}
