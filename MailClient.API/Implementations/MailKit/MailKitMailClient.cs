using MailClient.API.Abstractions;
using MailKit.Net.Imap;
using MailKit;

namespace MailClient.API.Implementations.MailKit;

/// <summary>
/// MailKit implementation of IMailClient
/// </summary>
public class MailKitMailClient : IMailClient
{
    private readonly ImapClient _client;
    private bool _disposed = false;

    public MailKitMailClient()
    {
        _client = new ImapClient();
    }

    public bool IsConnected => _client.IsConnected;

    public async Task ConnectAsync(string server, int port, bool useSsl, CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(server, port, useSsl, cancellationToken);
    }

    public async Task AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await _client.AuthenticateAsync(email, password, cancellationToken);
    }

    public async Task<IMailFolder> GetInboxAsync(CancellationToken cancellationToken = default)
    {
        var inbox = _client.Inbox;
        return new MailKitMailFolder(inbox);
    }

    public async Task<IMailFolder?> GetSentFolderAsync(CancellationToken cancellationToken = default)
    {
        var sentFolder = _client.GetFolder(MailKit.SpecialFolder.Sent) 
            ?? _client.GetFolder("Sent") 
            ?? _client.GetFolder("[Gmail]/Sent Mail");

        if (sentFolder == null)
            return null;

        return new MailKitMailFolder(sentFolder);
    }

    public async Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(quit, cancellationToken);
        }
    }

    public async Task IdleAsync(CancellationToken cancellationToken)
    {
        await _client.IdleAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_client.IsConnected)
            {
                try
                {
                    _client.Disconnect(false);
                }
                catch
                {
                    // Ignore disconnect errors during disposal
                }
            }
            _client.Dispose();
            _disposed = true;
        }
    }
}

