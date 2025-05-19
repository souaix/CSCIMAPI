using Core.Interfaces;
using Core.Entities.Public;
using Core.Entities.DboEmap;
using Core.Utilities;
using Infrastructure.Utilities;

namespace Infrastructure.Services
{
	public class InsertWipDataService : IInsertWipDataService
	{
		private readonly IRepositoryFactory _repositoryFactory;

		public InsertWipDataService(IRepositoryFactory repositoryFactory)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<ApiReturn<int>> InsertWipDataAsync(string environment, string tableName, TblMesWipData_Record request)
		{
			if (!Utils.IsValidTableName(tableName))
				return ApiReturn<int>.Failure("Invalid table name.");

			//var repository = _repositoryFactory.CreateRepository(environment);
			var repositories = RepositoryHelper.CreateRepositories(environment, _repositoryFactory);
			// 使用某個特定的資料庫
			var repository = repositories["CsCimEmap"];

			string sql = @"
                INSERT INTO {tableName} (
                    ORACLEDATE, RECORDDATE, DEVICEID, PROCESS, STEP, STEPORDER, LOTSERIAL, 
                    USERID, PARTNO, PARTNOREV, LOTNO, MBOMNO, TILEID, DATAINDEX, TILE_TOTAL_QTY,
                    TILE_IN_QTY, TILE_OUT_QTY, CELL_IN_QTY, CELL_OUT_QTY, ARRAY_QTY, FIXTUREID_01, 
                    FIXTUREID_02, RACKID, CASSETTEID, RECIPENAME, SERIALNUM, SERVERID, STATUS,
                    ALARMCODE, ALARMMESSAGE, ALARMSTATUS, CSTYPE, DEVICEID_1
                ) VALUES (
                    SYSDATE, :RecordDate, :DeviceId, :Process, :Step, :StepOrder, :LotSerial,
                    :UserId, :PartNo, :PartNoRev, :LotNo, :MbomNo, :TileId, :DataIndex, :TileTotalQty,
                    :TileInQty, :TileOutQty, :CellInQty, :CellOutQty, :ArrayQty, :FixtureId01,
                    :FixtureId02, :RackId, :CassetteId, :RecipeName, :SerialNum, :ServerId, :Status,
                    :AlarmCode, :AlarmMessage, :AlarmStatus, :CsType, :DeviceId1
                )";

			var rowsAffected = await repository.ExecuteAsync(sql, request);
			return ApiReturn<int>.Success("Data inserted successfully.", rowsAffected);
		}
	}
}
