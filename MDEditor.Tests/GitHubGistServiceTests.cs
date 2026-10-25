using System.Net;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using Moq;
using Octokit;
using Octokit.Internal;

namespace MDEditor.Tests;

public class GitHubGistServiceTests
{
    private readonly Mock<IGistsClient> _gists = new();
    private readonly List<string> _rawDownloads = new();
    private readonly GitHubGistService _service;

    public GitHubGistServiceTests()
    {
        var client = new Mock<IGitHubClient>();
        client.Setup(c => c.Gist).Returns(_gists.Object);

        var auth = new Mock<IGitHubAuthService>();
        auth.Setup(a => a.CreateClient(null)).Returns(client.Object);
        auth.Setup(a => a.ActiveAccount).Returns(new GitHubAccount { Login = "alice" });

        _service = new GitHubGistService(auth.Object, Mock.Of<ILoggingService>(), url =>
        {
            _rawDownloads.Add(url);
            return Task.FromResult("raw full content");
        });
    }

    private static Gist GistJson(string owner = "alice", string updatedAt = "2026-10-20T10:00:00Z", string content = "hello\r\nworld", int size = 12) =>
        new SimpleJsonSerializer().Deserialize<Gist>($$$$"""
            {"id":"abc123def","description":"Notes","public":false,"owner":{"login":"{{{{owner}}}}"},
             "html_url":"https://gist.github.com/abc123def","updated_at":"{{{{updatedAt}}}}",
             "files":{"notes.md":{"filename":"notes.md","size":{{{{size}}}},"content":{{{{System.Text.Json.JsonSerializer.Serialize(content)}}}},
                                  "raw_url":"https://gist.githubusercontent.com/alice/abc123def/raw/notes.md"},
                      "other.txt":{"filename":"other.txt","size":1,"content":"x"}}}
            """);

    [Theory]
    [InlineData("abc123def", "abc123def")]
    [InlineData("  0123456789abcdef0123456789abcdef  ", "0123456789abcdef0123456789abcdef")]
    [InlineData("https://gist.github.com/alice/abc123def", "abc123def")]
    [InlineData("https://gist.github.com/abc123def", "abc123def")]
    [InlineData("https://gist.github.com/alice/abc123def#file-notes-md", "abc123def")]
    [InlineData("https://gist.github.com/alice/abc123def.git", "abc123def")]
    [InlineData("https://gist.githubusercontent.com/alice/abc123def/raw/notes.md", "abc123def")]
    [InlineData("https://github.com/alice/abc123def", null)]
    [InlineData("not a gist", null)]
    [InlineData("xyz", null)]
    [InlineData("", null)]
    public void ParseGistId(string input, string? expected) => Assert.Equal(expected, GitHubGistService.ParseGistId(input));

