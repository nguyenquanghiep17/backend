using MailClient.API.Models;

namespace MailClient.API.Services;

public interface IMailAccountRepository
{
    Task<List<ManagementConfigMail>> GetAllAccountsAsync(CancellationToken cancellationToken = default);
}

