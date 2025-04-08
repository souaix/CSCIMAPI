using Core.Entities.MailSender;
using Core.Entities.Public;
using Core.Interfaces;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Infrastructure.Services
{
	public class MailSenderService : IMailSenderService
	{
		private readonly IRepositoryFactory _repositoryFactory;
		private readonly ILogger<MailSenderService> _logger;

		public MailSenderService(IRepositoryFactory repositoryFactory, ILogger<MailSenderService> logger)
		{
			_repositoryFactory = repositoryFactory;
			_logger = logger;
		}

		public async Task<ApiReturn<bool>> SendEmailAsync(MailSenderRequest request)
		{
			var notifyGroup = request.NOTIFYGROUP;
			var title = request.TITLE;
			var message = request.CONTEXT;

			var repository = _repositoryFactory.CreateRepository(request.Environment);

			// 檢查 MAIL 通知開關
			var mailSwitch = await repository.QueryFirstOrDefaultAsync<string>(
				"SELECT MAIL FROM ARGOCIMNOTIFYCONFIG WHERE NOTIFYGROUP = :notifyGroup",
				new { notifyGroup });

			if ((mailSwitch?.Trim() ?? "") != "1")
			{
				_logger.LogWarning("⚠ Mail 告警已關閉，NOTIFYGROUP={notifyGroup}");
				return ApiReturn<bool>.Failure("Teams disabled", false);
			}


			// 查詢收件人
			var recipients = await repository.QueryAsync<string>(
				"SELECT EM_EMAIL FROM ARGOCIMNOTIFYGROUPLIST WHERE NOTIFYGROUP = :notifyGroup",
				new { notifyGroup });

			if (recipients == null || !recipients.Any())
			{
				_logger.LogWarning("⚠ 找不到收件人: " + notifyGroup);
				return ApiReturn<bool>.Failure("No recipients found", false);
			}

			try
			{
				using var smtpClient = new SmtpClient("bdrelay.theil.com")
				{
					Port = 25,
					EnableSsl = false
				};

				var mailMessage = new MailMessage
				{
					From = new MailAddress("CIM.ROBOT@theil.com"),
					Subject = title,
					IsBodyHtml = true
				};

				// 預設 HTML 內容視圖
				AlternateView htmlView = AlternateView.CreateAlternateViewFromString(message, Encoding.UTF8, MediaTypeNames.Text.Html);

				// 嵌入圖片
				foreach (var image in request.INLINEIMAGES)
				{
					byte[] imageBytes = Convert.FromBase64String(image.Value);
					MemoryStream imageStream = new MemoryStream(imageBytes);
					LinkedResource linkedImage = new LinkedResource(imageStream, MediaTypeNames.Image.Jpeg)
					{
						ContentId = image.Key,
						TransferEncoding = TransferEncoding.Base64
					};
					htmlView.LinkedResources.Add(linkedImage);
				}

				mailMessage.AlternateViews.Add(htmlView);
				mailMessage.Body = message;

				// 附件
				foreach (var filePath in request.ATTACHMENTS)
				{
					if (File.Exists(filePath))
					{
						mailMessage.Attachments.Add(new Attachment(filePath));
					}
					else
					{
						_logger.LogWarning($"❌ 找不到附件檔案: {filePath}");
					}
				}

				foreach (var recipient in recipients)
				{
					mailMessage.To.Add(recipient);
				}

				await smtpClient.SendMailAsync(mailMessage);
				_logger.LogInformation("✅ Email sent successfully");
				return ApiReturn<bool>.Success("Email sent successfully", true);
			}
			catch (Exception ex)
			{
				_logger.LogError("❌ Mail sending error: " + ex.Message);
				return ApiReturn<bool>.Failure(ex.Message, false);
			}
		}
	}
}
