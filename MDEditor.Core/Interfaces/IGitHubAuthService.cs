using MDEditor.Core.Models;
using Octokit;

namespace MDEditor.Core.Interfaces;

/// <summary>
/// Manages connected GitHub accounts: sign-in via OAuth device flow or Personal Access Token,
/// secure token storage, and the active account used for GitHub operations.
/// </summary>
public interface IGitHubAuthService
{
    /// <summary>
    /// All connected accounts, in the order they were added.
    /// </summary>
    IReadOnlyList<GitHubAccount> Accounts { get; }

    /// <summary>
    /// The account used for GitHub operations, or null if none is connected.
    /// </summary>
    GitHubAccount? ActiveAccount { get; }

    /// <summary>
    /// OAuth App client ID used for the device flow. Device flow is unavailable while empty.
    /// </summary>
    string OAuthClientId { get; set; }

    /// <summary>
    /// Raised when accounts are added/removed or the active account changes.
    /// </summary>
    event EventHandler? AccountsChanged;

    /// <summary>
    /// Validates a Personal Access Token against GitHub and connects its account.
    /// Re-adding an existing account replaces its token.
    /// </summary>
    Task<GitHubAccount> AddAccountWithTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an OAuth device flow and returns the code the user must enter on GitHub.
    /// </summary>
    Task<GitHubDeviceCode> StartDeviceFlowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls GitHub until the user approves (or denies / the code expires), then connects
    /// the account.
    /// </summary>
    Task<GitHubAccount> CompleteDeviceFlowAsync(GitHubDeviceCode code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes <paramref name="login"/> the active account.
    /// </summary>
    void SetActiveAccount(string login);

    /// <summary>
    /// Disconnects an account and deletes its stored token.
    /// </summary>
    void RemoveAccount(string login);

    /// <summary>
    /// Creates an authenticated client for <paramref name="login"/> (or the active account),
    /// or null if that account has no stored token.
    /// </summary>
    IGitHubClient? CreateClient(string? login = null);
}
