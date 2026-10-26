using MDEditor.Core.Models;

namespace MDEditor.Core.Interfaces;

/// <summary>Looks for newer releases of the app on GitHub Releases.</summary>
public interface IUpdateService
{
    /// <summary>
    /// Returns the latest stable release if it is newer than <paramref name="currentVersion"/>,
    /// or null when up to date (or the release cannot be understood). Throws on network failure.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default);
}
