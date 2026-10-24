using System.Net;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using Moq;
using Octokit;
using Octokit.Internal;

namespace MDEditor.Tests;

public class GitHubRepositoryServiceTests
{
    private readonly Mock<IRepositoryContentsClient> _contents = new();
    private readonly Mock<IReferencesClient> _references = new();
    private readonly Mock<IPullRequestsClient> _pullRequests = new();
    private readonly GitHubRepositoryService _service;

    private static readonly GitHubDocument Document = new()
    {
        Owner = "octo",
        Repo = "docs",
        Branch = "main",
        Path = "guide/intro.md",
        Sha = "blob-opened",
        BaseCommitSha = "commit-opened",
        LineEnding = "LF",
        CanPush = true,
    };

    public GitHubRepositoryServiceTests()
    {
        var repository = new Mock<IRepositoriesClient>();
        repository.Setup(r => r.Content).Returns(_contents.Object);

        var git = new Mock<IGitDatabaseClient>();
        git.Setup(g => g.Reference).Returns(_references.Object);

        var client = new Mock<IGitHubClient>();
        client.Setup(c => c.Repository).Returns(repository.Object);
        client.Setup(c => c.Git).Returns(git.Object);
        client.Setup(c => c.PullRequest).Returns(_pullRequests.Object);

        var auth = new Mock<IGitHubAuthService>();
        auth.Setup(a => a.CreateClient(null)).Returns(client.Object);

        _service = new GitHubRepositoryService(auth.Object, Mock.Of<ILoggingService>());
    }

    private static T FromJson<T>(string json) => new SimpleJsonSerializer().Deserialize<T>(json);

    private static RepositoryContentChangeSet ChangeSet(string blobSha, string commitSha) =>
        FromJson<RepositoryContentChangeSet>($$$"""{"content":{"sha":"{{{blobSha}}}"},"commit":{"sha":"{{{commitSha}}}"}}""");

    [Fact]
    public async Task Commit_Direct_UpdatesWithOpenedShaOnSameBranch_AndPreservesLineEndings()
    {
        UpdateFileRequest? sent = null;
        _contents.Setup(c => c.UpdateFile("octo", "docs", "guide/intro.md", It.IsAny<UpdateFileRequest>()))
            .Callback<string, string, string, UpdateFileRequest>((_, _, _, r) => sent = r)
            .ReturnsAsync(ChangeSet("blob-new", "commit-new"));

        var result = await _service.CommitFileAsync(Document, "a\r\nb\r\n", "Update intro.md");

        Assert.NotNull(sent);
        Assert.Equal("blob-opened", sent!.Sha);
        Assert.Equal("main", sent.Branch);
        Assert.Equal("Update intro.md", sent.Message);
        Assert.Equal("a\nb\n", DecodeContent(sent.Content)); // editor CRLF -> file's LF

        Assert.Equal("blob-new", result.Document.Sha);
        Assert.Equal("commit-new", result.Document.BaseCommitSha);
        Assert.Equal("main", result.Document.Branch);
        Assert.Equal("https://github.com/octo/docs/commit/commit-new", result.CommitUrl);
        Assert.Null(result.PullRequestUrl);
        _references.VerifyNoOtherCalls();
    }

