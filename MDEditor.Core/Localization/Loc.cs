using System.Globalization;
using System.Resources;

namespace MDEditor.Core.Localization;

/// <summary>
/// UI string lookup for the app's supported languages (Resources/Strings*.resx). Missing keys or
/// translations fall back to English, then to the key itself so a gap is visible but harmless.
/// </summary>
public static class Loc
{
    /// <summary>Setting value meaning "follow the Windows display language".</summary>
    public const string SystemLanguage = "System";

    private static readonly ResourceManager Resources = new("MDEditor.Core.Resources.Strings", typeof(Loc).Assembly);

    // Captured before the first SetLanguage call changes the thread's UI culture.
    private static readonly CultureInfo SystemUICulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Languages with a translation, as (culture code, name in that language). The native names
    /// are shown untranslated in the language picker so users can always find their own.
    /// </summary>
    public static IReadOnlyList<(string Code, string NativeName)> SupportedLanguages { get; } = new[]
    {
        ("en", "English"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
    };

    /// <summary>The culture strings are currently looked up in.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(ResolveSupported(SystemUICulture));

    /// <summary>Raised after <see cref="SetLanguage"/> switches the language.</summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>
    /// Switches the UI language. <paramref name="language"/> is a code from
    /// <see cref="SupportedLanguages"/>, or <see cref="SystemLanguage"/>/null/unknown to follow Windows.
    /// </summary>
    public static void SetLanguage(string? language)
    {
        var code = SupportedLanguages.Any(l => l.Code == language) ? language! : ResolveSupported(SystemUICulture);
        var culture = CultureInfo.GetCultureInfo(code);

        // UI culture only: CurrentCulture (number/date parsing) is left alone so existing
        // parsing code keeps its behaviour; culture-aware display formats use Loc.Culture.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (Equals(culture, Culture))
            return;

        Culture = culture;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Maps any culture to the closest supported language: Traditional Chinese for zh-Hant and
    /// the regions that use it (TW, HK, MO), Simplified Chinese for other zh cultures, else English.
    /// </summary>
    public static string ResolveSupported(CultureInfo culture)
    {
        for (var c = culture; !Equals(c, CultureInfo.InvariantCulture); c = c.Parent)
        {
            switch (c.Name)
            {
                case "zh-Hant" or "zh-TW" or "zh-HK" or "zh-MO" or "zh-CHT":
                    return "zh-Hant";
                case "zh-Hans" or "zh-CN" or "zh-SG" or "zh-CHS" or "zh":
                    return "zh-Hans";
            }
        }

        return "en";
    }

    /// <summary>The string for <paramref name="key"/> in the current language.</summary>
    public static string Get(string key) => Resources.GetString(key, Culture) ?? key;

    /// <summary><see cref="Get"/> with <see cref="string.Format(IFormatProvider, string, object[])"/> placeholders filled in.</summary>
    public static string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);

    /// <summary>All keys in the English (neutral) resources; used by tests to check translations.</summary>
    public static IEnumerable<string> GetAllKeys(CultureInfo? culture = null)
    {
        var set = Resources.GetResourceSet(culture ?? CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        if (set == null)
            yield break;

        foreach (System.Collections.DictionaryEntry entry in set)
            yield return (string)entry.Key;
    }
}
