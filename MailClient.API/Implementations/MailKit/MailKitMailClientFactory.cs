using MailClient.API.Abstractions;

namespace MailClient.API.Implementations.MailKit;

/// <summary>
/// Factory for creating MailKit mail clients
/// </summary>
public class MailKitMailClientFactory : IMailClientFactory
{
    public IMailClient CreateClient()
    {
        return new MailKitMailClient();
    }
}

