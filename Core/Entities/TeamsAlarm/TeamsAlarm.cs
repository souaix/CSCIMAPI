namespace Core.Entities.TeamsAlarm
{
	public class TeamsAlarmRequest
	{
		public string Uri { get; set; }
		public string Message { get; set; }		
	}

	public class TeamsAlarmByGroupRequest
	{
		public string Environment { get; set; }
		public string NotifyGroup { get; set; }
		public string Message { get; set; }
	}

}
