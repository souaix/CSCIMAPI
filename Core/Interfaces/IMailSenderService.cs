using Core.Entities.TeamsAlarm;
using Core.Entities.MailSender;
using Core.Entities.Public;
using System.Threading.Tasks;

namespace Core.Interfaces
{
	public interface IMailSenderService
	{
		Task<ApiReturn<bool>> SendEmailAsync(MailSenderRequest request);
	}
}
