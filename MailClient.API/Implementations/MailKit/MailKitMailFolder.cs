using MailClient.API.Abstractions;
using MailClient.API.Models;
using MailClient.API.Services;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKitFolder = MailKit.IMailFolder;

namespace MailClient.API.Implementations.MailKit;

/// <summary>
/// MailKit implementation of IMailFolder
/// </summary>
public class MailKitMailFolder : IMailFolder
{
    private readonly MailKitFolder _folder;

    public MailKitMailFolder(MailKitFolder folder)
    {
        _folder = folder;
        
        // Forward CountChanged event from MailKit folder to our abstraction
        _folder.CountChanged += (sender, args) => CountChanged?.Invoke(this, args);
    }

    public event EventHandler? CountChanged;

    public async Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default)
    {
        var mailKitAccess = access == FolderAccess.ReadOnly 
            ? MailKit.FolderAccess.ReadOnly 
            : MailKit.FolderAccess.ReadWrite;
        
        await _folder.OpenAsync(mailKitAccess, cancellationToken);
    }

    public async Task<List<string>> SearchAllAsync(CancellationToken cancellationToken = default)
    {
        var uids = await _folder.SearchAsync(SearchQuery.All, cancellationToken);
        return uids.Select(uid => uid.ToString()).ToList();
    }

    public async Task<EmailMessage> GetMessageAsync(string uid, CancellationToken cancellationToken = default)
    {
        if (!UniqueId.TryParse(uid, out var uniqueId))
        {
            throw new ArgumentException($"Invalid UID format: {uid}", nameof(uid));
        }

        var message = await _folder.GetMessageAsync(uniqueId, cancellationToken);
        return EmailMapper.ToEmailMessage(message, uid);
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        await _folder.CheckAsync(cancellationToken);
    }
}

