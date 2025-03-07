
namespace Core.Entities.LaserMarking;
public class Product
{
	public string LotNO { get; set; } = string.Empty;
	public string? Customer { get; set; }
	public string? ProductName { get; set; }
	public string? TileID { get; set; }
	public string? TileIDEnd { get; set; }
	public string? LastSN { get; set; }
	public int? Quantity { get; set; }
	public string? RuleFile1 { get; set; }
	public string? RuleFile2 { get; set; }
	public string? RuleFile3 { get; set; }
	public string? CreateDate { get; set; }
	public string? CreateTime { get; set; }
	public string? NoteData { get; set; }
}

