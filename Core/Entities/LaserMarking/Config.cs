
using System.Data;

namespace Core.Entities.LaserMarking;

public class Config
{
	public string? Config_Name { get; set; }
	public string? Customer { get; set; }
	public int? Side { get; set; }
	public int? Block_Qty { get; set; }
	public int? Panel_Qty { get; set; }
	public string? Year_Code { get; set; }
	public string? Month_Code { get; set; }
	public string? Day_Code { get; set; }
	public string? TileId { get; set; }
	public string? Top_TileText01 { get; set; }
	public string? Top_TileText02 { get; set; }
	public string? Top_TileText03 { get; set; }
	public string? Top_TileText04 { get; set; }
	public string? Top_TileText05 { get; set; }
	public string? Top_CellText01 { get; set; }
	public string? Top_CellText02 { get; set; }
	public string? Top_CellText03 { get; set; }
	public string? Top_CellText04 { get; set; }
	public string? Top_CellText05 { get; set; }
	public string? Top_RuleFile1 { get; set; }
	public string? Top_RuleFile2 { get; set; }
	public string? Top_RuleFile3 { get; set; }
	public string? Top_CellDirection { get; set; }
	public string? Back_TileText01 { get; set; }
	public string? Back_TileText02 { get; set; }
	public string? Back_TileText03 { get; set; }
	public string? Back_TileText04 { get; set; }
	public string? Back_TileText05 { get; set; }
	public string? Back_CellText01 { get; set; }
	public string? Back_CellText02 { get; set; }
	public string? Back_CellText03 { get; set; }
	public string? Back_CellText04 { get; set; }
	public string? Back_CellText05 { get; set; }
	public string? Back_RuleFile1 { get; set; }
	public string? Back_RuleFile2 { get; set; }
	public string? Back_RuleFile3 { get; set; }
	public string? Back_CellDirection { get; set; }
	public string? CreateDate { get; set; }
	public string? CreateTime { get; set; }
}

public static class ConfigMapper
{
	/// <summary>
	/// 從 IDataReader 映射到 Config 對象
	/// </summary>
	/// <param name="reader">數據庫讀取器</param>
	/// <returns>映射後的 Config 對象</returns>
	public static Config MapFromReader(IDataReader reader)
	{
		return new Config
		{
			Config_Name = reader["CONFIG_NAME"] as string,
			Customer = reader["CUSTOMER"] as string,
			Side = reader["SIDE"] as int?,
			Block_Qty = reader["BLOCK_QTY"] as int?,
			Panel_Qty = reader["PANEL_QTY"] as int?,
			Year_Code = reader["YEAR_CODE"] as string,
			Month_Code = reader["MONTH_CODE"] as string,
			Day_Code = reader["DAY_CODE"] as string,
			TileId = reader["TILEID"] as string,
			Top_TileText01 = reader["TOP_TILETEXT01"] as string,
			Top_TileText02 = reader["TOP_TILETEXT02"] as string,
			Top_TileText03 = reader["TOP_TILETEXT03"] as string,
			Top_TileText04 = reader["TOP_TILETEXT04"] as string,
			Top_TileText05 = reader["TOP_TILETEXT05"] as string,
			Top_CellText01 = reader["TOP_CELLTEXT01"] as string,
			Top_CellText02 = reader["TOP_CELLTEXT02"] as string,
			Top_CellText03 = reader["TOP_CELLTEXT03"] as string,
			Top_CellText04 = reader["TOP_CELLTEXT04"] as string,
			Top_CellText05 = reader["TOP_CELLTEXT05"] as string,
			Top_RuleFile1 = reader["TOP_RULEFILE1"] as string,
			Top_RuleFile2 = reader["TOP_RULEFILE2"] as string,
			Top_RuleFile3 = reader["TOP_RULEFILE3"] as string,
			Top_CellDirection = reader["TOP_CELLDIRECTION"] as string,
			Back_TileText01 = reader["BACK_TILETEXT01"] as string,
			Back_TileText02 = reader["BACK_TILETEXT02"] as string,
			Back_TileText03 = reader["BACK_TILETEXT03"] as string,
			Back_TileText04 = reader["BACK_TILETEXT04"] as string,
			Back_TileText05 = reader["BACK_TILETEXT05"] as string,
			Back_CellText01 = reader["BACK_CELLTEXT01"] as string,
			Back_CellText02 = reader["BACK_CELLTEXT02"] as string,
			Back_CellText03 = reader["BACK_CELLTEXT03"] as string,
			Back_CellText04 = reader["BACK_CELLTEXT04"] as string,
			Back_CellText05 = reader["BACK_CELLTEXT05"] as string,
			Back_RuleFile1 = reader["BACK_RULEFILE1"] as string,
			Back_RuleFile2 = reader["BACK_RULEFILE2"] as string,
			Back_RuleFile3 = reader["BACK_RULEFILE3"] as string,
			Back_CellDirection = reader["BACK_CELLDIRECTION"] as string,
			CreateDate = reader["CREATEDATE"] as string,
			CreateTime = reader["CREATETIME"] as string
		};
	}
}
