using MailClient.API.Models;

namespace MailClient.API.Abstractions;

/// <summary>
/// Abstraction for mail folder operations.
/// </summary>
public interface IMailFolder
{
    /// <summary>
    /// Opens the folder with specified access mode
    /// </summary>
    Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all message UIDs in the folder
    /// </summary>
    Task<List<string>> SearchAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a message by its UID
    /// </summary>
    Task<EmailMessage> GetMessageAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for updates from the server
    /// </summary>
    Task CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when the message count changes
    /// </summary>
    event EventHandler CountChanged;
}

/// <summary>
/// Folder access modes
/// </summary>
public enum FolderAccess
{
    ReadOnly,
    ReadWrite
}

