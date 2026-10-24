using MDEditor.Core.Models;
using MDEditor.Core.Services;

namespace MDEditor.Tests;

public class MarkdownParserOptionsTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void MathEnabledByDefault()
    {
        var html = _parser.ToHtml("$x^2$");
        Assert.Contains("\\(", html);
    }

    [Fact]
    public void DisablingMathLeavesDollarSyntaxLiteral()
    {
        _parser.UpdateOptions(new MarkdownParserOptions { EnableMath = false });

        var html = _parser.ToHtml("$x^2$");

        Assert.DoesNotContain("\\(", html);
        Assert.Contains("$x^2$", html);
    }

    [Fact]
    public void DisablingDiagramsLeavesMermaidFenceAsPlainCode()
    {
        _parser.UpdateOptions(new MarkdownParserOptions { EnableDiagrams = false });

        var html = _parser.ToHtml("```mermaid\ngraph TD; A-->B;\n```");

        Assert.DoesNotContain("class=\"mermaid\"", html);
        Assert.Contains("<pre>", html);
    }

    [Fact]
    public void DisablingEmojiLeavesShortcodeLiteral()
    {
        _parser.UpdateOptions(new MarkdownParserOptions { EnableEmoji = false });

        var html = _parser.ToHtml("Hello :smile:");

        Assert.Contains(":smile:", html);
    }

    [Fact]
    public void DisablingFootnotesLeavesMarkerLiteral()
    {
        _parser.UpdateOptions(new MarkdownParserOptions { EnableFootnotes = false });

        var html = _parser.ToHtml("Text[^1]\n\n[^1]: Note");

        Assert.DoesNotContain("footnote-ref", html);
    }

    [Fact]
    public void DisablingTaskListsLeavesPlainListItem()
    {
        _parser.UpdateOptions(new MarkdownParserOptions { EnableTaskLists = false });

        var html = _parser.ToHtml("- [ ] todo");

        Assert.DoesNotContain("task-list-item", html);
    }

    [Fact]
    public void UpdateOptionsIsNoOpWhenUnchanged()
    {
        _parser.UpdateOptions(MarkdownParserOptions.Default);
        var before = _parser.ToHtml("$x$");

        _parser.UpdateOptions(MarkdownParserOptions.Default);
        var after = _parser.ToHtml("$x$");

        Assert.Equal(before, after);
    }

    [Fact]
    public void ExportDocumentIsAlwaysLightWithNoCustomCss()
    {
        var html = _parser.ToHtmlDocument("# Hi", new PreviewRenderOptions(IsDarkTheme: true, CustomCss: ".mine { color: red; }"));
        Assert.Contains("#0d1117", html); // dark background only present via the preview overload
        Assert.Contains(".mine", html);

        var exported = _parser.ToHtmlDocument("# Hi");
        Assert.DoesNotContain("#0d1117", exported);
        Assert.DoesNotContain(".mine", exported);
    }

    [Fact]
    public void CustomCssCannotBreakOutOfStyleTag()
    {
        var html = _parser.ToHtmlDocument("# Hi", new PreviewRenderOptions(IsDarkTheme: false, CustomCss: "</style><script>evil()</script>"));

        Assert.DoesNotContain("</style><script>", html);
    }
}
