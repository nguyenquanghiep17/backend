using MailClient.API.Models;
using MailClient.API.Abstractions;

namespace MailClient.API.Services;

public class MailService : IMailService
{
    private readonly IMailAccountRepository _repository;
    private readonly IMailClientFactory _mailClientFactory;

    public MailService(IMailAccountRepository repository, IMailClientFactory mailClientFactory)
    {
        _repository = repository;
        _mailClientFactory = mailClientFactory;
    }

    private async Task<MailAccount?> GetAccountAsync(string email)
    {
        var allAccounts = await _repository.GetAllAccountsAsync();
        var dbAccount = allAccounts.FirstOrDefault(a => 
            a.Email != null && a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        if (dbAccount == null)
            return null;

        return new MailAccount
        {
            Email = dbAccount.Email!,
            Password = dbAccount.Password ?? string.Empty,
            ImapServer = dbAccount.ImapServer ?? string.Empty,
            ImapPort = dbAccount.ImapPort ?? 993,
            SmtpServer = dbAccount.SmtpServer ?? string.Empty,
            SmtpPort = dbAccount.SmtpPort ?? 587,
            UseSsl = dbAccount.UseSsl ?? true
        };
    }

    public async Task<List<EmailMessage>> GetInboxEmailsAsync(string accountEmail)
    {
        var account = await GetAccountAsync(accountEmail);
        if (account == null)
            throw new Exception($"Account {accountEmail} not found");

        var emails = new List<EmailMessage>();

        using var client = _mailClientFactory.CreateClient();
        try
        {
            await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl);
            await client.AuthenticateAsync(account.Email, account.Password);

            var inbox = await client.GetInboxAsync();
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            // Lấy 50 email mới nhất
            var uids = await inbox.SearchAllAsync();
            var items = uids.TakeLast(50).ToList();

            foreach (var uid in items)
            {
                var message = await inbox.GetMessageAsync(uid);
                emails.Add(message);
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching inbox emails: {ex.Message}", ex);
        }

        return emails.OrderByDescending(e => e.Date).ToList();
    }

    public async Task<List<EmailMessage>> GetSentEmailsAsync(string accountEmail)
    {
        var account = await GetAccountAsync(accountEmail);
        if (account == null)
            throw new Exception($"Account {accountEmail} not found");

        var emails = new List<EmailMessage>();

        using var client = _mailClientFactory.CreateClient();
        try
        {
            await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl);
            await client.AuthenticateAsync(account.Email, account.Password);

            var sentFolder = await client.GetSentFolderAsync();
            if (sentFolder == null)
                return emails;

            await sentFolder.OpenAsync(FolderAccess.ReadOnly);

            // Lấy 50 email mới nhất
            var uids = await sentFolder.SearchAllAsync();
            var items = uids.Take(50).ToList();

            foreach (var uid in items)
            {
                var message = await sentFolder.GetMessageAsync(uid);
                emails.Add(message);
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching sent emails: {ex.Message}", ex);
        }

        return emails.OrderByDescending(e => e.Date).ToList();
    }

    public async Task<EmailMessage?> GetEmailByIdAsync(string accountEmail, string emailId)
    {
        var account = await GetAccountAsync(accountEmail);
        if (account == null)
            throw new Exception($"Account {accountEmail} not found");

        using var client = _mailClientFactory.CreateClient();
        try
        {
            await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl);
            await client.AuthenticateAsync(account.Email, account.Password);

            var inbox = await client.GetInboxAsync();
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var message = await inbox.GetMessageAsync(emailId);
            return message;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching email: {ex.Message}", ex);
        }
    }

}


