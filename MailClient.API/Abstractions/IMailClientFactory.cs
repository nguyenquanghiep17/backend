namespace MailClient.API.Abstractions;

/// <summary>
/// Factory for creating IMailClient instances.
/// This allows for dependency injection and easy swapping of implementations.
/// </summary>
public interface IMailClientFactory
{
    /// <summary>
    /// Creates a new mail client instance
    /// </summary>
    IMailClient CreateClient();
}

