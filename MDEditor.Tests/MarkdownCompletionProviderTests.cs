using MDEditor.Editor;

namespace MDEditor.Tests;

public class MarkdownCompletionProviderTests
{
    [Fact]
    public void EmojiCompletesAfterColon()
    {
        var result = MarkdownCompletionProvider.GetCompletions("hello :smi", 10, null, CompletionTrigger.Typing);

        Assert.NotNull(result);
        Assert.Equal("emoji", result!.Kind);
        Assert.Equal(6, result.TokenStart);
        Assert.Contains(result.Items, i => i.Insert.StartsWith(":smile:"));
    }

    [Fact]
    public void EmojiDoesNotTriggerInsideCodeFence()
    {
        var result = MarkdownCompletionProvider.GetCompletions("```\n:smi\n```", 8, null, CompletionTrigger.Typing);

        Assert.Null(result);
    }

    [Fact]
    public void EmojiDoesNotTriggerForWordColon()
    {
        Assert.Null(MarkdownCompletionProvider.GetCompletions("https:", 6, null, CompletionTrigger.Typing));
    }

    [Fact]
    public void ManualCompletionListsSnippets()
    {
        var result = MarkdownCompletionProvider.GetCompletions("", 0, null, CompletionTrigger.Manual);

        Assert.NotNull(result);
        Assert.Contains(result!.Items, i => i.Display == "Bold");
        Assert.Contains(result.Items, i => i.Display == "Table");
        Assert.Contains(result.Items, i => i.Display == "Code fence");
    }

    [Fact]
    public void ManualCompletionFiltersByWord()
    {
        var result = MarkdownCompletionProvider.GetCompletions("hea", 3, null, CompletionTrigger.Manual);

        Assert.NotNull(result);
        Assert.Equal(0, result!.TokenStart);
        Assert.All(result.Items, i => Assert.Contains("heading", i.Display, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SnippetInsertOffsetsAreInsideTheSnippet()
    {
        var result = MarkdownCompletionProvider.GetCompletions("", 0, null, CompletionTrigger.Manual);
        var bold = result!.Items.Single(i => i.Display == "Bold");

        Assert.Equal("**text**", bold.Insert);
        Assert.Equal(2, bold.CaretOffsetInInsert);
    }

    [Fact]
    public void LineStartHashSuggestsHeadings()
    {
        var result = MarkdownCompletionProvider.GetCompletions("#", 1, null, CompletionTrigger.Typing);

        Assert.NotNull(result);
        Assert.Equal("syntax", result!.Kind);
        Assert.Equal(0, result.TokenStart);
        Assert.All(result.Items, i => Assert.StartsWith("Heading", i.Display));
    }

    [Fact]
    public void LineStartDashSuggestsLists()
    {
        var result = MarkdownCompletionProvider.GetCompletions("- ", 2, null, CompletionTrigger.Typing);

        Assert.NotNull(result);
        Assert.Contains(result!.Items, i => i.Display == "Bulleted list");
        Assert.Contains(result.Items, i => i.Display == "Task list");
    }

    [Fact]
    public void UrlDoesNotTriggerPathCompletion()
    {
        Assert.Null(MarkdownCompletionProvider.GetCompletions("see https://example.com/pa", 26, "C:\\docs\\a.md", CompletionTrigger.Typing));
    }

    [Fact]
    public void PathCompletionIsSkippedForUntitledDocuments()
    {
        Assert.Null(MarkdownCompletionProvider.GetCompletions("[x](./re", 8, null, CompletionTrigger.Typing));
    }

    [Fact]
    public void PathCompletionListsFilesFromCurrentDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "mdeditor-completion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "readme.md"), "# hi");
        File.WriteAllText(Path.Combine(directory, "other.txt"), "x");

        try
        {
            string document = Path.Combine(directory, "doc.md");
            var result = MarkdownCompletionProvider.GetCompletions("[x](./re", 8, document, CompletionTrigger.Typing);

            Assert.NotNull(result);
            Assert.Equal("path", result!.Kind);
            Assert.Equal(4, result.TokenStart);
            Assert.Contains(result.Items, i => i.Insert == "./readme.md");
            Assert.DoesNotContain(result.Items, i => i.Insert.Contains("other"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
