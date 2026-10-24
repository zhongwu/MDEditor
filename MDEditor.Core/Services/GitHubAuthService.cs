using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using Octokit;

namespace MDEditor.Core.Services;

/// <summary>
/// Manages connected GitHub accounts. Account metadata is persisted in settings
/// (<c>github.accounts</c>, <c>github.activeAccount</c>); tokens go to <see cref="ITokenStore"/>.
/// </summary>
public class GitHubAuthService : IGitHubAuthService
{
    /// <summary>
    /// Scopes requested by the device flow: repo (read/write files, commit, branches, PRs),
    /// gist (Phase 5.4), read:org (list organization repositories).
    /// </summary>
    public static readonly string[] RequestedScopes = { "repo", "gist", "read:org" };

    /// <summary>
    /// Client ID of MDEditor's registered OAuth App (device flow enabled), used unless overridden
    /// in the GitHub Accounts window. The client ID is public - the device flow needs no client
    /// secret - so it's safe to ship in source.
    /// </summary>
    public const string DefaultOAuthClientId = "Ov23lilApRZVKAicwHRt";

    private const string AccountsKey = "github.accounts";
    private const string ActiveAccountKey = "github.activeAccount";
    private const string ClientIdKey = "github.oauthClientId";
    private const string TokenKeyPrefix = "github:";

    private static readonly ProductHeaderValue ProductHeader = new("MDEditor");

    private readonly ISettingsService _settingsService;
    private readonly ITokenStore _tokenStore;
    private readonly ILoggingService _loggingService;
    private readonly Func<Credentials?, IGitHubClient> _clientFactory;
    private readonly List<GitHubAccount> _accounts;
    private string? _activeLogin;

    public GitHubAuthService(ISettingsService settingsService, ITokenStore tokenStore, ILoggingService loggingService)
        : this(settingsService, tokenStore, loggingService, DefaultClientFactory)
    {
    }

    /// <param name="clientFactory">Creates a client for the given credentials (null = anonymous).</param>
    public GitHubAuthService(ISettingsService settingsService, ITokenStore tokenStore, ILoggingService loggingService,
        Func<Credentials?, IGitHubClient> clientFactory)
    {
        _settingsService = settingsService;
        _tokenStore = tokenStore;
        _loggingService = loggingService;
        _clientFactory = clientFactory;

        _accounts = _settingsService.Get<List<GitHubAccount>?>(AccountsKey, null) ?? new List<GitHubAccount>();
        _activeLogin = FindAccount(_settingsService.Get<string?>(ActiveAccountKey, null))?.Login
            ?? _accounts.FirstOrDefault()?.Login;
    }

    private static IGitHubClient DefaultClientFactory(Credentials? credentials)
    {
        var client = new GitHubClient(ProductHeader);
        if (credentials != null)
            client.Credentials = credentials;
        return client;
    }

    public IReadOnlyList<GitHubAccount> Accounts => _accounts.AsReadOnly();

    public GitHubAccount? ActiveAccount => FindAccount(_activeLogin);

