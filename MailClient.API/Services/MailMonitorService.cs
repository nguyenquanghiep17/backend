using System.Collections.Concurrent;
using MailClient.API.Hubs;
using MailClient.API.Models;
using MailClient.API.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace MailClient.API.Services;

public class MailMonitorService : BackgroundService
{
    private readonly ILogger<MailMonitorService> _logger;
    private readonly IHubContext<MailHub> _hubContext;
    private readonly IDistributedAccountAllocator _allocator;
    private readonly IMailClientFactory _mailClientFactory;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _knownEmailIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task> _monitorTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _accountCancellationTokens = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5); // Refresh allocation every 5 minutes

    public MailMonitorService(
        ILogger<MailMonitorService> logger,
        IHubContext<MailHub> hubContext,
        IDistributedAccountAllocator allocator,
        IMailClientFactory mailClientFactory)
    {
        _logger = logger;
        _hubContext = hubContext;
        _allocator = allocator;
        _mailClientFactory = mailClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Mail monitor started. Pod: {PodId}", _allocator.GetPodIdentifier());

        // Initial load
        await RefreshAndStartMonitorsAsync(stoppingToken);

        // Periodically refresh allocation to handle pod scaling
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
                await RefreshAndStartMonitorsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during allocation refresh");
            }
        }

        // Stop all monitors
        foreach (var cts in _accountCancellationTokens.Values)
        {
            cts.Cancel();
        }

        await Task.WhenAll(_monitorTasks.Values);
    }

    private async Task RefreshAndStartMonitorsAsync(CancellationToken stoppingToken)
    {
        var allocatedAccounts = await _allocator.GetAllocatedAccountsAsync(stoppingToken);

        if (allocatedAccounts.Count == 0)
        {
            _logger.LogWarning("No mail accounts allocated to this pod. Pod: {PodId}", _allocator.GetPodIdentifier());
            return;
        }

        _logger.LogInformation("Refreshing allocation. Pod: {PodId}, Allocated accounts: {Count}", 
            _allocator.GetPodIdentifier(), allocatedAccounts.Count);

        var currentAccountEmails = new HashSet<string>(
            allocatedAccounts.Select(a => a.Email), 
            StringComparer.OrdinalIgnoreCase);

        // Stop monitors for accounts no longer allocated to this pod
        var accountsToStop = _monitorTasks.Keys
            .Where(email => !currentAccountEmails.Contains(email))
            .ToList();

        foreach (var email in accountsToStop)
        {
            _logger.LogInformation("Stopping monitor for account {Email} (no longer allocated)", email);
            if (_accountCancellationTokens.TryRemove(email, out var cts))
            {
                cts.Cancel();
            }
            _monitorTasks.TryRemove(email, out _);
        }

        // Start/restart monitors for allocated accounts
        foreach (var account in allocatedAccounts)
        {
            if (_monitorTasks.ContainsKey(account.Email))
            {
                // Already monitoring, skip
                continue;
            }

            var accountCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _accountCancellationTokens[account.Email] = accountCts;

            var monitorTask = MonitorAccountAsync(account, accountCts.Token);
            _monitorTasks[account.Email] = monitorTask;

            _logger.LogInformation("Started monitor for account {Email}", account.Email);
        }
    }

    private async Task MonitorAccountAsync(MailAccount account, CancellationToken cancellationToken)
    {
        var knownIds = _knownEmailIds.GetOrAdd(account.Email, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        while (!cancellationToken.IsCancellationRequested)
        {
            using var client = _mailClientFactory.CreateClient();
            try
            {
                _logger.LogInformation("Connecting IMAP IDLE for account {Email}", account.Email);

                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl, cancellationToken);
                await client.AuthenticateAsync(account.Email, account.Password, cancellationToken);

                var inbox = await client.GetInboxAsync();
                await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                // Seed known messages to avoid notifying existing items
                if (knownIds.IsEmpty)
                {
                    var existingUids = await inbox.SearchAllAsync(cancellationToken);
                    foreach (var uid in existingUids.TakeLast(50))
                    {
                        knownIds.TryAdd(uid, 0);
                    }

                    if (existingUids.Count > 0)
                    {
                        _logger.LogInformation("Seeded {Count} existing emails for account {Email}", Math.Min(existingUids.Count, 50), account.Email);
                    }
                }

                CancellationTokenSource? idleDone = null;

                void OnCountChanged(object? sender, EventArgs args)
                {
                    // Wake the IDLE loop; we'll query for new UIDs afterwards
                    idleDone?.Cancel();
                }

                inbox.CountChanged += OnCountChanged;

                try
                {
                    while (!cancellationToken.IsCancellationRequested && client.IsConnected)
                    {
                        idleDone = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        idleDone.CancelAfter(TimeSpan.FromMinutes(9));

                        try
                        {
                            await client.IdleAsync(idleDone.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Expected when a message arrives or when the keep-alive timer fires
                        }
                        finally
                        {
                            idleDone.Dispose();
                            idleDone = null;
                        }

                        if (!client.IsConnected)
                        {
                            break;
                        }

                        await inbox.CheckAsync(cancellationToken);

                        // After IDLE breaks, look for newly appeared UIDs and process them
                        var allUids = await inbox.SearchAllAsync(cancellationToken);
                        foreach (var uid in allUids.TakeLast(10)) // only scan a small tail
                        {
                            if (!knownIds.TryAdd(uid, 0))
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
                    if (client.IsConnected)
                    {
                        await client.DisconnectAsync(true, cancellationToken);
                    }
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

    private async Task ProcessMessageAsync(IMailFolder inbox, string accountEmail, string uid, ConcurrentDictionary<string, byte> knownIds, CancellationToken cancellationToken)
    {
        // Note: knownIds.TryAdd was already called in the caller, this is just a safety check
        if (!knownIds.ContainsKey(uid))
        {
            knownIds.TryAdd(uid, 0);
        }

        EmailMessage? email = null;
        try
        {
            email = await inbox.GetMessageAsync(uid, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch new email {Uid} for {Email}", uid, accountEmail);
            knownIds.TryRemove(uid, out _);
            return;
        }

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

