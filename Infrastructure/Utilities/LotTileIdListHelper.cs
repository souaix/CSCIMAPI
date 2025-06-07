using Core.Entities.ArgoCim;
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
			RecordDate = x.RecordDate ?? DateTime.Now,   // 🟢 補上這行
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
	// LotTileIdListHelper.cs
	public static async Task UpsertLotTileIdListAsync(
		IRepository repo,
		List<TileCheckLaserInkDto> data,
		string creator,
		bool enableGroupSync = false)
	{
		if (data == null || !data.Any()) return;

		/* ---------- 1. 逐筆 MERGE ---------- */
		const string sql = @"
		MERGE INTO ARGOCIMLOTTILEIDLIST T
		USING (
			SELECT
				:TileId       AS TILEID,
				:LotNo        AS LOTNO,
				:RecordDate   AS RECORDDATE,
				:SourceCol    AS SOURCECOL,
				:Th_TileId    AS TH_TILEID,
				:Th_TileId_2  AS TH_TILEID_2,
				:ResultList   AS RESULTLIST,
				:Reason       AS REASON,
				:Creator      AS CREATOR,
				:Opno         AS OPNO,
				:TileGroup    AS TILEGROUP
			FROM DUAL
		) D
		ON (T.TILEID = D.TILEID AND T.LOTNO = D.LOTNO)
		WHEN MATCHED THEN
			UPDATE SET
				
				RECORDDATE  = D.RECORDDATE,
				SOURCECOL   = COALESCE(D.SOURCECOL, T.SOURCECOL),
				TH_TILEID   = D.TH_TILEID,
				TH_TILEID_2 = D.TH_TILEID_2,
				RESULTLIST  = D.RESULTLIST,
				REASON      = CASE
								 WHEN NVL(T.REASON,'') = '' THEN D.REASON
								 ELSE T.REASON || '→' || D.REASON
							  END,
				CREATEDATE  = SYSDATE,
				CREATOR     = D.CREATOR,
				OPNO        = D.OPNO
			WHERE
				
				(T.RESULTLIST = 'White' AND D.RESULTLIST = 'Black')
				OR (T.RESULTLIST = 'Black'
					AND D.RESULTLIST = 'Black'
					AND D.REASON = 'NG'
					AND NVL(T.REASON,'') <> 'NG')
		WHEN NOT MATCHED THEN
			INSERT (
				TILEID, LOTNO, RECORDDATE, SOURCECOL,
				TH_TILEID, TH_TILEID_2,
				RESULTLIST, REASON, CREATEDATE, CREATOR, OPNO, TILEGROUP
			) VALUES (
				D.TILEID, D.LOTNO, D.RECORDDATE, D.SOURCECOL,
				D.TH_TILEID, D.TH_TILEID_2,
				D.RESULTLIST, D.REASON, SYSDATE, D.CREATOR, D.OPNO, D.TILEGROUP
			)";


		foreach (var row in data)
		{

			var finalSql = sql
				.Replace(":TileId", $"'{(row.TileId ?? "").Replace("'", "''")}'")
				.Replace(":LotNo", $"'{(row.LotNo ?? "").Replace("'", "''")}'")
				.Replace(":RecordDate", row.RecordDate.HasValue
					? $"TO_DATE('{row.RecordDate.Value:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')"
					: "SYSDATE")
				.Replace(":SourceCol", $"'{(row.SourceCol ?? "").Replace("'", "''")}'")
				.Replace(":Th_TileId", $"'{(row.TH_TileId ?? "").Replace("'", "''")}'")
				.Replace(":Th_TileId_2", $"'{((row.TH_TileId_2 ?? "").Replace("'", "''"))}'")

				.Replace(":ResultList", $"'{(row.ResultList ?? "").Replace("'", "''")}'")
				.Replace(":Reason", $"'{(row.Reason ?? "").Replace("'", "''")}'")
				.Replace(":Creator", $"'{(creator ?? "").Replace("'", "''")}'")
				.Replace(":Opno", $"'{(row.OpNo ?? "").Replace("'", "''")}'")
				.Replace(":TileGroup", $"'{(row.TileGroup ?? "").Replace("'", "''")}'");


			Console.WriteLine("🟢 Final Executable SQL:");
			Console.WriteLine(finalSql);
			Console.WriteLine("-----------------------------------------------------");

			var old = await repo.QueryFirstOrDefaultAsync<DateTime?>(@"
    SELECT RECORDDATE FROM ARGOCIMLOTTILEIDLIST
    WHERE TILEID = :tileid AND LOTNO = :lotno",
				new { tileid = row.TileId, lotno = row.LotNo });

			Console.WriteLine($"[Debug RECORDDATE] TileId={row.TileId} | New={row.RecordDate} | Old={old}");


			await repo.ExecuteAsync(sql, new
			{
				TileId = row.TileId,
				LotNo = row.LotNo,
				RecordDate = row.RecordDate ?? (DateTime.Now).AddSeconds(-10), //為了不影響群組擴散，把時間-10秒
				SourceCol = row.SourceCol,
				Th_TileId = row.TH_TileId ?? "",
				Th_TileId_2 = row.TH_TileId_2 ?? "",
				ResultList = row.ResultList,
				Reason = row.Reason,
				Creator = creator,
				Opno = row.OpNo ?? "",
				TileGroup = row.TileGroup
			});
		}

		/* ---------- 2. 若啟用群組擴散，直接用一條 UPDATE 染黑整組 ---------- */
		if (enableGroupSync)
		{
			// 🟡 STEP: 計算當前資料的最大 RecordDate 作為擴散時間基準
			var now = data
				.Where(d => d.RecordDate.HasValue)
				.Select(d => d.RecordDate.Value)
				.DefaultIfEmpty(DateTime.Now)
				.Max();

			const string oneShotSql = @"
					UPDATE ARGOCIMLOTTILEIDLIST T
					SET (T.RESULTLIST, T.REASON) = (
					  SELECT
						CASE 
						  WHEN MAX(CASE WHEN B.RESULTLIST = 'White' THEN 1 ELSE 0 END) = 1
						   AND MAX(CASE WHEN B.RESULTLIST = 'Black' AND B.RECORDDATE < :now THEN 1 ELSE 0 END) = 0
						  THEN 'White'
						  ELSE 'Black'
						END,
						CASE
						  WHEN MAX(CASE WHEN B.RESULTLIST = 'White' THEN 1 ELSE 0 END) = 1
						   AND MAX(CASE WHEN B.RESULTLIST = 'Black' AND B.RECORDDATE < :now THEN 1 ELSE 0 END) = 0
						  THEN 'PASS'
						  ELSE MAX(B.REASON) KEEP (
								 DENSE_RANK FIRST 
								 ORDER BY CASE WHEN B.REASON IS NULL OR B.REASON = '' THEN 1 ELSE 0 END
							   )
						END
					  FROM ARGOCIMLOTTILEIDLIST B
					  WHERE B.TILEGROUP = T.TILEGROUP
						AND B.LOTNO = T.LOTNO
					)
					WHERE T.TILEGROUP IS NOT NULL
					  AND T.LOTNO = :lotno";

			await repo.ExecuteAsync(oneShotSql, new
			{
				lotno = data.FirstOrDefault()?.LotNo ?? "UNKNOWN",
				now = now
			});

		}
	}
}
