using MDEditor.Core.Models;

namespace MDEditor.Core.Interfaces;

/// <summary>
/// Service for parsing Markdown content into HTML.
/// </summary>
public interface IMarkdownParser
{
    /// <summary>
    /// Converts Markdown text to HTML.
    /// </summary>
    /// <param name="markdown">The Markdown content to convert.</param>
    /// <returns>HTML representation of the Markdown content.</returns>
    string ToHtml(string markdown);

    /// <summary>
    /// Converts Markdown text to HTML with a complete HTML document wrapper. Always portable/
    /// light-themed with no custom CSS - used for export and copy, where the output should not
    /// depend on the app's current theme or preview-only customization.
    /// </summary>
    /// <param name="markdown">The Markdown content to convert.</param>
    /// <returns>Complete HTML document.</returns>
    string ToHtmlDocument(string markdown);

    /// <summary>
    /// Converts Markdown text to a complete HTML document for live rendering (preview /
    /// presentation mode), applying dark-theme CSS and/or the user's custom CSS on top of the
    /// base stylesheet.
    /// </summary>
    string ToHtmlDocument(string markdown, PreviewRenderOptions previewOptions);

    /// <summary>
    /// Extracts the heading outline (table of contents) of the Markdown content.
    /// Heading ids match the ids generated in the rendered HTML.
    /// </summary>
    /// <param name="markdown">The Markdown content to inspect.</param>
    /// <returns>Top-level headings, each with nested sub-headings.</returns>
    IReadOnlyList<OutlineItem> GetOutline(string markdown);

    /// <summary>
    /// Replaces the markdown-flavor options and rebuilds the rendering pipeline accordingly.
    /// Affects every output (preview, export, copy) since these describe what markup is
    /// understood at all. No-ops if <paramref name="options"/> matches the current options.
    /// </summary>
    void UpdateOptions(MarkdownParserOptions options);
}
