using MDEditor.Core.Models;
using MDEditor.Core.Services;

namespace MDEditor.Tests;

public class ImageUrlRewriterTests
{
    private const string Folder = "https://raw.githubusercontent.com/octo/docs/abc123/guide/";
    private const string Root = "https://raw.githubusercontent.com/octo/docs/abc123/";

    [Theory]
    [InlineData("img/a.png", Folder + "img/a.png")]
    [InlineData("./img/a.png", Folder + "img/a.png")]
    [InlineData("../assets/a.png", Folder + "../assets/a.png")] // browser normalizes the dot segment
    [InlineData("/assets/a.png", Root + "assets/a.png")]
    [InlineData("https://example.com/a.png", null)]
    [InlineData("data:image/png;base64,AAAA", null)]
    [InlineData("//cdn.example.com/a.png", null)]
    [InlineData("#frag", null)]
    [InlineData("", null)]
    public void Resolve(string src, string? expected) => Assert.Equal(expected, ImageUrlRewriter.Resolve(src, Folder, Root));

    [Fact]
    public void Rewrite_MarkdownImages_AndRawHtmlImages()
    {
        var parser = new MarkdownParser();
        var html = parser.ToHtml("![logo](img/logo.png)\n\n<img src='diagram.svg' width=\"200\">\n\n![abs](https://x.test/a.png)\n");

        var rewritten = ImageUrlRewriter.Rewrite(html, Folder, Root);

        Assert.Contains($"src=\"{Folder}img/logo.png\"", rewritten);
        Assert.Contains($"src='{Folder}diagram.svg'", rewritten);
        Assert.Contains("src=\"https://x.test/a.png\"", rewritten);
    }

    [Fact]
    public void Rewrite_LeavesLinksAndAnchorsAlone()
    {
        var html = "<p><a href=\"other.md\">x</a> <a href=\"#sec\">y</a></p>";
        Assert.Equal(html, ImageUrlRewriter.Rewrite(html, Folder, Root));
    }

    [Fact]
    public void PreviewDocument_RewritesImages_OnlyWhenBaseGiven()
    {
        var parser = new MarkdownParser();

        var withBase = parser.ToHtmlDocument("![a](a.png)", new PreviewRenderOptions(false, null, Folder, Root));
        var withoutBase = parser.ToHtmlDocument("![a](a.png)", new PreviewRenderOptions(false, null));

        Assert.Contains($"src=\"{Folder}a.png\"", withBase);
        Assert.Contains("src=\"a.png\"", withoutBase);
        Assert.DoesNotContain("<base", withBase); // a <base> would break #anchor and footnote links
    }

    [Fact]
    public void GitHubDocument_RawUrls_PinCommit_AndEscapeSegments()
    {
        var doc = new GitHubDocument { Owner = "octo", Repo = "docs", Branch = "main", BaseCommitSha = "abc123", Path = "my guide/intro.md" };
        Assert.Equal("https://raw.githubusercontent.com/octo/docs/abc123/", doc.RawRootUrl);
        Assert.Equal("https://raw.githubusercontent.com/octo/docs/abc123/my%20guide/", doc.RawFolderUrl);

        var rootFile = new GitHubDocument { Owner = "octo", Repo = "docs", Branch = "feature/x", Path = "README.md" };
        Assert.Equal("https://raw.githubusercontent.com/octo/docs/feature/x/", rootFile.RawRootUrl); // falls back to branch
        Assert.Equal(rootFile.RawRootUrl, rootFile.RawFolderUrl);
    }
}
