using MDEditor.Core.Models;

namespace MDEditor.Core.Interfaces;

/// <summary>
/// Gist operations for the active GitHub account.
/// </summary>
public interface IGitHubGistService
{
    /// <summary>The active account's gists (public and secret), most recently updated first.</summary>
    Task<IReadOnlyList<GitHubGistInfo>> GetMyGistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Any gist by ID (the account's own, or someone else's public/secret-by-link gist).</summary>
    Task<GitHubGistInfo> GetGistAsync(string gistId, CancellationToken cancellationToken = default);

    /// <summary>Downloads one file of a gist.</summary>
    Task<GitHubGistFileContent> GetFileAsync(string gistId, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Creates a new single-file gist and returns it as an open document.</summary>
    Task<GitHubGistDocument> CreateAsync(string fileName, string text, string? description, bool isPublic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves <paramref name="text"/> to the document's gist file.
    /// </summary>
    /// <param name="overwrite">Save even if the gist changed on GitHub since it was opened.</param>
    /// <exception cref="GitHubConflictException">The gist changed on GitHub and <paramref name="overwrite"/> is false.</exception>
    Task<GitHubGistDocument> UpdateAsync(GitHubGistDocument document, string text, bool overwrite = false, CancellationToken cancellationToken = default);
}
