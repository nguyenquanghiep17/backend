using MailClient.API.Models;

namespace MailClient.API.Abstractions;

/// <summary>
/// Abstraction for mail client operations.
/// This interface allows swapping mail library implementations without affecting business logic.
/// </summary>
public interface IMailClient : IDisposable
{
    /// <summary>
    /// Connects to the mail server
    /// </summary>
    Task ConnectAsync(string server, int port, bool useSsl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates with the mail server
    /// </summary>
    Task AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the inbox folder
    /// </summary>
    Task<IMailFolder> GetInboxAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sent folder
    /// </summary>
    Task<IMailFolder?> GetSentFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the mail server
    /// </summary>
    Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the client is connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts IDLE mode to wait for new messages
    /// </summary>
    Task IdleAsync(CancellationToken cancellationToken);
}

