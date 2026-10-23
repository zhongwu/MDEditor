using MDEditor.Core.Models;
using MDEditor.Core.Services;

namespace MDEditor.Tests;

public class MarkdownOutlineTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void EmptyDocumentHasNoOutline()
    {
        Assert.Empty(_parser.GetOutline(""));
        Assert.Empty(_parser.GetOutline("just a paragraph\n\nand another"));
    }

    [Fact]
    public void NestsHeadingsByLevel()
    {
        var outline = _parser.GetOutline("# A\n\n## B\n\n### C\n\n## D\n\n# E\n");

        Assert.Equal(2, outline.Count);
        Assert.Equal("A", outline[0].Title);
        Assert.Equal(new[] { "B", "D" }, outline[0].Children.Select(c => c.Title));
        Assert.Equal("C", Assert.Single(outline[0].Children[0].Children).Title);
        Assert.Equal("E", outline[1].Title);
    }

    [Fact]
    public void SkippedLevelsNestUnderNearestParent()
    {
        var outline = _parser.GetOutline("# Top\n\n### Deep\n\n## Mid\n");

        var top = Assert.Single(outline);
        Assert.Equal(new[] { "Deep", "Mid" }, top.Children.Select(c => c.Title));
        Assert.Equal(3, top.Children[0].Level);
    }

    [Fact]
    public void ReportsZeroBasedSourceLines()
    {
        var outline = _parser.GetOutline("intro\n\n# First\ntext\n\n## Second\n");

        Assert.Equal(2, outline[0].Line);
        Assert.Equal(5, outline[0].Children[0].Line);
    }

    [Fact]
    public void StripsInlineMarkupFromTitles()
    {
        var outline = _parser.GetOutline("# Use `code` and **bold** with [a link](http://example.com) & more\n");

        Assert.Equal("Use code and bold with a link & more", Assert.Single(outline).Title);
    }

    [Fact]
    public void IgnoresHeadingSyntaxInsideCodeBlocks()
    {
        var outline = _parser.GetOutline("# Real\n\n```\n# not a heading\n```\n");

        Assert.Equal("Real", Assert.Single(outline).Title);
    }

    [Fact]
    public void IncludesSetextHeadings()
    {
        var outline = _parser.GetOutline("Title\n=====\n\nSub\n---\n");

        var title = Assert.Single(outline);
        Assert.Equal("Title", title.Title);
        Assert.Equal("Sub", Assert.Single(title.Children).Title);
    }

    [Fact]
    public void IdsMatchRenderedHtml()
    {
        const string markdown = "# Getting Started\n\n## Custom {#my-id}\n";
        var outline = _parser.GetOutline(markdown);
        var html = _parser.ToHtml(markdown);

        Assert.Equal("getting-started", outline[0].Id);
        Assert.Equal("my-id", outline[0].Children[0].Id);
        Assert.Contains("id=\"getting-started\"", html);
        Assert.Contains("id=\"my-id\"", html);
    }

    [Fact]
    public void SkipsEmptyHeadings()
    {
        var outline = _parser.GetOutline("#\n\n# Named\n");

        Assert.Equal("Named", Assert.Single(outline).Title);
    }

    [Fact]
    public void BuildTreeHandlesFlatSequence()
    {
        var items = new[]
        {
            new OutlineItem(2, "a", 0, null),
            new OutlineItem(2, "b", 1, null),
            new OutlineItem(1, "c", 2, null),
            new OutlineItem(3, "d", 3, null),
        };

        var roots = OutlineItem.BuildTree(items);

        Assert.Equal(new[] { "a", "b", "c" }, roots.Select(r => r.Title));
        Assert.Equal("d", Assert.Single(roots[2].Children).Title);
    }
}
