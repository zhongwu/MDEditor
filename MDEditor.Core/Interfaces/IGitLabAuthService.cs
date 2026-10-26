using MDEditor.Core.Models;
using MDEditor.Core.Services;

namespace MDEditor.Core.Interfaces;

/// <summary>
/// Manages connected GitLab accounts (gitlab.com or self-hosted servers): sign-in with a personal
/// access token, secure token storage, and the active account used for GitLab operations.
/// </summary>
public interface IGitLabAuthService
{
    /// <summary>All connected accounts, in the order they were added.</summary>
    IReadOnlyList<GitLabAccount> Accounts { get; }

    /// <summary>The account used for GitLab operations, or null if none is connected.</summary>
    GitLabAccount? ActiveAccount { get; }

    /// <summary>Raised when accounts are added/removed or the active account changes.</summary>
    event EventHandler? AccountsChanged;

    /// <summary>
    /// Validates a personal access token against <paramref name="serverUrl"/> and connects its account.
    /// Re-adding an existing account replaces its token.
    /// </summary>
    /// <param name="serverUrl">Server address as typed by the user, e.g. "http://host:35080" or "gitlab.example.com".</param>
    /// <param name="skipCertificateValidation">Accept an untrusted/self-signed TLS certificate (self-hosted servers).</param>
    /// <exception cref="InvalidOperationException">The server is unreachable, isn't GitLab, or rejected the token (message is user-presentable).</exception>
    Task<GitLabAccount> AddAccountAsync(string serverUrl, string token, bool skipCertificateValidation = false, CancellationToken cancellationToken = default);

    /// <summary>Makes the account with <see cref="GitLabAccount.Key"/> <paramref name="key"/> the active one.</summary>
    void SetActiveAccount(string key);

    /// <summary>Disconnects an account and deletes its stored token.</summary>
    void RemoveAccount(string key);

    /// <summary>An authenticated API client for the account (or the active account), or null if it has no stored token.</summary>
    GitLabApiClient? CreateClient(string? key = null);

    /// <summary>The stored token for the account (or the active one), or null. Never log or persist it.</summary>
    string? GetAccessToken(string? key = null);
}
