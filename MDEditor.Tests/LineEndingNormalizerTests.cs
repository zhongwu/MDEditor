using MDEditor.Core.Services;

namespace MDEditor.Tests;

public class LineEndingNormalizerTests
{
    [Theory]
    [InlineData("a\nb\nc", "CRLF", "a\r\nb\r\nc")]
    [InlineData("a\r\nb\r\nc", "CRLF", "a\r\nb\r\nc")]
    [InlineData("a\rb\rc", "CRLF", "a\r\nb\r\nc")]
    public void NormalizesToCrlf(string input, string target, string expected)
    {
        Assert.Equal(expected, LineEndingNormalizer.Normalize(input, target));
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", "LF", "a\nb\nc")]
    [InlineData("a\nb\nc", "LF", "a\nb\nc")]
    [InlineData("a\rb\rc", "LF", "a\nb\nc")]
    public void NormalizesToLf(string input, string target, string expected)
    {
        Assert.Equal(expected, LineEndingNormalizer.Normalize(input, target));
    }

    [Fact]
    public void MixedLineEndingsNormalizeConsistently()
    {
        var input = "a\r\nb\nc\rd";

        Assert.Equal("a\nb\nc\nd", LineEndingNormalizer.Normalize(input, "LF"));
        Assert.Equal("a\r\nb\r\nc\r\nd", LineEndingNormalizer.Normalize(input, "CRLF"));
    }
}
