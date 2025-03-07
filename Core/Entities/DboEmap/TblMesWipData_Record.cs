using System.ComponentModel.DataAnnotations;
namespace Core.Entities.DboEmap
{
	public class TblMesWipData_Record
	{
		// 必填屬性（不允許為 null）
		public DateTime RecordDate { get; set; }
		public string DeviceId { get; set; }
		public int DataIndex { get; set; }
		public string Status { get; set; }
		public string AlarmCode { get; set; } = "NA";

		// 選填屬性（允許為 null）
		
		public DateTime? OracleDate { get; set; } = null;
		
		public string Process { get; set; } = null;
		
		public string Step { get; set; } = null;
		
		public int? StepOrder { get; set; } = null;
		
		public string LotSerial { get; set; } = null;
		
		public string UserId { get; set; } = null;
		
		public string PartNo { get; set; } = null;

		
		public string PartNoRev { get; set; } = null;

		
		public string LotNo { get; set; } = null;

		
		public string MbomNo { get; set; } = null;

		
		public string TileId { get; set; } = null;

		
		public int? TileTotalQty { get; set; } = null;

		
		public int? TileInQty { get; set; } = null;

		
		public int? TileOutQty { get; set; } = null;

		
		public int? CellInQty { get; set; } = null;

		
		public int? CellOutQty { get; set; } = null;

		
		public int? ArrayQty { get; set; } = null;

		
		public string FixtureId01 { get; set; } = null;

		
		public string FixtureId02 { get; set; } = null;

		
		public string RackId { get; set; } = null;

		
		public string CassetteId { get; set; } = null;

		
		public string RecipeName { get; set; } = null;

		
		public int? SerialNum { get; set; } = null;

		
		public string ServerId { get; set; } = null;

		
		public string AlarmMessage { get; set; } = "NA";

		
		public string AlarmStatus { get; set; } = null;

		
		public string CsType { get; set; } = null;

		
		public string DeviceId1 { get; set; } = null;

		// 通用屬性 V001 到 V100
		 public string? V001 { get; set; } = null;
		 public string? V002 { get; set; } = null;
		 public string? V003 { get; set; } = null;
		 public string? V004 { get; set; } = null;
		 public string? V005 { get; set; } = null;
		 public string? V006 { get; set; } = null;
		 public string? V007 { get; set; } = null;
		 public string? V008 { get; set; } = null;
		 public string? V009 { get; set; } = null;
		 public string? V010 { get; set; } = null;
		 public string? V011 { get; set; } = null;
		 public string? V012 { get; set; } = null;
		 public string? V013 { get; set; } = null;
		 public string? V014 { get; set; } = null;
		 public string? V015 { get; set; } = null;
		 public string? V016 { get; set; } = null;
		 public string? V017 { get; set; } = null;
		 public string? V018 { get; set; } = null;
		 public string? V019 { get; set; } = null;
		 public string? V020 { get; set; } = null;
		 public string? V021 { get; set; } = null;
		 public string? V022 { get; set; } = null;
		 public string? V023 { get; set; } = null;
		 public string? V024 { get; set; } = null;
		 public string? V025 { get; set; } = null;
		 public string? V026 { get; set; } = null;
		 public string? V027 { get; set; } = null;
		 public string? V028 { get; set; } = null;
		 public string? V029 { get; set; } = null;
		 public string? V030 { get; set; } = null;
		 public string? V031 { get; set; } = null;
		 public string? V032 { get; set; } = null;
		 public string? V033 { get; set; } = null;
		 public string? V034 { get; set; } = null;
		 public string? V035 { get; set; } = null;
		 public string? V036 { get; set; } = null;
		 public string? V037 { get; set; } = null;
		 public string? V038 { get; set; } = null;
		 public string? V039 { get; set; } = null;
		 public string? V040 { get; set; } = null;
		 public string? V041 { get; set; } = null;
		 public string? V042 { get; set; } = null;
		 public string? V043 { get; set; } = null;
		 public string? V044 { get; set; } = null;
		 public string? V045 { get; set; } = null;
		 public string? V046 { get; set; } = null;
		 public string? V047 { get; set; } = null;
		 public string? V048 { get; set; } = null;
		 public string? V049 { get; set; } = null;
		 public string? V050 { get; set; } = null;
		 public string? V051 { get; set; } = null;
		 public string? V052 { get; set; } = null;
		 public string? V053 { get; set; } = null;
		 public string? V054 { get; set; } = null;
		 public string? V055 { get; set; } = null;
		 public string? V056 { get; set; } = null;
		 public string? V057 { get; set; } = null;
		 public string? V058 { get; set; } = null;
		 public string? V059 { get; set; } = null;
		 public string? V060 { get; set; } = null;
		 public string? V061 { get; set; } = null;
		 public string? V062 { get; set; } = null;
		 public string? V063 { get; set; } = null;
		 public string? V064 { get; set; } = null;
		 public string? V065 { get; set; } = null;
		 public string? V066 { get; set; } = null;
		 public string? V067 { get; set; } = null;
		 public string? V068 { get; set; } = null;
		 public string? V069 { get; set; } = null;
		 public string? V070 { get; set; } = null;
		 public string? V071 { get; set; } = null;
		 public string? V072 { get; set; } = null;
		 public string? V073 { get; set; } = null;
		 public string? V074 { get; set; } = null;
		 public string? V075 { get; set; } = null;
		 public string? V076 { get; set; } = null;
		 public string? V077 { get; set; } = null;
		 public string? V078 { get; set; } = null;
		 public string? V079 { get; set; } = null;
		 public string? V080 { get; set; } = null;
		 public string? V081 { get; set; } = null;
		 public string? V082 { get; set; } = null;
		 public string? V083 { get; set; } = null;
		 public string? V084 { get; set; } = null;
		 public string? V085 { get; set; } = null;
		 public string? V086 { get; set; } = null;
		 public string? V087 { get; set; } = null;
		 public string? V088 { get; set; } = null;
		 public string? V089 { get; set; } = null;
		 public string? V090 { get; set; } = null;
		 public string? V091 { get; set; } = null;
		 public string? V092 { get; set; } = null;
		 public string? V093 { get; set; } = null;
		 public string? V094 { get; set; } = null;
		 public string? V095 { get; set; } = null;
		 public string? V096 { get; set; } = null;
		 public string? V097 { get; set; } = null;
		 public string? V098 { get; set; } = null;
		 public string? V099 { get; set; } = null;
		 public string? V100 { get; set; } = null;
	}
}