    // Octokit base64-encodes content by default; accept either form so the test doesn't depend on that detail.
    private static string DecodeContent(string content)
    {
        try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content)); }
        catch (FormatException) { return content; }
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "blob-opened is not the latest")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "guide/intro.md does not match blob-opened")]
    public async Task Commit_StaleSha_ThrowsConflict(HttpStatusCode status, string message)
    {
        _contents.Setup(c => c.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateFileRequest>()))
            .ThrowsAsync(new ApiException(message, status));

        await Assert.ThrowsAsync<GitHubConflictException>(() => _service.CommitFileAsync(Document, "text", "msg"));
    }

    [Fact]
    public async Task Commit_OtherValidationError_IsNotReportedAsConflict()
    {
        _contents.Setup(c => c.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateFileRequest>()))
            .ThrowsAsync(new ApiException("Invalid request", HttpStatusCode.UnprocessableEntity));

        var ex = await Assert.ThrowsAsync<ApiException>(() => _service.CommitFileAsync(Document, "text", "msg"));
        Assert.IsNotType<GitHubConflictException>(ex);
    }

    [Fact]
    public async Task Commit_NewBranch_BranchesFromOpenedCommit_CommitsThere_AndOpensPullRequest()
    {
        NewReference? createdRef = null;
        _references.Setup(r => r.Create("octo", "docs", It.IsAny<NewReference>()))
            .Callback<string, string, NewReference>((_, _, r) => createdRef = r)
            .ReturnsAsync(new Reference());

        UpdateFileRequest? sent = null;
        _contents.Setup(c => c.UpdateFile("octo", "docs", "guide/intro.md", It.IsAny<UpdateFileRequest>()))
            .Callback<string, string, string, UpdateFileRequest>((_, _, _, r) => sent = r)
            .ReturnsAsync(ChangeSet("blob-new", "commit-new"));

        NewPullRequest? pr = null;
        _pullRequests.Setup(p => p.Create("octo", "docs", It.IsAny<NewPullRequest>()))
            .Callback<string, string, NewPullRequest>((_, _, r) => pr = r)
            .ReturnsAsync(FromJson<PullRequest>("""{"html_url":"https://github.com/octo/docs/pull/7"}"""));

        var result = await _service.CommitFileAsync(Document, "text", "Fix typo", newBranch: " me-patch-1 ", pullRequestTitle: "Fix typo");

        Assert.Equal("refs/heads/me-patch-1", createdRef!.Ref);
        Assert.Equal("commit-opened", createdRef.Sha);
        Assert.Equal("me-patch-1", sent!.Branch);
        Assert.Equal("blob-opened", sent.Sha); // applies cleanly: the new branch has the opened version
        Assert.Equal("me-patch-1", pr!.Head);
        Assert.Equal("main", pr.Base);
        Assert.Equal("Fix typo", pr.Title);

        Assert.Equal("me-patch-1", result.Document.Branch);
        Assert.Equal("https://github.com/octo/docs/pull/7", result.PullRequestUrl);
    }

    [Fact]
    public async Task Commit_NewBranchWithoutPrTitle_DoesNotOpenPullRequest()
    {
        _references.Setup(r => r.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NewReference>())).ReturnsAsync(new Reference());
        _contents.Setup(c => c.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateFileRequest>()))
            .ReturnsAsync(ChangeSet("b", "c"));

        var result = await _service.CommitFileAsync(Document, "text", "msg", newBranch: "topic");

        Assert.Null(result.PullRequestUrl);
        _pullRequests.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Commit_Overwrite_UsesLatestShaFromBranch()
    {
        _contents.Setup(c => c.GetAllContentsByRef("octo", "docs", "guide/intro.md", "main"))
            .ReturnsAsync(new[] { FromJson<RepositoryContent>("""{"sha":"blob-theirs","type":"file"}""") });

        UpdateFileRequest? sent = null;
        _contents.Setup(c => c.UpdateFile("octo", "docs", "guide/intro.md", It.IsAny<UpdateFileRequest>()))
            .Callback<string, string, string, UpdateFileRequest>((_, _, _, r) => sent = r)
            .ReturnsAsync(ChangeSet("blob-new", "commit-new"));

        await _service.CommitFileAsync(Document, "mine", "msg", overwrite: true);

        Assert.Equal("blob-theirs", sent!.Sha);
    }

    [Fact]
    public async Task Commit_Overwrite_FileDeletedUpstream_RecreatesIt()
    {
        _contents.Setup(c => c.GetAllContentsByRef(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));
        _contents.Setup(c => c.CreateFile("octo", "docs", "guide/intro.md", It.IsAny<CreateFileRequest>()))
            .ReturnsAsync(ChangeSet("blob-new", "commit-new"));

        var result = await _service.CommitFileAsync(Document, "mine", "msg", overwrite: true);

        Assert.Equal("blob-new", result.Document.Sha);
        _contents.Verify(c => c.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateFileRequest>()), Times.Never);
    }

    [Fact]
    public async Task Commit_EmptyMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CommitFileAsync(Document, "text", "  "));
    }

    [Theory]
    [InlineData("README.md", true)]
    [InlineData("docs/Guide.MARKDOWN", true)]
    [InlineData("page.mdx", true)]
    [InlineData("notes.txt", false)]
    [InlineData("md", false)]
    public void IsMarkdownPath(string path, bool expected) => Assert.Equal(expected, GitHubRepositoryService.IsMarkdownPath(path));

    [Theory]
    [InlineData("a\nb", "LF")]
    [InlineData("a\r\nb", "CRLF")]
    [InlineData("single line", "LF")]
    public void DetectLineEnding(string text, string expected) => Assert.Equal(expected, GitHubRepositoryService.DetectLineEnding(text));

    [Theory]
    [InlineData("feature/docs-update", true)]
    [InlineData("me-patch-20261024-1530", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("a..b", false)]
    [InlineData("/leading", false)]
    [InlineData("trailing/", false)]
    [InlineData("ends.lock", false)]
    [InlineData("what?", false)]
    [InlineData("tilde~1", false)]
    [InlineData("a//b", false)]
    [InlineData(".hidden", false)]
    [InlineData("@", false)]
    public void IsValidBranchName(string name, bool expected) => Assert.Equal(expected, GitHubRepositoryService.IsValidBranchName(name));
}

