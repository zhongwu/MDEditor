using System.Net;
using System.Text;
using System.Text.Json;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using Moq;

namespace MDEditor.Tests;

/// <summary>Scripted GitLab server: routes "METHOD path?query" to a canned response and records every request.</summary>
internal sealed class FakeGitLabHandler : HttpMessageHandler
{
    public sealed record Recorded(HttpMethod Method, string PathAndQuery, string? Token, string? Body);

    public readonly List<Recorded> Requests = new();
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, string?, HttpResponseMessage> Respond)> _routes = new();

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK, string? nextPage = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (nextPage != null)
            response.Headers.Add("X-Next-Page", nextPage);
        return response;
    }

    /// <summary>Route requests whose "METHOD /api/v4/..." line contains <paramref name="contains"/>; the first matching route wins.</summary>
    public FakeGitLabHandler On(string method, string contains, Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        _routes.Add((r => r.Method.Method == method && Uri.UnescapeDataString(r.RequestUri!.PathAndQuery).Contains(contains), respond));
        return this;
    }

    public FakeGitLabHandler On(string method, string contains, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, contains, (_, _) => Json(json, status));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        request.Headers.TryGetValues("PRIVATE-TOKEN", out var tokens);
        Requests.Add(new Recorded(request.Method, request.RequestUri!.PathAndQuery, tokens?.FirstOrDefault(), body));

        foreach (var (match, respond) in _routes)
        {
            if (match(request))
                return respond(request, body);
        }

        return Json("{\"message\":\"404 Not Found\"}", HttpStatusCode.NotFound);
    }
}

