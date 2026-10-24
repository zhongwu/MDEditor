using System.Text.RegularExpressions;

namespace MDEditor.Core.Services;

/// <summary>
/// Resolves relative image URLs in rendered HTML against a base URL. The preview is loaded with
/// NavigateToString (no document URL), so relative &lt;img src&gt; values would otherwise point
/// nowhere. Only images are rewritten - unlike &lt;base href&gt;, this leaves in-page anchors
/// (#section, footnotes) working.
/// </summary>
public static partial class ImageUrlRewriter
{
    // Matches both Markdig output (src="...") and hand-written HTML (src='...').
    [GeneratedRegex(@"(<img\b[^>]*?\bsrc\s*=\s*)([""'])(.*?)\2", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ImgSrcPattern();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:")]
    private static partial Regex SchemePattern();

    /// <param name="folderUrl">Base for document-relative paths ("img/a.png"); must end with '/'.</param>
    /// <param name="rootUrl">Base for root-relative paths ("/docs/a.png"); must end with '/'.</param>
    public static string Rewrite(string html, string folderUrl, string rootUrl)
    {
        if (string.IsNullOrEmpty(html) || html.IndexOf("<img", StringComparison.OrdinalIgnoreCase) < 0)
            return html;

        return ImgSrcPattern().Replace(html, m =>
        {
            var src = m.Groups[3].Value.Trim();
            var resolved = Resolve(src, folderUrl, rootUrl);
            return resolved == null ? m.Value : m.Groups[1].Value + m.Groups[2].Value + resolved + m.Groups[2].Value;
        });
    }

    /// <summary>The absolute URL for <paramref name="src"/>, or null if it's already absolute/unresolvable.</summary>
    public static string? Resolve(string src, string folderUrl, string rootUrl)
    {
        if (src.Length == 0 || src.StartsWith('#') || src.StartsWith("//", StringComparison.Ordinal) || SchemePattern().IsMatch(src))
            return null; // empty, fragment, protocol-relative, or absolute (https:, data:, ...)

        if (src.StartsWith('/'))
            return rootUrl + src.TrimStart('/');

        if (src.StartsWith("./", StringComparison.Ordinal))
            src = src[2..];

        // "../" segments are left for the browser's URL parser to normalize.
        return folderUrl + src;
    }
}
