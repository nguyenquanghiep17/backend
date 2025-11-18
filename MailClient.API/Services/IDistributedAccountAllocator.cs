using MailClient.API.Models;

namespace MailClient.API.Services;

public interface IDistributedAccountAllocator
{
    Task<List<MailAccount>> GetAllocatedAccountsAsync(CancellationToken cancellationToken = default);
    string GetPodIdentifier();
}