public class GitLabApiClientTests
{
    [Theory]
    [InlineData("http://unicornlab.fun:35080/", "http://unicornlab.fun:35080")]
    [InlineData("http://unicornlab.fun:35080/users/sign_in", "http://unicornlab.fun:35080")]
    [InlineData("gitlab.example.com", "https://gitlab.example.com")]
    [InlineData("  https://gitlab.com/api/v4  ", "https://gitlab.com")]
    [InlineData("https://example.com/gitlab/", "https://example.com/gitlab")]
    [InlineData("https://example.com/gitlab/api/v4/", "https://example.com/gitlab")]
    public void NormalizeServerUrl_Valid(string input, string expected) =>
        Assert.Equal(expected, GitLabApiClient.NormalizeServerUrl(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://host")]
    [InlineData("http://")]
    public void NormalizeServerUrl_Invalid(string input) =>
        Assert.Throws<FormatException>(() => GitLabApiClient.NormalizeServerUrl(input));

    private static GitLabApiClient Client(FakeGitLabHandler handler, string? token = "tok") =>
        new(new HttpClient(handler), "http://unicornlab.fun:35080", token);

    [Fact]
    public async Task Get_SendsTokenAndHitsApiV4()
    {
        var handler = new FakeGitLabHandler().On("GET", "/api/v4/user", "{\"username\":\"u\"}");
        var user = await Client(handler).GetAsync("user");

        Assert.Equal("u", user.GetProperty("username").GetString());
        var request = Assert.Single(handler.Requests);
        Assert.Equal("tok", request.Token);
        Assert.Equal("/api/v4/user", request.PathAndQuery);
    }

    [Fact]
    public async Task GetAllPages_FollowsNextPageHeader_AndReportsTruncation()
    {
        var handler = new FakeGitLabHandler().On("GET", "/items", (r, _) =>
        {
            var page = r.RequestUri!.Query.Contains("page=2") ? 2 : 1;
            return FakeGitLabHandler.Json($"[{{\"n\":{page}}}]", nextPage: page == 1 ? "2" : "3");
        });

        var (items, truncated) = await Client(handler).GetAllPagesAsync("items", maxPages: 2);

        Assert.Equal(2, items.Count);
        Assert.True(truncated); // a third page existed but the cap stopped us

        var (all, truncated2) = await Client(new FakeGitLabHandler().On("GET", "/items", "[{\"n\":1}]")).GetAllPagesAsync("items");
        Assert.Single(all);
        Assert.False(truncated2);
    }

    [Fact]
    public async Task Error_ExposesStatusAndServerMessage()
    {
        var handler = new FakeGitLabHandler().On("GET", "/user", "{\"message\":\"401 Unauthorized\"}", HttpStatusCode.Unauthorized);
        var ex = await Assert.ThrowsAsync<GitLabApiException>(() => Client(handler).GetAsync("user"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal("401 Unauthorized", ex.ApiMessage);
    }

    [Fact]
    public async Task HtmlResponse_IsReportedAsNotGitLab()
    {
        var handler = new FakeGitLabHandler().On("GET", "/user", (_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>hi</html>") });
        var ex = await Assert.ThrowsAsync<GitLabApiException>(() => Client(handler).GetAsync("user"));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public void ExtractMessage_HandlesObjectsStringsAndGarbage()
    {
        Assert.Equal("boom", GitLabApiClient.ExtractMessage("{\"message\":\"boom\"}"));
        Assert.Equal("bad", GitLabApiClient.ExtractMessage("{\"error\":\"bad\"}"));
        Assert.Contains("name", GitLabApiClient.ExtractMessage("{\"message\":{\"name\":[\"taken\"]}}"));
        Assert.Null(GitLabApiClient.ExtractMessage("<html>"));
        Assert.Null(GitLabApiClient.ExtractMessage(""));
    }

    [Fact]
    public void EscapeFilePath_EncodesSlashes() =>
        Assert.Equal("docs%2Fa%20b.md", GitLabApiClient.EscapeFilePath("docs/a b.md"));
}

public class GitLabAuthServiceTests
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

    private GitLabAuthService Create(FakeGitLabHandler handler) =>
        new(_settings, _tokens, Mock.Of<ILoggingService>(),
            (url, token, _) => new GitLabApiClient(new HttpClient(handler), url, token));

    private static FakeGitLabHandler HealthyServer(string username = "alice") => new FakeGitLabHandler()
        .On("GET", "/api/v4/user", $"{{\"id\":7,\"username\":\"{username}\",\"name\":\"Alice A\",\"avatar_url\":\"http://x/a.png\"}}")
        .On("GET", "/api/v4/version", "{\"version\":\"17.5.1\",\"revision\":\"abc\"}");

    [Fact]
    public async Task AddAccount_ConnectsStoresTokenAndPersists()
    {
        var service = Create(HealthyServer());
        var account = await service.AddAccountAsync("unicornlab.fun:35080/users/sign_in", " secret-token ");

        Assert.Equal("http://unicornlab.fun:35080".Replace("http", "https"), account.ServerUrl); // no scheme typed -> https
        Assert.Equal("alice", account.Username);
        Assert.Equal(7, account.UserId);
        Assert.Equal("17.5.1", account.ServerVersion);
        Assert.Same(account, service.ActiveAccount);
        Assert.Equal("secret-token", service.GetAccessToken());
        Assert.DoesNotContain("secret-token", JsonSerializer.Serialize(_settings.Values.Values)); // token never in settings
        Assert.Single(_settings.Get<List<GitLabAccount>>("gitlab.accounts", new()));
    }

    [Fact]
    public async Task AddAccount_ExplicitHttpServerWithPort_KeepsSchemeAndPort()
    {
        var account = await Create(HealthyServer()).AddAccountAsync("http://unicornlab.fun:35080/", "t");
        Assert.Equal("http://unicornlab.fun:35080", account.ServerUrl);
        Assert.Equal("alice@unicornlab.fun:35080", account.DisplayName);
    }

    [Fact]
    public async Task AddAccount_VersionEndpointFailing_StillConnects()
    {
        var handler = new FakeGitLabHandler()
            .On("GET", "/api/v4/user", "{\"id\":1,\"username\":\"bob\"}")
            .On("GET", "/api/v4/version", "{\"message\":\"403 Forbidden\"}", HttpStatusCode.Forbidden);
        var account = await Create(handler).AddAccountAsync("http://h", "t");
        Assert.Null(account.ServerVersion);
    }

    [Fact]
    public async Task AddAccount_RejectedToken_GivesFriendlyError()
    {
        var handler = new FakeGitLabHandler().On("GET", "/api/v4/user", "{\"message\":\"401 Unauthorized\"}", HttpStatusCode.Unauthorized);
        var service = Create(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAccountAsync("http://h", "bad"));
        Assert.Contains("rejected", ex.Message);
        Assert.Empty(service.Accounts);
        Assert.Empty(_tokens.Values);
    }

    [Fact]
    public async Task AddAccount_NotAGitLabServer_Reported()
    {
        var handler = new FakeGitLabHandler().On("GET", "/api/v4/user", (_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html/>") });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).AddAccountAsync("http://h", "t"));
        Assert.Contains("GitLab", ex.Message);
    }

    [Fact]
    public async Task AddAccount_UnreachableServer_Reported()
    {
        var handler = new FakeGitLabHandler().On("GET", "/api/v4/user", (_, _) => throw new HttpRequestException("No such host is known"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).AddAccountAsync("http://nope", "t"));
        Assert.Contains("No such host", ex.Message);
    }

    [Fact]
    public async Task AddAccount_InvalidUrlOrEmptyToken_Throws()
    {
        var service = Create(HealthyServer());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAccountAsync("ftp://x", "t"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAccountAsync("http://h", "  "));
    }

    [Fact]
    public async Task SameUserOnTwoServers_AreTwoAccounts_AndReaddReplaces()
    {
        var service = Create(HealthyServer());
        await service.AddAccountAsync("http://one", "t1");
        await service.AddAccountAsync("http://two", "t2");
        await service.AddAccountAsync("http://one", "t1b"); // same server + user: replace

        Assert.Equal(2, service.Accounts.Count);
        Assert.Equal("t1b", service.GetAccessToken("alice@one"));
        Assert.Equal("t2", service.GetAccessToken("alice@two"));
        Assert.Equal("alice@one", service.ActiveAccount!.Key); // last added becomes active
    }

    [Fact]
    public async Task SetActive_Remove_UpdateStateAndDeleteToken()
    {
        var service = Create(HealthyServer());
        await service.AddAccountAsync("http://one", "t1");
        await service.AddAccountAsync("http://two", "t2");
        var raised = 0;
        service.AccountsChanged += (_, _) => raised++;

        service.SetActiveAccount("alice@one");
        Assert.Equal("alice@one", service.ActiveAccount!.Key);
        Assert.Throws<ArgumentException>(() => service.SetActiveAccount("nobody@x"));

        service.RemoveAccount("alice@one");
        Assert.Equal("alice@two", service.ActiveAccount!.Key);
        Assert.Null(service.GetAccessToken("alice@one"));
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task CreateClient_UsesStoredToken_AndNullWithoutAccount()
    {
        var handler = HealthyServer();
        var service = Create(handler);
        Assert.Null(service.CreateClient());

        await service.AddAccountAsync("http://one", "tok-1");
        var client = service.CreateClient();
        Assert.NotNull(client);
        await client!.GetAsync("user");
        Assert.Equal("tok-1", handler.Requests.Last().Token);
    }

    [Fact]
    public async Task AccountsRestoredFromSettings()
    {
        await Create(HealthyServer()).AddAccountAsync("http://one", "t1");
        var restored = Create(HealthyServer());

        Assert.Single(restored.Accounts);
        Assert.Equal("alice@one", restored.ActiveAccount!.Key);
        Assert.Equal("t1", restored.GetAccessToken());
    }
}

public class GitLabRepositoryServiceTests
{
    private const string Server = "http://unicornlab.fun:35080";

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static string FileJson(string text, string lastCommit = "c1", string blob = "b1") =>
        $"{{\"file_name\":\"a.md\",\"content\":\"{Base64(text)}\",\"encoding\":\"base64\",\"blob_id\":\"{blob}\",\"last_commit_id\":\"{lastCommit}\"}}";

    private static GitLabRepositoryService Create(FakeGitLabHandler handler)
    {
        var auth = new Mock<IGitLabAuthService>();
        auth.Setup(a => a.CreateClient(It.IsAny<string?>())).Returns(() => new GitLabApiClient(new HttpClient(handler), Server, "tok"));
        return new GitLabRepositoryService(auth.Object, Mock.Of<ILoggingService>());
    }

    private static GitLabDocument Doc(string lastCommit = "c1", string baseSha = "head1", string lineEnding = "LF", bool canPush = true) => new()
    {
        ServerUrl = Server, ProjectId = 42, ProjectPath = "team/docs", Branch = "main", Path = "guide/a.md",
        LastCommitId = lastCommit, BaseCommitSha = baseSha, LineEnding = lineEnding, CanPush = canPush,
    };

    [Fact]
    public async Task GetProjects_ParsesPermissionsVisibilityAndArchived()
    {
        var handler = new FakeGitLabHandler().On("GET", "/api/v4/projects?", """
            [
              {"id":1,"path_with_namespace":"team/docs","name":"docs","default_branch":"main","visibility":"private","last_activity_at":"2026-10-01T10:00:00Z",
               "web_url":"http://s/team/docs","permissions":{"project_access":{"access_level":40},"group_access":null}},
              {"id":2,"path_with_namespace":"team/reader","name":"reader","default_branch":null,"visibility":"public",
               "permissions":{"project_access":{"access_level":20},"group_access":{"access_level":10}}},
              {"id":3,"path_with_namespace":"team/old","name":"old","visibility":"internal","archived":true,
               "permissions":{"project_access":{"access_level":50}}},
              {"id":4,"path_with_namespace":"grp/viaGroup","name":"viaGroup","default_branch":"dev","visibility":"private",
               "permissions":{"project_access":null,"group_access":{"access_level":30}}}
            ]
            """);

        var projects = await Create(handler).GetProjectsAsync();

        Assert.Equal(4, projects.Count);
        Assert.True(projects[0].CanPush);
        Assert.True(projects[0].IsPrivate);
        Assert.Equal("main", projects[0].DefaultBranch);
        Assert.NotNull(projects[0].UpdatedAt);
        Assert.False(projects[1].CanPush);          // Reporter
        Assert.False(projects[1].IsPrivate);        // public
        Assert.Equal("main", projects[1].DefaultBranch); // null default branch falls back
        Assert.False(projects[2].CanPush);          // archived, even for an Owner
        Assert.True(projects[3].CanPush);           // Developer through the group
        Assert.Equal("dev", projects[3].DefaultBranch);
        Assert.Contains("membership=true", handler.Requests.Single(r => r.PathAndQuery.Contains("/projects?")).PathAndQuery);
    }

    [Fact]
    public async Task GetProjects_Administrator_SeesEveryProject_AndCanPushWithoutMembership()
    {
        var handler = new FakeGitLabHandler()
            .On("GET", "/api/v4/user", "{\"id\":1,\"username\":\"root\",\"is_admin\":true}")
            .On("GET", "/api/v4/projects?", """
                [{"id":1,"path_with_namespace":"other/notmine","name":"notmine","visibility":"private","permissions":{"project_access":null,"group_access":null}},
                 {"id":2,"path_with_namespace":"other/old","name":"old","visibility":"private","archived":true,"permissions":{"project_access":null,"group_access":null}}]
                """);

        var projects = await Create(handler).GetProjectsAsync();

        Assert.True(projects[0].CanPush);   // no membership, but an administrator
        Assert.False(projects[1].CanPush);  // archived projects are read-only for everyone
        var listRequest = handler.Requests.Single(r => r.PathAndQuery.Contains("/projects?"));
        Assert.DoesNotContain("membership=true", listRequest.PathAndQuery);
    }

    [Fact]
    public async Task GetProjects_NonAdministrator_StaysLimitedToMemberships()
    {
        var handler = new FakeGitLabHandler()
            .On("GET", "/api/v4/user", "{\"id\":2,\"username\":\"dev\",\"is_admin\":false}")
            .On("GET", "/api/v4/projects?", "[]");
        await Create(handler).GetProjectsAsync();
        Assert.Contains("membership=true", handler.Requests.Single(r => r.PathAndQuery.Contains("/projects?")).PathAndQuery);
    }

    [Fact]
    public async Task GetBranches_ReturnsNames()
    {
        var handler = new FakeGitLabHandler().On("GET", "/projects/42/repository/branches", "[{\"name\":\"main\"},{\"name\":\"dev\"}]");
        Assert.Equal(new[] { "main", "dev" }, await Create(handler).GetBranchesAsync(42));
    }

    [Fact]
    public async Task GetTree_MapsBlobsAndTrees_AndEmptyRepoIsEmpty()
    {
        var handler = new FakeGitLabHandler().On("GET", "/repository/tree", """[{"type":"tree","path":"docs"},{"type":"blob","path":"docs/a.md"}]""");
        var tree = await Create(handler).GetTreeAsync(42, "main");
        Assert.Equal(2, tree.Entries.Count);
        Assert.True(tree.Entries[0].IsFolder);
        Assert.False(tree.Entries[1].IsFolder);
        Assert.Equal("a.md", tree.Entries[1].Name);
        Assert.Contains("recursive=true", handler.Requests[0].PathAndQuery);

        var empty = new FakeGitLabHandler().On("GET", "/repository/tree", "{\"message\":\"404 Tree Not Found\"}", HttpStatusCode.NotFound);
        Assert.Empty((await Create(empty).GetTreeAsync(42, "main")).Entries);
    }

    [Fact]
    public async Task GetFile_DecodesContentAndBuildsDocument()
    {
        var handler = new FakeGitLabHandler()
            .On("GET", "/repository/files/guide/a.md", FileJson("﻿line1\r\nline2", lastCommit: "lc9"))
            .On("GET", "/repository/branches/main", "{\"name\":\"main\",\"commit\":{\"id\":\"head9\"}}");
        var project = new GitLabProject { Id = 42, PathWithNamespace = "team/docs", CanPush = true };

        var file = await Create(handler).GetFileAsync(project, "main", "guide/a.md");

        Assert.Equal("line1\r\nline2", file.Text); // BOM stripped
        Assert.Equal("CRLF", file.Document.LineEnding);
        Assert.Equal("lc9", file.Document.LastCommitId);
        Assert.Equal("head9", file.Document.BaseCommitSha);
        Assert.Equal("guide/a.md", file.Document.Path);
        Assert.Equal(Server, file.Document.ServerUrl);
        Assert.True(file.Document.CanPush);
        Assert.Contains("guide%2Fa.md", handler.Requests[0].PathAndQuery); // the path is one encoded segment
        Assert.Contains("ref=main", handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task GetFileHistory_MapsCommits()
    {
        var handler = new FakeGitLabHandler().On("GET", "/repository/commits", """
            [{"id":"aaaaaaaaaa","message":"Fix typo\n\nlong body","author_name":"Alice","authored_date":"2026-10-02T09:00:00Z","parent_ids":["bbbbbbbbbb"],"web_url":"http://s/c/aaaa"},
             {"id":"bbbbbbbbbb","title":"First","author_name":"Bob","created_at":"2026-10-01T09:00:00Z","parent_ids":[]}]
            """);
        var history = await Create(handler).GetFileHistoryAsync(Doc());

        Assert.Equal(2, history.Count);
        Assert.Equal("Fix typo", history[0].Summary);
        Assert.Equal("bbbbbbbbbb", history[0].ParentSha);
        Assert.Null(history[1].ParentSha);
        Assert.Equal("First", history[1].Message);
        Assert.Contains($"{Server}/team/docs/-/commit/bbbbbbbbbb", history[1].HtmlUrl); // fallback link
        Assert.Contains("ref_name=main", handler.Requests[0].PathAndQuery);
        Assert.Contains("path=guide%2Fa.md", handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task GetFileAtCommit_ReturnsTextOrNull()
    {
        var found = new FakeGitLabHandler().On("GET", "ref=abc123", FileJson("old text"));
        Assert.Equal("old text", await Create(found).GetFileAtCommitAsync(Doc(), "abc123"));

        var missing = new FakeGitLabHandler().On("GET", "/repository/files/", "{\"message\":\"404 File Not Found\"}", HttpStatusCode.NotFound);
        Assert.Null(await Create(missing).GetFileAtCommitAsync(Doc(), "abc123"));
    }

    private static FakeGitLabHandler CommitServer(string currentLastCommit = "c1", HttpStatusCode fileStatus = HttpStatusCode.OK) =>
        new FakeGitLabHandler()
            .On("GET", "/repository/files/", (_, _) => fileStatus == HttpStatusCode.OK
                ? FakeGitLabHandler.Json(FileJson("current", currentLastCommit))
                : FakeGitLabHandler.Json("{\"message\":\"404 File Not Found\"}", fileStatus))
            .On("POST", "/repository/commits", "{\"id\":\"newcommit\",\"web_url\":\"http://s/team/docs/-/commit/newcommit\"}", HttpStatusCode.Created)
            .On("POST", "/merge_requests", "{\"web_url\":\"http://s/team/docs/-/merge_requests/5\"}", HttpStatusCode.Created);

    private static JsonElement LastBody(FakeGitLabHandler handler, string pathContains) =>
        JsonDocument.Parse(handler.Requests.Last(r => r.PathAndQuery.Contains(pathContains)).Body!).RootElement;

    [Fact]
    public async Task Commit_Update_SendsActionWithLastCommitId_AndAdvancesDocument()
    {
        var handler = CommitServer();
        var result = await Create(handler).CommitFileAsync(Doc(lineEnding: "CRLF"), "a\nb", "Edit");

        var body = LastBody(handler, "/repository/commits");
        Assert.Equal("main", body.GetProperty("branch").GetString());
        Assert.Equal("Edit", body.GetProperty("commit_message").GetString());
        var action = body.GetProperty("actions")[0];
        Assert.Equal("update", action.GetProperty("action").GetString());
        Assert.Equal("guide/a.md", action.GetProperty("file_path").GetString());
        Assert.Equal("a\r\nb", action.GetProperty("content").GetString()); // original line endings preserved
        Assert.Equal("c1", action.GetProperty("last_commit_id").GetString());
        Assert.False(body.TryGetProperty("start_sha", out _));

        Assert.Equal("newcommit", result.Document.LastCommitId);
        Assert.Equal("newcommit", result.Document.BaseCommitSha);
        Assert.Equal("main", result.Document.Branch);
        Assert.Contains("newcommit", result.CommitUrl);
        Assert.Null(result.MergeRequestUrl);
    }

    [Fact]
    public async Task Commit_FileChangedOnServer_ThrowsConflictWithoutCommitting()
    {
        var handler = CommitServer(currentLastCommit: "someone-else");
        await Assert.ThrowsAsync<GitHubConflictException>(() => Create(handler).CommitFileAsync(Doc(), "x", "msg"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Commit_FileDeletedOnServer_IsConflict_ButOverwriteRecreatesIt()
    {
        await Assert.ThrowsAsync<GitHubConflictException>(() =>
            Create(CommitServer(fileStatus: HttpStatusCode.NotFound)).CommitFileAsync(Doc(), "x", "msg"));

        var handler = CommitServer(fileStatus: HttpStatusCode.NotFound);
        await Create(handler).CommitFileAsync(Doc(), "x", "msg", overwrite: true);
        var action = LastBody(handler, "/repository/commits").GetProperty("actions")[0];
        Assert.Equal("create", action.GetProperty("action").GetString());
    }

    [Fact]
    public async Task Commit_Overwrite_SkipsLastCommitIdCheck()
    {
        var handler = CommitServer(currentLastCommit: "someone-else");
        await Create(handler).CommitFileAsync(Doc(), "x", "msg", overwrite: true);

        var action = LastBody(handler, "/repository/commits").GetProperty("actions")[0];
        Assert.Equal("update", action.GetProperty("action").GetString());
        Assert.False(action.TryGetProperty("last_commit_id", out _));
    }

    [Fact]
    public async Task Commit_RaceDetectedByServer_BecomesConflict()
    {
        var handler = new FakeGitLabHandler()
            .On("GET", "/repository/files/", (_, _) => FakeGitLabHandler.Json(FileJson("current", "c1")))
            .On("POST", "/repository/commits", "{\"message\":\"You are attempting to update a file that has changed since you started editing it.\"}", HttpStatusCode.BadRequest);
        await Assert.ThrowsAsync<GitHubConflictException>(() => Create(handler).CommitFileAsync(Doc(), "x", "msg"));
    }

    [Fact]
    public async Task Commit_NewBranch_StartsFromBaseCommit_AndOpensMergeRequest()
    {
        var handler = CommitServer();
        var result = await Create(handler).CommitFileAsync(Doc(), "x", "msg", newBranch: " feature/x ", mergeRequestTitle: "My MR");

        var body = LastBody(handler, "/repository/commits");
        Assert.Equal("feature/x", body.GetProperty("branch").GetString());
        Assert.Equal("head1", body.GetProperty("start_sha").GetString());
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Get); // no conflict check on a fresh branch
        Assert.False(body.GetProperty("actions")[0].TryGetProperty("last_commit_id", out _));

        var mr = LastBody(handler, "/merge_requests");
        Assert.Equal("feature/x", mr.GetProperty("source_branch").GetString());
        Assert.Equal("main", mr.GetProperty("target_branch").GetString());
        Assert.Equal("My MR", mr.GetProperty("title").GetString());

        Assert.Equal("feature/x", result.Document.Branch);
        Assert.Contains("merge_requests/5", result.MergeRequestUrl);
    }

    [Fact]
    public async Task Commit_NewBranchWithoutBaseSha_UsesStartBranch()
    {
        var handler = CommitServer();
        await Create(handler).CommitFileAsync(Doc(baseSha: ""), "x", "msg", newBranch: "b");
        var body = LastBody(handler, "/repository/commits");
        Assert.Equal("main", body.GetProperty("start_branch").GetString());
        Assert.False(body.TryGetProperty("start_sha", out _));
    }

    [Fact]
    public async Task Commit_ExistingBranchName_ReportsFriendlyError()
    {
        var handler = new FakeGitLabHandler().On("POST", "/repository/commits", "{\"message\":\"A branch called 'b' already exists.\"}", HttpStatusCode.BadRequest);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).CommitFileAsync(Doc(), "x", "msg", newBranch: "b"));
        Assert.Contains("'b'", ex.Message);
    }

    [Fact]
    public async Task Commit_Forbidden_ReportsNoPermission()
    {
        var handler = CommitServer();
        handler = new FakeGitLabHandler()
            .On("GET", "/repository/files/", (_, _) => FakeGitLabHandler.Json(FileJson("current", "c1")))
            .On("POST", "/repository/commits", "{\"message\":\"403 Forbidden\"}", HttpStatusCode.Forbidden);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).CommitFileAsync(Doc(canPush: false), "x", "msg"));
        Assert.Contains("team/docs", ex.Message);
    }

    [Fact]
    public async Task Commit_EmptyMessage_Throws() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Create(CommitServer()).CommitFileAsync(Doc(), "x", "  "));

    [Fact]
    public void Document_BuildsGitLabLinks()
    {
        var doc = Doc();
        Assert.Equal($"{Server}/team/docs/-/blob/main/guide/a.md", doc.HtmlUrl);
        Assert.Equal($"{Server}/team/docs/-/raw/head1/", doc.RawRootUrl);
        Assert.Equal($"{Server}/team/docs/-/raw/head1/guide/", doc.RawFolderUrl);
        Assert.Equal("team/docs:guide/a.md@main", doc.DisplayName);
    }

    [Fact]
    public async Task NoActiveAccount_ThrowsSignInMessage()
    {
        var auth = new Mock<IGitLabAuthService>();
        auth.Setup(a => a.CreateClient(It.IsAny<string?>())).Returns((GitLabApiClient?)null);
        var service = new GitLabRepositoryService(auth.Object, Mock.Of<ILoggingService>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetProjectsAsync());
    }
}
