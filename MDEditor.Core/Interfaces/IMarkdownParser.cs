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
    /// Converts Markdown text to HTML with a complete HTML document wrapper.
    /// </summary>
    /// <param name="markdown">The Markdown content to convert.</param>
    /// <returns>Complete HTML document.</returns>
    string ToHtmlDocument(string markdown);

    /// <summary>
    /// Extracts the heading outline (table of contents) of the Markdown content.
    /// Heading ids match the ids generated in the rendered HTML.
    /// </summary>
    /// <param name="markdown">The Markdown content to inspect.</param>
    /// <returns>Top-level headings, each with nested sub-headings.</returns>
    IReadOnlyList<OutlineItem> GetOutline(string markdown);
}
