
using System.Data;

namespace Core.Entities.LaserMarking;

public class CustomerConfig
{
	public string? Customer { get; set; }
	public int? SnLength { get; set; }
	public string? CharacterEncode { get; set; }
	public string? CharacterExclude { get; set; }
	public string? SelectLastSn { get; set; }
	public string? NewSnPattern { get; set; }

    // 新增的四個欄位
    public string? UseConfigTileIdPrefix { get; set; }
    public string? UseDateCodeMapping { get; set; }
    public string? CompareCreateDate { get; set; }
    public string? EnableFlag { get; set; }

}