public class GitHubTreeBuilderTests
{
    private static GitHubTreeEntry File(string path) => new() { Path = path };
    private static GitHubTreeEntry Folder(string path) => new() { Path = path, IsFolder = true };

    private static readonly GitHubTreeEntry[] Entries =
    {
        File("README.md"),
        Folder("src"),
        File("src/app.cs"),
        Folder("docs"),
        File("docs/zeta.md"),
        File("docs/Alpha.md"),
        Folder("docs/api"),
        File("docs/api/index.md"),
        Folder("assets"),
        File("assets/logo.png"),
        File("LICENSE"),
    };

    [Fact]
    public void MarkdownOnly_KeepsMarkdownAndPrunesFoldersWithoutAny()
    {
        var nodes = GitHubTreeBuilder.Build(Entries, markdownOnly: true);

        Assert.Equal(new[] { "docs", "README.md" }, nodes.Select(n => n.Name));
        var docs = nodes[0];
        Assert.Equal(new[] { "api", "Alpha.md", "zeta.md" }, docs.Children.Select(n => n.Name)); // folders first, case-insensitive
        Assert.Equal("docs/api/index.md", docs.Children[0].Children.Single().Path);
    }

    [Fact]
    public void AllFiles_KeepsEverything_FoldersFirst()
    {
        var nodes = GitHubTreeBuilder.Build(Entries, markdownOnly: false);

        Assert.Equal(new[] { "assets", "docs", "src", "LICENSE", "README.md" }, nodes.Select(n => n.Name));
        Assert.True(nodes[0].IsFolder);
        Assert.False(nodes[3].IsFolder);
    }

    [Fact]
    public void MissingFolderEntries_FromTruncatedListing_AreSynthesized()
    {
        var nodes = GitHubTreeBuilder.Build(new[] { File("a/b/c.md") }, markdownOnly: true);

        var a = Assert.Single(nodes);
        Assert.True(a.IsFolder);
        var b = Assert.Single(a.Children);
        Assert.Equal("a/b", b.Path);
        Assert.Equal("a/b/c.md", Assert.Single(b.Children).Path);
    }

    [Fact]
    public void EmptyFoldersInAllFilesMode_AreKept()
    {
        var nodes = GitHubTreeBuilder.Build(new[] { Folder("empty") }, markdownOnly: false);
        Assert.Equal("empty", Assert.Single(nodes).Name);
    }
}
