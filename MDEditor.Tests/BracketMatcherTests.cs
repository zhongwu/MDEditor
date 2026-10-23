using MDEditor.Editor;

namespace MDEditor.Tests;

public class BracketMatcherTests
{
    [Fact]
    public void MatchesBracketAfterCaret()
    {
        Assert.True(BracketMatcher.TryFindMatch("(hello)", 0, out int open, out int close));
        Assert.Equal(0, open);
        Assert.Equal(6, close);
    }

    [Fact]
    public void MatchesBracketBeforeCaret()
    {
        Assert.True(BracketMatcher.TryFindMatch("(hello)", 7, out int open, out int close));
        Assert.Equal(0, open);
        Assert.Equal(6, close);
    }

    [Fact]
    public void MatchesBracketImmediatelyAfterOpener()
    {
        Assert.True(BracketMatcher.TryFindMatch("a (b) c", 3, out int open, out int close));
        Assert.Equal(2, open);
        Assert.Equal(4, close);
    }

    [Fact]
    public void MatchesBracketImmediatelyBeforeCloser()
    {
        Assert.True(BracketMatcher.TryFindMatch("a (b) c", 4, out int open, out int close));
        Assert.Equal(2, open);
        Assert.Equal(4, close);
    }

    [Fact]
    public void HandlesNesting()
    {
        Assert.True(BracketMatcher.TryFindMatch("a(b[c]d)e", 1, out _, out int close));
        Assert.Equal(7, close);
    }

    [Fact]
    public void ReturnsFalseForUnmatched()
    {
        Assert.False(BracketMatcher.TryFindMatch("(hello", 1, out _, out _));
        Assert.False(BracketMatcher.TryFindMatch("hello)", 6, out _, out _));
    }

    [Fact]
    public void IgnoresEscapedBrackets()
    {
        Assert.False(BracketMatcher.TryFindMatch(@"\[x\]", 0, out _, out _));
        Assert.False(BracketMatcher.TryFindMatch(@"\[x\]", 3, out _, out _));
    }

    [Fact]
    public void HandlesNoBracketAtCaret()
    {
        Assert.False(BracketMatcher.TryFindMatch("hello", 2, out _, out _));
    }

    [Fact]
    public void MatchesAcrossNewlines()
    {
        Assert.True(BracketMatcher.TryFindMatch("function() {\n  return 1;\n}", 11, out _, out int close));
        Assert.Equal(25, close);
    }
}
