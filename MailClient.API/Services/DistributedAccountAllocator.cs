using System.Security.Cryptography;
using System.Text;
using MailClient.API.Models;

namespace MailClient.API.Services;

public class DistributedAccountAllocator : IDistributedAccountAllocator
{
    private readonly IMailAccountRepository _repository;
    private readonly ILogger<DistributedAccountAllocator> _logger;
    private readonly string _podIdentifier;
    private readonly int _totalPods;

    public DistributedAccountAllocator(
        IMailAccountRepository repository,
        ILogger<DistributedAccountAllocator> logger)
    {
        _repository = repository;
        _logger = logger;

        // Get pod identifier from environment variable or use hostname
        _podIdentifier = Environment.GetEnvironmentVariable("POD_NAME") 
            ?? Environment.GetEnvironmentVariable("HOSTNAME") 
            ?? Environment.MachineName;

        // Get total pod count from environment variable (default to 1)
        var podCountEnv = Environment.GetEnvironmentVariable("POD_COUNT");
        _totalPods = int.TryParse(podCountEnv, out var count) && count > 0 ? count : 1;

        _logger.LogInformation("DistributedAccountAllocator initialized. Pod: {PodId}, Total Pods: {TotalPods}", 
            _podIdentifier, _totalPods);
    }

    public string GetPodIdentifier() => _podIdentifier;

    public async Task<List<MailAccount>> GetAllocatedAccountsAsync(CancellationToken cancellationToken = default)
    {
        var allAccounts = await _repository.GetAllAccountsAsync(cancellationToken);
        
        if (allAccounts.Count == 0)
        {
            _logger.LogWarning("No mail accounts found in database");
            return new List<MailAccount>();
        }

        var allocatedAccounts = new List<MailAccount>();

        foreach (var dbAccount in allAccounts)
        {
            if (string.IsNullOrWhiteSpace(dbAccount.Email))
                continue;

            // Hash account ID + pod identifier to determine allocation
            var accountKey = $"{dbAccount.Id}_{dbAccount.Email}";
            var hash = ComputeHash(accountKey);
            var podIndex = Math.Abs(hash) % _totalPods;
            var podIndexForThisPod = GetPodIndex(_podIdentifier, _totalPods);

            if (podIndex == podIndexForThisPod)
            {
                var mailAccount = new MailAccount
                {
                    Email = dbAccount.Email!,
                    Password = dbAccount.Password ?? string.Empty,
                    ImapServer = dbAccount.ImapServer ?? string.Empty,
                    ImapPort = dbAccount.ImapPort ?? 993,
                    SmtpServer = dbAccount.SmtpServer ?? string.Empty,
                    SmtpPort = dbAccount.SmtpPort ?? 587,
                    UseSsl = dbAccount.UseSsl ?? true
                };

                allocatedAccounts.Add(mailAccount);
                _logger.LogDebug("Allocated account {Email} to pod {PodId}", dbAccount.Email, _podIdentifier);
            }
        }

        _logger.LogInformation("Allocated {Count}/{Total} accounts to pod {PodId}", 
            allocatedAccounts.Count, allAccounts.Count, _podIdentifier);

        return allocatedAccounts;
    }

    private static int ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToInt32(hashBytes, 0);
    }

    private static int GetPodIndex(string podIdentifier, int totalPods)
    {
        var hash = ComputeHash(podIdentifier);
        return Math.Abs(hash) % totalPods;
    }
}

