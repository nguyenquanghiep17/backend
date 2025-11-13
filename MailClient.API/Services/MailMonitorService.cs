using System.Collections.Concurrent;
using System.Threading.Channels;
using MailClient.API.Hubs;
using MailClient.API.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MailClient.API.Services;

public class MailMonitorService : BackgroundService
{
    private readonly ILogger<MailMonitorService> _logger;
    private readonly IHubContext<MailHub> _hubContext;
    private readonly MailSettings _mailSettings;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _knownEmailIds = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    public MailMonitorService(
        ILogger<MailMonitorService> logger,
        IHubContext<MailHub> hubContext,
        IOptions<MailSettings> mailSettings)
    {
        _logger = logger;
        _hubContext = hubContext;
        _mailSettings = mailSettings.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_mailSettings.Accounts.Count == 0)
        {
            _logger.LogWarning("No mail accounts configured. Mail monitor will not run.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Mail monitor started for {Count} account(s).", _mailSettings.Accounts.Count);

        var monitorTasks = _mailSettings.Accounts
            .Select(account => MonitorAccountAsync(account, stoppingToken))
            .ToArray();

        return Task.WhenAll(monitorTasks);
    }

    private async Task MonitorAccountAsync(MailAccount account, CancellationToken cancellationToken)
    {
        var knownIds = _knownEmailIds.GetOrAdd(account.Email, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        while (!cancellationToken.IsCancellationRequested)
        {
            using var client = new ImapClient();
            try
            {
                _logger.LogInformation("Connecting IMAP IDLE for account {Email}", account.Email);

                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl, cancellationToken);
                await client.AuthenticateAsync(account.Email, account.Password, cancellationToken);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                // Seed known messages to avoid notifying existing items
                if (knownIds.IsEmpty)
                {
                    var existingUids = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
                    foreach (var uid in existingUids.TakeLast(50))
                    {
                        knownIds.TryAdd(uid.ToString(), 0);
                    }

                    if (existingUids.Count > 0)
                    {
                        _logger.LogInformation("Seeded {Count} existing emails for account {Email}", Math.Min(existingUids.Count, 50), account.Email);
                    }
                }

                CancellationTokenSource? idleCts = null;

                void OnCountChanged(object? sender, EventArgs args)
                {
                    // Wake the IDLE loop; we'll query for new UIDs afterwards
                    idleCts?.Cancel();
                }

                inbox.CountChanged += OnCountChanged;

                try
                {
                    while (!cancellationToken.IsCancellationRequested && client.IsConnected)
                    {
                        idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        idleCts.CancelAfter(TimeSpan.FromMinutes(9));

                        try
                        {
                            await inbox.IdleAsync(idleCts.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Expected when a message arrives or when the keep-alive timer fires
                        }
                        finally
                        {
                            idleCts.Dispose();
                            idleCts = null;
                        }

                        if (!client.IsConnected)
                        {
                            break;
                        }

                        await inbox.CheckAsync(cancellationToken);

                        // After IDLE breaks, look for newly appeared UIDs and process them
                        var allUids = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
                        foreach (var uid in allUids.TakeLast(10)) // only scan a small tail
                        {
                            var idString = uid.ToString();
                            if (!knownIds.TryAdd(idString, 0))
                                continue;

                            await ProcessMessageAsync(inbox, account.Email, uid, knownIds, cancellationToken);
                        }
                    }
                }
                finally
                {
                    inbox.CountChanged -= OnCountChanged;
                }

                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Stopping IMAP monitor for account {Email}", account.Email);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IMAP monitor error for account {Email}. Reconnecting...", account.Email);

                try
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
                catch
                {
                    // Ignore disconnect errors
                }

                try
                {
                    await Task.Delay(ReconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessMessageAsync(IMailFolder inbox, string accountEmail, UniqueId uid, ConcurrentDictionary<string, byte> knownIds, CancellationToken cancellationToken)
    {
        var idString = uid.ToString();
        if (!knownIds.TryAdd(idString, 0))
        {
            return;
        }

        MimeMessage? message = null;
        try
        {
            message = await inbox.GetMessageAsync(uid, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch new email {Uid} for {Email}", uid, accountEmail);
            knownIds.TryRemove(idString, out _);
            return;
        }

        var email = EmailMapper.ToEmailMessage(message, idString);
        await NotifyNewEmailAsync(accountEmail, email, cancellationToken);
    }

    private async Task NotifyNewEmailAsync(string accountEmail, EmailMessage email, CancellationToken cancellationToken)
    {
        _logger.LogInformation("New email detected for {Email}: {Subject}", accountEmail, email.Subject);

        var payload = new
        {
            account = accountEmail,
            email
        };

        await _hubContext.Clients.Group(accountEmail).SendAsync("NewEmail", payload, cancellationToken);
    }
}

