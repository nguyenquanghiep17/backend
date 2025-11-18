using MailClient.API.Models;
using MimeKit;

namespace MailClient.API.Services;

public static class EmailMapper
{
    public static EmailMessage ToEmailMessage(MimeMessage message, string id)
    {
        var emailMessage = new EmailMessage
        {
            Id = id,
            Subject = message.Subject ?? string.Empty,
            Date = message.Date.DateTime,
            From = message.From?.ToString() ?? string.Empty,
            To = message.To.Mailboxes.Select(m => m.Address).ToList(),
            Cc = message.Cc.Mailboxes.Select(m => m.Address).ToList(),
            IsRead = false
        };

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

        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimePart part)
            {
                emailMessage.Attachments.Add(new EmailAttachment
                {
                    FileName = part.FileName ?? "unknown",
                    ContentType = part.ContentType.MimeType,
                    Size = 0
                });
            }
        }

        return emailMessage;
    }
}


