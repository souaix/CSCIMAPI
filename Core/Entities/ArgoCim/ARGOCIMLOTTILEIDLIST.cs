namespace Core.Entities.ArgoCim
{
	public class ARGOCIMLOTTILEIDLIST
	{
		public string TileId { get; set; }           // TILEID - VARCHAR2(50)
		public string LotNo { get; set; }            // LOTNO - VARCHAR2(50)
		public string OpNo { get; set; }             // OPNO - VARCHAR2(50)
		public DateTime? RecordDate { get; set; }    // RECORDDATE - DATE
		public string SourceCol { get; set; }        // SOURCECOL - VARCHAR2(50)
		public string Th_TileId { get; set; }        // TH_TILEID - VARCHAR2(50)
		public string Th_TileId_2 { get; set; }      // TH_TILEID_2 - VARCHAR2(50)
		public string ResultList { get; set; }       // RESULTLIST - VARCHAR2(10)
		public string Reason { get; set; }           // REASON - VARCHAR2(30)
		public DateTime? CreateDate { get; set; }    // CREATEDATE - DATE
		public string Creator { get; set; }          // CREATOR - VARCHAR2(30)
		public DateTime? UpdateDate { get; set; }    // CREATEDATE - DATE
		public string Updater { get; set; }          // CREATOR - VARCHAR2(30)
		public string TileGroup { get; set; }
	}
}
