using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MDEditor.Core.Localization;

namespace MDEditor.Tests;

internal static class TestCultureSetup
{
    /// <summary>
    /// Tests assert English UI text; pin the language so they pass on a Chinese Windows install
    /// (Loc otherwise follows the OS display language).
    /// </summary>
    [ModuleInitializer]
    internal static void UseEnglish() => Loc.SetLanguage("en");
}

/// <summary>Runs alone: switching Loc's (process-wide) language would break other tests' English assertions.</summary>
[CollectionDefinition(nameof(LanguageSwitchingCollection), DisableParallelization = true)]
public class LanguageSwitchingCollection
{
}

[Collection(nameof(LanguageSwitchingCollection))]
public partial class LocalizationTests
{
    private static readonly string[] Translations = { "zh-Hans", "zh-Hant" };

    private static Dictionary<string, string> Strings(string culture)
    {
        var rm = new System.Resources.ResourceManager("MDEditor.Core.Resources.Strings", typeof(Loc).Assembly);
        var set = rm.GetResourceSet(culture == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture), true, tryParents: false)!;
        return set.Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex PlaceholderPattern();

    private static string Placeholders(string value) =>
        string.Join(",", PlaceholderPattern().Matches(value).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));

    [Fact]
    public void EveryTranslation_HasEveryKey_WithMatchingPlaceholders()
    {
        var english = Strings("en");
        Assert.NotEmpty(english);

        foreach (var culture in Translations)
        {
            var translated = Strings(culture);
            Assert.Empty(english.Keys.Except(translated.Keys)); // missing translations
            Assert.Empty(translated.Keys.Except(english.Keys)); // stale keys

            foreach (var (key, value) in english)
            {
                Assert.False(string.IsNullOrWhiteSpace(translated[key]), $"{culture}:{key} is empty");
                Assert.True(Placeholders(value) == Placeholders(translated[key]), $"{culture}:{key} placeholders differ");
            }
        }
    }

    [Fact]
    public void Translations_AreActuallyTranslated()
    {
        // Guard against a column shift in strings.tsv: most values must differ from English.
        var english = Strings("en");
        foreach (var culture in Translations)
        {
            var translated = Strings(culture);
            int same = english.Count(e => e.Value == translated[e.Key]);
            Assert.True(same < english.Count / 10, $"{culture}: {same} of {english.Count} strings identical to English");
        }

        Assert.NotEqual(Strings("zh-Hans")["Menu_File"], Strings("zh-Hant")["Menu_File"]); // 文件 vs 檔案
    }

    [Theory]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-MO", "zh-Hant")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    [InlineData("ja-JP", "en")]
    public void ResolveSupported_MapsCultures(string culture, string expected) =>
        Assert.Equal(expected, Loc.ResolveSupported(CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void SetLanguage_SwitchesStrings_AndRaisesEvent()
    {
        int raised = 0;
        EventHandler handler = (s, e) => raised++;
        Loc.LanguageChanged += handler;
        try
        {
            Loc.SetLanguage("zh-Hans");
            Assert.Equal("文件", Loc.Get("Menu_File"));
            Assert.Equal("已导出到 C:\\a.md", Loc.Format("Msg_Exported", "C:\\a.md"));

            Loc.SetLanguage("zh-Hant");
            Assert.Equal("檔案", Loc.Get("Menu_File"));

            Loc.SetLanguage("zh-Hant"); // no change, no event
            Assert.Equal(2, raised);
        }
        finally
        {
            Loc.LanguageChanged -= handler;
            Loc.SetLanguage("en");
        }

        Assert.Equal("File", Loc.Get("Menu_File"));
    }

    [Fact]
    public void XamlBindings_ShowTranslation_AndUpdateLive()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Loc.SetLanguage("zh-Hant");
                var document = new MDEditor.Core.Models.GitHubDocument { Owner = "o", Repo = "r", Branch = "main", Path = "a.md" };
                var dialog = new GitHubConflictDialog(document);
                Assert.Equal("檔案已在 GitHub 上變更", dialog.Title);

                Loc.SetLanguage("zh-Hans");
                Assert.Equal("文件已在 GitHub 上更改", dialog.Title); // {loc:Tr} binding refreshed
                dialog.Close();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                Loc.SetLanguage("en");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw new Exception("XAML localization check failed", error);
    }

    [Fact]
    public void UnknownKey_FallsBackToKey()
    {
        Assert.Equal("No_Such_Key", Loc.Get("No_Such_Key"));
    }

    private static string RepoRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));

    private static IEnumerable<string> SourceFiles() =>
        new[] { "MDEditor", "MDEditor.Core" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(RepoRoot(), project), "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml"))
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [GeneratedRegex(@"Loc\.(?:Get|Format)\(""([A-Za-z0-9_]+)""|\{loc:Tr ([A-Za-z0-9_]+)\}")]
    private static partial Regex KeyUsagePattern();

    [Fact]
    public void EveryKeyUsedInSource_Exists()
    {
        var keys = Strings("en").Keys.ToHashSet();
        var missing = SourceFiles()
            .SelectMany(f => KeyUsagePattern().Matches(File.ReadAllText(f))
                .Select(m => (File: Path.GetFileName(f), Key: m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)))
            .Where(u => !keys.Contains(u.Key))
            .Select(u => $"{u.File}: {u.Key}")
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryKey_IsUsedSomewhere()
    {
        var source = string.Join("\n", SourceFiles().Select(File.ReadAllText));
        var unused = Strings("en").Keys.Where(k => !Regex.IsMatch(source, $@"\b{Regex.Escape(k)}\b")).OrderBy(k => k).ToList();
        Assert.Empty(unused);
    }
}
