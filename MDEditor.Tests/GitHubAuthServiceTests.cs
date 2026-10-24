using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using Moq;
using Octokit;

namespace MDEditor.Tests;

public class GitHubAuthServiceTests
{
    private sealed class InMemorySettings : ISettingsService
    {
        public readonly Dictionary<string, object?> Values = new();
        public T Get<T>(string key, T defaultValue) => Values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;
        public void Set<T>(string key, T value) => Values[key] = value;
        public void Remove(string key) => Values.Remove(key);
        public void Save() { }
    }

    private sealed class InMemoryTokenStore : ITokenStore
    {
        public readonly Dictionary<string, string> Values = new();
        public void Save(string key, string secret) => Values[key] = secret;
        public string? Get(string key) => Values.TryGetValue(key, out var v) ? v : null;
        public void Delete(string key) => Values.Remove(key);
    }

    private readonly InMemorySettings _settings = new();
    private readonly InMemoryTokenStore _tokens = new();

    // token -> login the fake GitHub resolves it to; anything else is rejected.
    private readonly Dictionary<string, string> _validTokens = new()
    {
        ["tok-alice"] = "alice",
        ["tok-alice-2"] = "Alice",
        ["tok-bob"] = "bob",
    };

    private readonly List<Credentials?> _clientsCreatedWith = new();

    private GitHubAuthService CreateService() =>
        new(_settings, _tokens, Mock.Of<ILoggingService>(), CreateFakeClient);

    private IGitHubClient CreateFakeClient(Credentials? credentials)
    {
        _clientsCreatedWith.Add(credentials);

        var users = new Mock<IUsersClient>();
        users.Setup(u => u.Current()).Returns(() =>
        {
            if (credentials?.Password is string token && _validTokens.TryGetValue(token, out var login))
                return Task.FromResult(CreateUser(login));
            throw new AuthorizationException();
        });

        var client = new Mock<IGitHubClient>();
        client.Setup(c => c.User).Returns(users.Object);
        client.Setup(c => c.GetLastApiInfo()).Returns(new ApiInfo(
            new Dictionary<string, Uri>(), new List<string> { "repo", "gist" }, new List<string>(), "etag", new RateLimit()));
        return client.Object;
    }

    private static User CreateUser(string login) => new(
        avatarUrl: $"https://avatars.example/{login}", bio: null, blog: null, collaborators: 0, company: null,
        createdAt: DateTimeOffset.UtcNow, updatedAt: DateTimeOffset.UtcNow, diskUsage: 0, email: null, followers: 0,
        following: 0, hireable: null, htmlUrl: null, totalPrivateRepos: 0, id: 1, location: null, login: login,
        name: login.ToUpperInvariant(), nodeId: null, ownedPrivateRepos: 0, plan: null, privateGists: 0, publicGists: 0,
        publicRepos: 0, url: null, permissions: null, siteAdmin: false, ldapDistinguishedName: null, suspendedAt: null);

    [Fact]
    public async Task AddToken_ValidToken_ConnectsAccountAndStoresTokenSecurely()
    {
        var service = CreateService();

        var account = await service.AddAccountWithTokenAsync("  tok-alice  ");

        Assert.Equal("alice", account.Login);
        Assert.Equal("ALICE", account.Name);
        Assert.Equal(GitHubAuthMethod.PersonalAccessToken, account.AuthMethod);
        Assert.Equal(new[] { "repo", "gist" }, account.Scopes);
        Assert.Same(account, service.ActiveAccount);
        Assert.Equal("tok-alice", _tokens.Get("github:alice"));
    }

    [Fact]
    public async Task AddToken_TokenIsNeverWrittenToSettings()
    {
        var service = CreateService();
        await service.AddAccountWithTokenAsync("tok-alice");

        var serialized = System.Text.Json.JsonSerializer.Serialize(_settings.Values);
        Assert.DoesNotContain("tok-alice", serialized);
    }