    public string OAuthClientId
    {
        get => _settingsService.Get(ClientIdKey, DefaultOAuthClientId) ?? string.Empty;
        set
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || trimmed == DefaultOAuthClientId)
                _settingsService.Remove(ClientIdKey);
            else
                _settingsService.Set(ClientIdKey, trimmed);
        }
    }

    public event EventHandler? AccountsChanged;

    public Task<GitHubAccount> AddAccountWithTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        token = token?.Trim() ?? string.Empty;
        if (token.Length == 0)
            throw new ArgumentException("Token is empty.", nameof(token));

        return ConnectAsync(token, GitHubAuthMethod.PersonalAccessToken, cancellationToken);
    }

    public async Task<GitHubDeviceCode> StartDeviceFlowAsync(CancellationToken cancellationToken = default)
    {
        var clientId = OAuthClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("No OAuth App client ID is configured, so browser sign-in is unavailable. Use a Personal Access Token instead, or set a client ID.");

        var request = new OauthDeviceFlowRequest(clientId);
        foreach (var scope in RequestedScopes)
            request.Scopes.Add(scope);

        var response = await _clientFactory(null).Oauth.InitiateDeviceFlow(request, cancellationToken);
        _loggingService.LogInformation("GitHub device flow started");

        return new GitHubDeviceCode
        {
            UserCode = response.UserCode,
            VerificationUri = response.VerificationUri,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
            Response = response,
        };
    }

    public async Task<GitHubAccount> CompleteDeviceFlowAsync(GitHubDeviceCode code, CancellationToken cancellationToken = default)
    {
        // Octokit polls at the server-specified interval (backing off on slow_down) until the
        // user approves, denies, or the code expires.
        OauthToken oauthToken;
        try
        {
            oauthToken = await _clientFactory(null).Oauth.CreateAccessTokenForDeviceFlow(OAuthClientId, code.Response, cancellationToken);
        }
        catch (ApiException ex) when (ex is not AuthorizationException)
        {
            throw new InvalidOperationException(DeviceFlowErrorMessage(ex.Message, null), ex);
        }

        if (string.IsNullOrEmpty(oauthToken.AccessToken))
            throw new InvalidOperationException(DeviceFlowErrorMessage(oauthToken.Error, oauthToken.ErrorDescription));

        return await ConnectAsync(oauthToken.AccessToken, GitHubAuthMethod.OAuthDevice, cancellationToken);
    }

    private static string DeviceFlowErrorMessage(string? error, string? description) => error switch
    {
        "access_denied" => "Sign-in was cancelled on GitHub.",
        "expired_token" => "The sign-in code expired. Please try again.",
        _ => description ?? error ?? "GitHub did not return an access token.",
    };

    private async Task<GitHubAccount> ConnectAsync(string token, GitHubAuthMethod method, CancellationToken cancellationToken)
    {
        var client = _clientFactory(new Credentials(token));

        User user;
        try
        {
            user = await client.User.Current();
        }
        catch (AuthorizationException ex)
        {
            throw new InvalidOperationException("GitHub rejected the token. Check that it was copied correctly and hasn't expired or been revoked.", ex);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var account = new GitHubAccount
        {
            Login = user.Login,
            Name = string.IsNullOrWhiteSpace(user.Name) ? null : user.Name,
            AvatarUrl = user.AvatarUrl,
            AuthMethod = method,
            Scopes = client.GetLastApiInfo()?.OauthScopes?.ToList() ?? new List<string>(),
            AddedAt = DateTimeOffset.UtcNow,
        };

        _tokenStore.Save(TokenKey(account.Login), token);

        var existing = FindAccount(account.Login);
        if (existing != null)
            _accounts[_accounts.IndexOf(existing)] = account;
        else
            _accounts.Add(account);

        // A newly connected account becomes active - it's almost always the one the user wants next.
        _activeLogin = account.Login;
        Persist();

        _loggingService.LogInformation("GitHub account connected: {Login} ({Method})", account.Login, method);
        AccountsChanged?.Invoke(this, EventArgs.Empty);
        return account;
    }

    public void SetActiveAccount(string login)
    {
        var account = FindAccount(login) ?? throw new ArgumentException($"No connected account '{login}'.", nameof(login));
        if (account.Login == _activeLogin)
            return;

        _activeLogin = account.Login;
        Persist();
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveAccount(string login)
    {
        var account = FindAccount(login);
        if (account == null)
            return;

        // Only the local copy is deleted: revoking an OAuth token needs the app's client secret,
        // and PATs can only be revoked on github.com.
        _tokenStore.Delete(TokenKey(account.Login));
        _accounts.Remove(account);

        if (account.Login == _activeLogin)
            _activeLogin = _accounts.FirstOrDefault()?.Login;

        Persist();
        _loggingService.LogInformation("GitHub account removed: {Login}", account.Login);
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    public IGitHubClient? CreateClient(string? login = null)
    {
        var token = GetAccessToken(login);
        return token == null ? null : _clientFactory(new Credentials(token));
    }

    public string? GetAccessToken(string? login = null)
    {
        var account = FindAccount(login ?? _activeLogin);
        if (account == null)
            return null;

        var token = _tokenStore.Get(TokenKey(account.Login));
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private GitHubAccount? FindAccount(string? login) =>
        login == null ? null : _accounts.FirstOrDefault(a => string.Equals(a.Login, login, StringComparison.OrdinalIgnoreCase));

    // GitHub logins are case-insensitive; normalize so re-adding "Octocat" overwrites "octocat".
    private static string TokenKey(string login) => TokenKeyPrefix + login.ToLowerInvariant();

    private void Persist()
    {
        _settingsService.Set(AccountsKey, _accounts.ToList());
        if (_activeLogin == null)
            _settingsService.Remove(ActiveAccountKey);
        else
            _settingsService.Set(ActiveAccountKey, _activeLogin);
    }
}
