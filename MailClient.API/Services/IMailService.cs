using MailClient.API.Models;

namespace MailClient.API.Services;

public interface IMailService
{
    Task<List<EmailMessage>> GetInboxEmailsAsync(string accountEmail);
    Task<List<EmailMessage>> GetSentEmailsAsync(string accountEmail);
    Task<EmailMessage?> GetEmailByIdAsync(string accountEmail, string emailId);
}

