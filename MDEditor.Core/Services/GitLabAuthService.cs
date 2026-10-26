using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Localization;
using MDEditor.Core.Models;

namespace MDEditor.Core.Services;

/// <summary>
/// Manages connected GitLab accounts. Account metadata is persisted in settings
/// (<c>gitlab.accounts</c>, <c>gitlab.activeAccount</c>); tokens go to <see cref="ITokenStore"/>.
/// </summary>
public class GitLabAuthService : IGitLabAuthService
{
    private const string AccountsKey = "gitlab.accounts";
    private const string ActiveAccountKey = "gitlab.activeAccount";
    private const string TokenKeyPrefix = "gitlab:";

    // HttpClients are reused per server (the token travels in a per-request header).
    private static readonly ConcurrentDictionary<string, HttpClient> SharedClients = new();

    private readonly ISettingsService _settingsService;
    private readonly ITokenStore _tokenStore;
    private readonly ILoggingService _loggingService;
    private readonly Func<string, string?, bool, GitLabApiClient> _clientFactory;
    private readonly List<GitLabAccount> _accounts;
    private string? _activeKey;

    public GitLabAuthService(ISettingsService settingsService, ITokenStore tokenStore, ILoggingService loggingService)
        : this(settingsService, tokenStore, loggingService, DefaultClientFactory)
    {
    }

    /// <param name="clientFactory">Creates an API client for (server URL, token or null, skip certificate validation).</param>
    public GitLabAuthService(ISettingsService settingsService, ITokenStore tokenStore, ILoggingService loggingService,
        Func<string, string?, bool, GitLabApiClient> clientFactory)
    {
        _settingsService = settingsService;
        _tokenStore = tokenStore;
        _loggingService = loggingService;
        _clientFactory = clientFactory;

        _accounts = _settingsService.Get<List<GitLabAccount>?>(AccountsKey, null) ?? new List<GitLabAccount>();
        _activeKey = FindAccount(_settingsService.Get<string?>(ActiveAccountKey, null))?.Key
            ?? _accounts.FirstOrDefault()?.Key;
    }

    private static GitLabApiClient DefaultClientFactory(string serverUrl, string? token, bool skipCertificateValidation)
    {
        var http = SharedClients.GetOrAdd($"{serverUrl}|{skipCertificateValidation}", _ =>
        {
            var handler = new SocketsHttpHandler();
            if (skipCertificateValidation)
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MDEditor");
            return client;
        });
        return new GitLabApiClient(http, serverUrl, token);
    }

    public IReadOnlyList<GitLabAccount> Accounts => _accounts.AsReadOnly();

    public GitLabAccount? ActiveAccount => FindAccount(_activeKey);

    public event EventHandler? AccountsChanged;

    public async Task<GitLabAccount> AddAccountAsync(string serverUrl, string token, bool skipCertificateValidation = false, CancellationToken cancellationToken = default)
    {
        token = token?.Trim() ?? string.Empty;
        if (token.Length == 0)
            throw new ArgumentException("Token is empty.", nameof(token));

        string normalized;
        try
        {
            normalized = GitLabApiClient.NormalizeServerUrl(serverUrl);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(Loc.Format("GL_Err_InvalidUrl", serverUrl), ex);
        }

        var client = _clientFactory(normalized, token, skipCertificateValidation);
        var shown = normalized[(normalized.IndexOf("://", StringComparison.Ordinal) + 3)..];

        JsonElement user;
        try
        {
            user = await client.GetAsync("user", cancellationToken);
        }
        catch (GitLabApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(Loc.Format("GL_Err_TokenRejected", shown), ex);
        }
        catch (GitLabApiException ex)
        {
            throw new InvalidOperationException(Loc.Format("GL_Err_NotGitLab", shown), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(Loc.Format("GL_Err_Unreachable", shown, ex.Message), ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(Loc.Format("GL_Err_Unreachable", shown, "timed out"), ex);
        }

        if (user.ValueKind != JsonValueKind.Object || !user.TryGetProperty("username", out var username) || username.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException(Loc.Format("GL_Err_NotGitLab", shown));

        string? version = null;
        try
        {
            var info = await client.GetAsync("version", cancellationToken);
            version = info.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch (Exception ex) when (ex is GitLabApiException or HttpRequestException)
        {
            // The version is informational only (some servers restrict it); the account still works.
        }

        var account = new GitLabAccount
        {
            ServerUrl = normalized,
            Username = username.GetString()!,
            UserId = user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : 0,
            Name = GetString(user, "name"),
            AvatarUrl = GetString(user, "avatar_url"),
            ServerVersion = version,
            SkipCertificateValidation = skipCertificateValidation,
            AddedAt = DateTimeOffset.UtcNow,
        };

        _tokenStore.Save(TokenKey(account), token);

        var existing = FindAccount(account.Key);
        if (existing != null)
            _accounts[_accounts.IndexOf(existing)] = account;
        else
            _accounts.Add(account);

        _activeKey = account.Key;
        Persist();

        _loggingService.LogInformation("GitLab account connected: {Account}", account.DisplayName);
        AccountsChanged?.Invoke(this, EventArgs.Empty);
        return account;
    }

    public void SetActiveAccount(string key)
    {
        var account = FindAccount(key) ?? throw new ArgumentException($"No connected account '{key}'.", nameof(key));
        if (account.Key == _activeKey)
            return;

        _activeKey = account.Key;
        Persist();
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveAccount(string key)
    {
        var account = FindAccount(key);
        if (account == null)
            return;

        // Only the local copy is deleted; a personal access token can only be revoked on the server.
        _tokenStore.Delete(TokenKey(account));
        _accounts.Remove(account);

        if (account.Key == _activeKey)
            _activeKey = _accounts.FirstOrDefault()?.Key;

        Persist();
        _loggingService.LogInformation("GitLab account removed: {Account}", account.DisplayName);
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    public GitLabApiClient? CreateClient(string? key = null)
    {
        var account = FindAccount(key ?? _activeKey);
        var token = GetAccessToken(key);
        return account == null || token == null ? null : _clientFactory(account.ServerUrl, token, account.SkipCertificateValidation);
    }

    public string? GetAccessToken(string? key = null)
    {
        var account = FindAccount(key ?? _activeKey);
        if (account == null)
            return null;

        var token = _tokenStore.Get(TokenKey(account));
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private GitLabAccount? FindAccount(string? key) =>
        key == null ? null : _accounts.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));

    // User names are case-insensitive on GitLab; Key is lower-cased so "Alice" and "alice" are one account.
    private static string TokenKey(GitLabAccount account) => TokenKeyPrefix + account.Key;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;

    private void Persist()
    {
        _settingsService.Set(AccountsKey, _accounts.ToList());
        if (_activeKey == null)
            _settingsService.Remove(ActiveAccountKey);
        else
            _settingsService.Set(ActiveAccountKey, _activeKey);
    }
}