    [Fact]
    public async Task GetFile_ReturnsContent_AndOwnershipAndLineEndings()
    {
        _gists.Setup(g => g.Get("abc123def")).ReturnsAsync(GistJson());

        var file = await _service.GetFileAsync("abc123def", "notes.md");

        Assert.Equal("hello\r\nworld", file.Text);
        Assert.True(file.Document.CanEdit);
        Assert.Equal("CRLF", file.Document.LineEnding);
        Assert.Equal("Notes", file.Document.DisplayName);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero), file.Document.UpdatedAt);
        Assert.Empty(_rawDownloads);
    }

    [Fact]
    public async Task GetFile_SomeoneElsesGist_IsReadOnly()
    {
        _gists.Setup(g => g.Get("abc123def")).ReturnsAsync(GistJson(owner: "bob"));

        var file = await _service.GetFileAsync("abc123def", "notes.md");

        Assert.False(file.Document.CanEdit);
        Assert.Equal("bob", file.Document.Owner);
    }

    [Fact]
    public async Task GetFile_TruncatedContent_FallsBackToRawUrl()
    {
        _gists.Setup(g => g.Get("abc123def")).ReturnsAsync(GistJson(content: "", size: 2_000_000));

        var file = await _service.GetFileAsync("abc123def", "notes.md");

        Assert.Equal("raw full content", file.Text);
        Assert.Equal("https://gist.githubusercontent.com/alice/abc123def/raw/notes.md", Assert.Single(_rawDownloads));
    }

    [Fact]
    public async Task GetFile_MissingFile_OrGist_ThrowsFriendlyError()
    {
        _gists.Setup(g => g.Get("abc123def")).ReturnsAsync(GistJson());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetFileAsync("abc123def", "nope.md"));
        Assert.Contains("nope.md", ex.Message);

        _gists.Setup(g => g.Get("deadbeef")).ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetGistAsync("deadbeef"));
        Assert.Contains("deadbeef", ex.Message);
    }

    [Fact]
    public async Task Create_SendsFileVisibilityAndDescription()
    {
        NewGist? sent = null;
        _gists.Setup(g => g.Create(It.IsAny<NewGist>())).Callback<NewGist>(n => sent = n).ReturnsAsync(GistJson());

        var doc = await _service.CreateAsync(" notes.md ", "# Hi", "  My notes  ", isPublic: true);

        Assert.Equal("# Hi", sent!.Files["notes.md"]);
        Assert.True(sent.Public);
        Assert.Equal("My notes", sent.Description);
        Assert.Equal("abc123def", doc.GistId);
        Assert.Equal("notes.md", doc.FileName);
        Assert.True(doc.CanEdit);
    }

    [Fact]
    public async Task Create_BlankDescription_IsOmitted()
    {
        NewGist? sent = null;
        _gists.Setup(g => g.Create(It.IsAny<NewGist>())).Callback<NewGist>(n => sent = n).ReturnsAsync(GistJson());

        await _service.CreateAsync("a.md", "x", "   ", isPublic: false);

        Assert.Null(sent!.Description);
        Assert.False(sent.Public);
    }

    [Fact]
    public async Task Create_WithoutGistScope_ExplainsTheScope()
    {
        _gists.Setup(g => g.Create(It.IsAny<NewGist>())).ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync("a.md", "x", null, false));

        Assert.Contains("gist scope", ex.Message);
    }

    [Fact]
    public async Task Create_RevokedToken_SaysToSignInAgain()
    {
        _gists.Setup(g => g.Create(It.IsAny<NewGist>())).ThrowsAsync(new AuthorizationException(Mock.Of<IResponse>(r => r.StatusCode == HttpStatusCode.Unauthorized), new Exception("Bad credentials")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync("a.md", "x", null, false));

        Assert.Contains("sign in again", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dir/a.md")]
    [InlineData("dir\\a.md")]
    public async Task Create_InvalidFileName_Throws(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(name, "x", null, false));
        _gists.Verify(g => g.Create(It.IsAny<NewGist>()), Times.Never);
    }

    private static GitHubGistDocument Doc(string updatedAt = "2026-10-20T10:00:00Z") => new()
    {
        GistId = "abc123def",
        FileName = "notes.md",
        Owner = "alice",
        UpdatedAt = DateTimeOffset.Parse(updatedAt),
        LineEnding = "LF",
        CanEdit = true,
    };

    [Fact]
    public async Task Update_Unchanged_EditsFile_WithOriginalLineEndings()
    {
        _gists.Setup(g => g.Get("abc123def")).ReturnsAsync(GistJson(updatedAt: "2026-10-20T10:00:00Z"));
        GistUpdate? sent = null;
        _gists.Setup(g => g.Edit("abc123def", It.IsAny<GistUpdate>())).Callback<string, GistUpdate>((_, u) => sent = u)
            .ReturnsAsync(GistJson(updatedAt: "2026-10-21T09:00:00Z"));

        var updated = await _service.UpdateAsync(Doc(), "a\r\nb");

        Assert.Equal("a\nb", sent!.Files["notes.md"].Content);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_ChangedOnGitHub_ThrowsConflict_WithoutEditing()
    {
        _gists.Setup(g => g.Get("abc123def")).ReturnsAsync(GistJson(updatedAt: "2026-10-22T00:00:00Z"));

        await Assert.ThrowsAsync<GitHubConflictException>(() => _service.UpdateAsync(Doc(), "mine"));

        _gists.Verify(g => g.Edit(It.IsAny<string>(), It.IsAny<GistUpdate>()), Times.Never);
    }

    [Fact]
    public async Task Update_Overwrite_SkipsConflictCheck()
    {
        _gists.Setup(g => g.Edit("abc123def", It.IsAny<GistUpdate>())).ReturnsAsync(GistJson(updatedAt: "2026-10-23T00:00:00Z"));

        await _service.UpdateAsync(Doc(), "mine", overwrite: true);

        _gists.Verify(g => g.Get(It.IsAny<string>()), Times.Never);
        _gists.Verify(g => g.Edit("abc123def", It.IsAny<GistUpdate>()), Times.Once);
    }

    [Fact]
    public void GistInfo_Title_FallsBackToFirstFileName()
    {
        Assert.Equal("notes.md", new GitHubGistInfo { Id = "x", Description = " ", FileNames = new[] { "notes.md" } }.Title);
        Assert.Equal("Desc", new GitHubGistInfo { Id = "x", Description = "Desc", FileNames = new[] { "notes.md" } }.Title);
    }
}
