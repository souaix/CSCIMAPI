using System.ComponentModel.DataAnnotations;

namespace Core.Entities.LaserMarkingFrontend
{
	public class LaserMarkingFrontendConfig
	{
		[Key]
		public string? Item { get; set; }                  // 對照表類型: Machine / Year / Month / Day
		[Key]
		public string? Source { get; set; }                  // 對照表 key ex.LS-028
		public string? Transfer { get; set; }                  // 對照表 value ex.1
		public DateTime? CreateDate { get; set; }           // 建立日期
		public string? Creator { get; set; }                  // 建立者
		public DateTime? UpdateDate { get; set; }                  // 更新日期
		public string? Updater { get; set; }                  // 更新人
	}
}
