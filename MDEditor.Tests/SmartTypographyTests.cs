using MDEditor.Editor;

namespace MDEditor.Tests;

public class SmartTypographyTests
{
    [Theory]
    [InlineData("hello ", 6, "\u201C")]
    [InlineData("", 0, "\u201C")]
    [InlineData("(", 1, "\u201C")]
    [InlineData("word", 4, "\u201D")]
    [InlineData("word.", 5, "\u201D")]
    public void DoubleQuotes_UseOpeningAndClosingContext(string before, int caret, string expected)
    {
        bool applied = SmartTypography.TryGetReplacement(before, caret, '"', out string insert, out int remove);

        Assert.True(applied);
        Assert.Equal(0, remove);
        Assert.Equal(expected, insert);
    }

    [Theory]
    [InlineData("", 0, "\u2018")]
    [InlineData("It is ", 6, "\u2018")]
    [InlineData("word", 4, "\u2019")]
    [InlineData("don", 3, "\u2019")]
    public void SingleQuotes_UseOpeningAndClosingContext(string before, int caret, string expected)
    {
        bool applied = SmartTypography.TryGetReplacement(before, caret, '\'', out string insert, out _);

        Assert.True(applied);
        Assert.Equal(expected, insert);
    }

    [Fact]
    public void ThirdDashMidLine_BecomesEmDash()
    {
        bool applied = SmartTypography.TryGetReplacement("wait --", 7, '-', out string insert, out int remove);

        Assert.True(applied);
        Assert.Equal("\u2014", insert);
        Assert.Equal(2, remove);
    }

    [Fact]
    public void DashAtLineStart_IsNotTransformed()
    {
        Assert.False(SmartTypography.TryGetReplacement("--", 2, '-', out _, out _));
        Assert.False(SmartTypography.TryGetReplacement("text\n--", 7, '-', out _, out _));
    }

    [Fact]
    public void SingleDash_IsNotTransformed()
    {
        Assert.False(SmartTypography.TryGetReplacement("text -", 6, '-', out _, out _));
    }

    [Fact]
    public void QuotesInsideFencedCodeBlock_AreNotTransformed()
    {
        const string text = "```\nvar s = \n```";
        Assert.True(SmartTypography.IsInsideCodeOrFrontMatter(text, 10));
        Assert.False(SmartTypography.TryGetReplacement(text, 10, '"', out _, out _));
    }

    [Fact]
    public void QuotesAfterClosingFence_AreTransformed()
    {
        const string text = "```\ncode\n```\n";
        Assert.False(SmartTypography.IsInsideCodeOrFrontMatter(text, text.Length));
        Assert.True(SmartTypography.TryGetReplacement(text, text.Length, '"', out _, out _));
    }

    [Fact]
    public void QuotesInsideInlineCode_AreNotTransformed()
    {
        Assert.True(SmartTypography.IsInsideCodeOrFrontMatter("use `code `", 10));
        Assert.False(SmartTypography.TryGetReplacement("use `code ", 10, '"', out _, out _));
        Assert.False(SmartTypography.IsInsideCodeOrFrontMatter("use `code` ", 11));
    }

    [Fact]
    public void QuotesInsideFrontMatter_AreNotTransformed()
    {
        const string text = "---\ntitle: \n---\nBody";
        Assert.True(SmartTypography.IsInsideCodeOrFrontMatter(text, 10));
        Assert.False(SmartTypography.TryGetReplacement(text, 10, '"', out _, out _));
        Assert.False(SmartTypography.IsInsideCodeOrFrontMatter(text, text.Length));
    }

    [Fact]
    public void TildeFences_AreRecognised()
    {
        Assert.True(SmartTypography.IsInsideCodeOrFrontMatter("~~~\ntext\n~~~", 6));
    }
}
