using MDEditor.Core.Services;

namespace MDEditor.Tests;

public class TextStatisticsTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("   \r\n ", 0)]
    [InlineData("hello world", 2)]
    [InlineData("# Title\n\n- one, two; three", 4)]
    [InlineData("don't stop", 2)]
    [InlineData("don2019t stop", 2)]
    [InlineData("2018quoted2019 word", 2)]
    [InlineData("Hello World, this is a test.", 6)]
    [InlineData("ＡＢＣ ｄｅｆ", 2)]
    [InlineData("中文 english 混合 text", 6)]
    [InlineData("don’t stop", 2)]
    [InlineData("‘quoted’ word", 2)]
    [InlineData("你好，世界", 4)]
    [InlineData("hello 世界 world", 4)]
    [InlineData("version 2.0", 3)]
    public void CountWords(string text, int expected) => Assert.Equal(expected, TextStatistics.CountWords(text));

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("abc", 1, 4)]
    [InlineData("abc\r\n", 2, 1)]
    [InlineData("abc\r\nde", 2, 3)]
    [InlineData("a\n\n\nxy", 4, 3)]
    public void GetLineColumn(string before, int line, int column) =>
        Assert.Equal((line, column), TextStatistics.GetLineColumn(before));
}
