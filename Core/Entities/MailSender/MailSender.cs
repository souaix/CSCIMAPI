namespace Core.Entities.MailSender
{
	public class MailSenderRequest
	{
		public string Environment { get; set; }
		public string NOTIFYGROUP { get; set; }
		public string TITLE { get; set; }
		public string CONTEXT { get; set; }
		public List<string> ATTACHMENTS { get; set; } = new List<string>();
		public Dictionary<string, string> INLINEIMAGES { get; set; } = new Dictionary<string, string>();
	}
}