    [Fact]
    public async Task AddToken_RejectedToken_ThrowsAndStoresNothing()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAccountWithTokenAsync("bogus"));

        Assert.Contains("rejected", ex.Message);
        Assert.Empty(service.Accounts);
        Assert.Empty(_tokens.Values);
        Assert.Null(service.ActiveAccount);
    }

    [Fact]
    public async Task AddToken_Empty_Throws()
    {
        var service = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAccountWithTokenAsync("   "));
    }

    [Fact]
    public async Task MultipleAccounts_LatestBecomesActive_AndCanSwitch()
    {
        var service = CreateService();
        await service.AddAccountWithTokenAsync("tok-alice");
        await service.AddAccountWithTokenAsync("tok-bob");

        Assert.Equal(new[] { "alice", "bob" }, service.Accounts.Select(a => a.Login));
        Assert.Equal("bob", service.ActiveAccount?.Login);

        service.SetActiveAccount("ALICE");
        Assert.Equal("alice", service.ActiveAccount?.Login);
    }

    [Fact]
    public async Task ReAddingSameAccount_DifferentCase_ReplacesTokenWithoutDuplicating()
    {
        var service = CreateService();
        await service.AddAccountWithTokenAsync("tok-alice");
        await service.AddAccountWithTokenAsync("tok-alice-2"); // GitHub reports "Alice"

        Assert.Single(service.Accounts);
        Assert.Equal("tok-alice-2", _tokens.Get("github:alice"));
    }

    [Fact]
    public async Task RemoveActiveAccount_DeletesToken_AndFallsBackToNextAccount()
    {
        var service = CreateService();
        await service.AddAccountWithTokenAsync("tok-alice");
        await service.AddAccountWithTokenAsync("tok-bob");

        service.RemoveAccount("bob");

        Assert.Null(_tokens.Get("github:bob"));
        Assert.Equal("alice", service.ActiveAccount?.Login);

        service.RemoveAccount("alice");
        Assert.Null(service.ActiveAccount);
        Assert.False(_settings.Values.ContainsKey("github.activeAccount"));
    }

    [Fact]
    public async Task AccountsChanged_RaisedOnAddSwitchAndRemove()
    {
        var service = CreateService();
        int raised = 0;
        service.AccountsChanged += (s, e) => raised++;

        await service.AddAccountWithTokenAsync("tok-alice");
        await service.AddAccountWithTokenAsync("tok-bob");
        service.SetActiveAccount("alice");
        service.SetActiveAccount("alice"); // no-op, already active
        service.RemoveAccount("bob");
        service.RemoveAccount("nobody"); // no-op

        Assert.Equal(4, raised);
    }

    [Fact]
    public async Task AccountsAndActiveAccount_PersistAcrossInstances()
    {
        var first = CreateService();
        await first.AddAccountWithTokenAsync("tok-alice");
        await first.AddAccountWithTokenAsync("tok-bob");
        first.SetActiveAccount("alice");

        var second = CreateService();

        Assert.Equal(new[] { "alice", "bob" }, second.Accounts.Select(a => a.Login));
        Assert.Equal("alice", second.ActiveAccount?.Login);
    }

    [Fact]
    public async Task CreateClient_UsesStoredTokenForRequestedOrActiveAccount()
    {
        var service = CreateService();
        await service.AddAccountWithTokenAsync("tok-alice");
        await service.AddAccountWithTokenAsync("tok-bob");
        _clientsCreatedWith.Clear();

        Assert.NotNull(service.CreateClient());
        Assert.NotNull(service.CreateClient("alice"));
        Assert.Null(service.CreateClient("nobody"));

        Assert.Equal(new[] { "tok-bob", "tok-alice" }, _clientsCreatedWith.Select(c => c?.Password));
    }

    [Fact]
    public async Task CreateClient_ReturnsNull_WhenTokenMissingFromStore()
    {
        var service = CreateService();
        await service.AddAccountWithTokenAsync("tok-alice");
        _tokens.Values.Clear(); // e.g. the user deleted it in Credential Manager

        Assert.Null(service.CreateClient());
    }

    [Fact]
    public void OAuthClientId_DefaultsToBuiltIn_AndClearingOverrideRevertsToIt()
    {
        var service = CreateService();
        Assert.Equal(GitHubAuthService.DefaultOAuthClientId, service.OAuthClientId);
        Assert.False(string.IsNullOrWhiteSpace(service.OAuthClientId));

        service.OAuthClientId = "Iv1.override";
        service.OAuthClientId = "   ";

        Assert.Equal(GitHubAuthService.DefaultOAuthClientId, service.OAuthClientId);
        Assert.False(_settings.Values.ContainsKey("github.oauthClientId"));
    }

    [Fact]
    public void OAuthClientId_IsTrimmedAndPersisted()
    {
        var service = CreateService();
        service.OAuthClientId = "  Iv1.abc123  ";

        Assert.Equal("Iv1.abc123", service.OAuthClientId);
        Assert.Equal("Iv1.abc123", CreateService().OAuthClientId);
    }
}

public class CredentialManagerTokenStoreTests
{
    [Fact]
    public void SaveGetDelete_RoundTrips()
    {
        var store = new CredentialManagerTokenStore($"MDEditor.Tests:{Guid.NewGuid():N}:");
        const string key = "github:roundtrip";
        try
        {
            Assert.Null(store.Get(key));

            store.Save(key, "secret-ü-✓");
            Assert.Equal("secret-ü-✓", store.Get(key));

            store.Save(key, "replaced");
            Assert.Equal("replaced", store.Get(key));
        }
        finally
        {
            store.Delete(key);
        }

        Assert.Null(store.Get(key));
        store.Delete(key); // deleting a missing credential is a no-op
    }
}
