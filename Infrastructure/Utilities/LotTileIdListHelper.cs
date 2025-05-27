using Core.Entities.LotTileCheck;
using Core.Interfaces;

namespace Infrastructure.Helpers;

public static class LotTileIdListHelper
{
	/// <summary>
	/// 將一般站點結果轉換為統一格式，用於儲存 ARGOCIMLOTTILEIDLIST
	/// </summary>
	public static List<TileCheckLaserInkDto> ConvertToLaserInkFormat(List<TileCheckResultDto> input)
	{
		return input.Select(x => new TileCheckLaserInkDto
		{
			TileId = x.TileId,
			LotNo = x.LotNo,
			RecordDate = x.RecordDate,
			ResultList = x.ResultList,
			Reason = x.Reason,
			SourceCol = "",        // 一般站沒有 CS_TILEID 來源
			TH_TileId = "",        // 一般站不需要
			TH_TileId_2 = ""       // 一般站不需要
		}).ToList();
	}

	/// <summary>
	/// 將 TileId 判斷結果儲存至 ARGOCIMLOTTILEIDLIST，有資料則更新、否則新增
	/// </summary>
	public static async Task UpsertLotTileIdListAsync(IRepository repo, List<TileCheckLaserInkDto> data, string creator)
	{
		string sql = @"
		MERGE INTO ARGOCIMLOTTILEIDLIST T
		USING (
			SELECT
				:TileId AS TILEID,
				:LotNo AS LOTNO,
				:RecordDate AS RECORDDATE,
				:SourceCol AS SOURCECOL,
				:Th_TileId AS TH_TILEID,
				:Th_TileId_2 AS TH_TILEID_2,
				:ResultList AS RESULTLIST,
				:Reason AS REASON,
				:Creator AS CREATOR,
				:Opno AS OPNO
			FROM DUAL
		) D
		ON (T.TILEID = D.TILEID AND T.LOTNO = D.LOTNO)
		WHEN MATCHED THEN
			UPDATE SET
				RECORDDATE  = D.RECORDDATE,
				SOURCECOL   = CASE WHEN D.SOURCECOL IS NULL THEN T.SOURCECOL ELSE D.SOURCECOL END,
				TH_TILEID   = D.TH_TILEID,
				TH_TILEID_2 = D.TH_TILEID_2,
				RESULTLIST  = D.RESULTLIST,
				REASON      = D.REASON,
				CREATEDATE  = SYSDATE,
				CREATOR     = D.CREATOR,
				OPNO        = D.OPNO
			WHERE (D.RECORDDATE > T.RECORDDATE OR T.RECORDDATE IS NULL)
			  AND T.REASON <> 'NG'
		WHEN NOT MATCHED THEN
			INSERT (
				TILEID, LOTNO, RECORDDATE, SOURCECOL,
				TH_TILEID, TH_TILEID_2,
				RESULTLIST, REASON, CREATEDATE, CREATOR, OPNO
			)
			VALUES (
				D.TILEID, D.LOTNO, D.RECORDDATE, D.SOURCECOL,
				D.TH_TILEID, D.TH_TILEID_2,
				D.RESULTLIST, D.REASON, SYSDATE, D.CREATOR, D.OPNO
			)";

		foreach (var row in data)
		{
			await repo.ExecuteAsync(sql, new
			{
				TileId = row.TileId,
				LotNo = row.LotNo,
				RecordDate = row.RecordDate ?? DateTime.Now,
				SourceCol = row.SourceCol, // 直接傳 null 就會保留原值
				Th_TileId = row.TH_TileId ?? "",
				Th_TileId_2 = row.TH_TileId_2 ?? "",
				ResultList = row.ResultList,
				Reason = row.Reason,
				Creator = creator,
				Opno = row.OpNo ?? ""
			});
		}
	}
}
