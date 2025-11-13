using System.Collections.Concurrent;
using MailClient.API.Hubs;
using MailClient.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MailClient.API.Services;

public class MailMonitorService : BackgroundService
{
    private readonly ILogger<MailMonitorService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<MailHub> _hubContext;
    private readonly MailSettings _mailSettings;
    private readonly ConcurrentDictionary<string, HashSet<string>> _knownEmailIds = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    public MailMonitorService(
        ILogger<MailMonitorService> logger,
        IServiceScopeFactory scopeFactory,
        IHubContext<MailHub> hubContext,
        IOptions<MailSettings> mailSettings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _mailSettings = mailSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_mailSettings.Accounts.Count == 0)
        {
            _logger.LogWarning("No mail accounts configured. Mail monitor will not run.");
            return;
        }

        _logger.LogInformation("Mail monitor started for {Count} account(s).", _mailSettings.Accounts.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var account in _mailSettings.Accounts)
                {
                    await CheckAccountAsync(account, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown, rethrow to exit loop
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while monitoring mail accounts.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Mail monitor stopped.");
    }

    private async Task CheckAccountAsync(MailAccount account, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();

        List<EmailMessage> emails;
        try
        {
            emails = await mailService.GetInboxEmailsAsync(account.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull emails for account {Email}", account.Email);
            return;
        }

        if (emails.Count == 0)
        {
            _knownEmailIds.TryAdd(account.Email, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return;
        }

        var knownIds = _knownEmailIds.GetOrAdd(account.Email, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var isInitialization = knownIds.Count == 0;

        foreach (var email in emails.OrderBy(e => e.Date))
        {
            if (!knownIds.Add(email.Id))
                continue;

            if (isInitialization)
            {
                // Seed existing emails without notifying clients
                continue;
            }

            await NotifyNewEmailAsync(account.Email, email, cancellationToken);
        }
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


