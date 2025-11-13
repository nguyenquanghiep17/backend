using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using MailClient.API.Models;

namespace MailClient.API.Services;

public class MailService : IMailService
{
    // Tạm thời lưu thông tin kết nối trong mảng
    private static readonly MailAccount[] MailAccounts = new[]
    {
        // Ví dụ với Gmail
        new MailAccount
        {
            Email = "your-email@gmail.com",
            Password = "your-app-password", // Sử dụng App Password cho Gmail
            ImapServer = "imap.gmail.com",
            ImapPort = 993,
            SmtpServer = "smtp.gmail.com",
            SmtpPort = 587,
            UseSsl = true
        }
        // Có thể thêm nhiều account khác vào đây
    };

    private MailAccount? GetAccount(string email)
    {
        return MailAccounts.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<EmailMessage>> GetInboxEmailsAsync(string accountEmail)
    {
        var account = GetAccount(accountEmail);
        if (account == null)
            throw new Exception($"Account {accountEmail} not found");

        var emails = new List<EmailMessage>();

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl);
            await client.AuthenticateAsync(account.Email, account.Password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            // Lấy 50 email mới nhất
            var uids = await inbox.SearchAsync(SearchQuery.All);
            var items = uids.Take(50).ToList();

            foreach (var uid in items)
            {
                var message = await inbox.GetMessageAsync(uid);
                emails.Add(ConvertToEmailMessage(message, uid.ToString()));
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
        var account = GetAccount(accountEmail);
        if (account == null)
            throw new Exception($"Account {accountEmail} not found");

        var emails = new List<EmailMessage>();

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl);
            await client.AuthenticateAsync(account.Email, account.Password);

            // Tìm thư mục Sent
            var personalNamespaces = client.PersonalNamespaces;
            var sentFolder = client.GetFolder(SpecialFolder.Sent) ?? 
                           client.GetFolder("Sent") ?? 
                           client.GetFolder("[Gmail]/Sent Mail");

            if (sentFolder == null)
                return emails;

            await sentFolder.OpenAsync(FolderAccess.ReadOnly);

            // Lấy 50 email mới nhất
            var uids = await sentFolder.SearchAsync(SearchQuery.All);
            var items = uids.Take(50).ToList();

            foreach (var uid in items)
            {
                var message = await sentFolder.GetMessageAsync(uid);
                emails.Add(ConvertToEmailMessage(message, uid.ToString()));
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
        var account = GetAccount(accountEmail);
        if (account == null)
            throw new Exception($"Account {accountEmail} not found");

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSsl);
            await client.AuthenticateAsync(account.Email, account.Password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            if (int.TryParse(emailId, out var uid))
            {
                var message = await inbox.GetMessageAsync(uid);
                return ConvertToEmailMessage(message, emailId);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching email: {ex.Message}", ex);
        }

        return null;
    }

    private EmailMessage ConvertToEmailMessage(MimeMessage message, string id)
    {
        var emailMessage = new EmailMessage
        {
            Id = id,
            Subject = message.Subject ?? string.Empty,
            Date = message.Date.DateTime,
            From = message.From?.ToString() ?? string.Empty,
            To = message.To.Mailboxes.Select(m => m.Address).ToList(),
            Cc = message.Cc.Mailboxes.Select(m => m.Address).ToList(),
            IsRead = false // Có thể kiểm tra flag từ message
        };

        // Xử lý body
        if (message.HtmlBody != null)
        {
            emailMessage.Body = message.HtmlBody;
            emailMessage.IsHtml = true;
        }
        else if (message.TextBody != null)
        {
            emailMessage.Body = message.TextBody;
            emailMessage.IsHtml = false;
        }

        // Xử lý attachments
        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimePart part)
            {
                emailMessage.Attachments.Add(new EmailAttachment
                {
                    FileName = part.FileName ?? "unknown",
                    ContentType = part.ContentType.MimeType,
                    Size = part.Content?.Length ?? 0
                });
            }
        }

        return emailMessage;
    }
}

