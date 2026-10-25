using MDEditor.Core.Localization;
using MDEditor.Core.Services;

namespace MDEditor.Tests;

[Collection(nameof(LanguageSwitchingCollection))]
public class MarkdownGuideContentTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("zh-Hans")]
    [InlineData("zh-Hant")]
    public void GuideContent_RendersWithoutError_AndCoversKeyFeatures(string language)
    {
        try
        {
            Loc.SetLanguage(language);
            var markdown = Loc.Get("MarkdownGuide_Content");
            Assert.False(string.IsNullOrWhiteSpace(markdown));

            var parser = new MarkdownParser();
            var html = parser.ToHtml(markdown);

            Assert.False(string.IsNullOrWhiteSpace(html));
            Assert.Contains("<h1", html);
            Assert.Contains("<table", html);
            Assert.Contains("<pre", html); // code blocks rendered
            Assert.Contains("mermaid", html); // mermaid fence example present
        }
        finally
        {
            Loc.SetLanguage("en");
        }
    }
}
