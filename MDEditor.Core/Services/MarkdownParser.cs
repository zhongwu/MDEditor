using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;

namespace MDEditor.Core.Services;

/// <summary>
/// Service for parsing Markdown content into HTML using Markdig.
/// </summary>
public class MarkdownParser : IMarkdownParser
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseGridTables()
            .UsePipeTables()
            .UseMathematics()
            .UseDiagrams()
            .UseFootnotes()
            .UseDefinitionLists()
            .UseAutoIdentifiers()
            .UseGenericAttributes()
            .UseAbbreviations()
            .UseCitations()
            .UseCustomContainers()
            .UseFigures()
            .UseMediaLinks()
            .UseSmartyPants()
            .UseTaskLists()
            .Build();
    }

    /// <summary>
    /// Converts Markdown text to HTML.
    /// </summary>
    public string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown, _pipeline);
    }

    /// <summary>
    /// Extracts the heading outline. Uses the same pipeline as rendering so
    /// heading ids (from UseAutoIdentifiers / generic attributes) match the preview.
    /// </summary>
    public IReadOnlyList<OutlineItem> GetOutline(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<OutlineItem>();
        }

        var document = Markdown.Parse(markdown, _pipeline);
        var headings = new List<OutlineItem>();

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var title = GetHeadingText(heading);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            headings.Add(new OutlineItem(
                Math.Clamp(heading.Level, 1, 6),
                title,
                heading.Line,
                heading.TryGetAttributes()?.Id));
        }

        return OutlineItem.BuildTree(headings);
    }

    private string GetHeadingText(HeadingBlock heading)
    {
        if (heading.Inline == null)
            return string.Empty;

        // Render the heading inlines with HTML disabled: this yields the same text the
        // preview shows (emoji, smart quotes, entities, link text) without any markup.
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer) { EnableHtmlForInline = false };
        _pipeline.Setup(renderer);
        renderer.WriteChildren(heading.Inline);
        writer.Flush();

        var text = WebUtility.HtmlDecode(writer.ToString());
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Converts Markdown text to HTML with a complete HTML document wrapper.
    /// </summary>
    public string ToHtmlDocument(string markdown)
    {
        var bodyHtml = ToHtml(markdown);

        return
            "<!DOCTYPE html>\n" +
            "<html lang=\"en\">\n" +
            "<head>\n" +
            "    <meta charset=\"utf-8\">\n" +
            "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
            "    <link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.css\">\n" +
            "    <script src=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.js\"></script>\n" +
            "    <script src=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/contrib/auto-render.min.js\"></script>\n" +
            "    <script src=\"https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js\"></script>\n" +
            "    <style>\n" +
            "        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; line-height: 1.6; padding: 20px; max-width: 900px; margin: 0 auto; color: #24292f; background-color: #ffffff; }\n" +
            "        h1, h2, h3, h4, h5, h6 { margin-top: 24px; margin-bottom: 16px; font-weight: 600; line-height: 1.25; }\n" +
            "        h1 { border-bottom: 1px solid #eaecef; padding-bottom: 10px; font-size: 2em; }\n" +
            "        h2 { border-bottom: 1px solid #eaecef; padding-bottom: 10px; font-size: 1.5em; }\n" +
            "        h3 { font-size: 1.25em; }\n" +
            "        h4 { font-size: 1em; }\n" +
            "        p, blockquote, ul, ol, dl, table, pre { margin-top: 0; margin-bottom: 16px; }\n" +
            "        code { font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace; font-size: 0.9em; padding: 0.2em 0.4em; margin: 0; background-color: rgba(27, 31, 35, 0.1); border-radius: 3px; }\n" +
            "        pre { padding: 16px; overflow: auto; line-height: 1.45; background-color: #f6f8fa; border-radius: 6px; border: 1px solid #dfe2e5; }\n" +
            "        pre code { padding: 0; margin: 0; font-size: 1em; background-color: transparent; border: none; }\n" +
            "        blockquote { padding: 0 1em; color: #6a737d; border-left: 0.25em solid #dfe2e5; margin: 0; }\n" +
            "        table { border-collapse: collapse; width: 100%; }\n" +
            "        table th { font-weight: 600; background-color: #f6f8fa; border: 1px solid #dfe2e5; padding: 6px 13px; }\n" +
            "        table td { border: 1px solid #dfe2e5; padding: 6px 13px; }\n" +
            "        table tr { background-color: #fff; border-top: 1px solid #c6cbd1; }\n" +
            "        a { color: #0366d6; text-decoration: none; }\n" +
            "        a:hover { text-decoration: underline; }\n" +
            "        .task-list-item { list-style-type: none; }\n" +
            "        .task-list-item input { margin-right: 0.5em; }\n" +
            "        figure { margin: 1em 0; text-align: center; }\n" +
            "        figcaption { font-size: 0.9em; color: #6a737d; margin-top: 0.5em; }\n" +
            "        .footnotes { margin-top: 2em; border-top: 1px solid #eaecef; padding-top: 1em; font-size: 0.9em; }\n" +
            "        .footnote-ref { font-size: 0.8em; vertical-align: super; }\n" +
            "        abbr { border-bottom: 1px dotted #6a737d; cursor: help; }\n" +
            "        .citation { color: #0366d6; }\n" +
            "        .alert { padding: 1em; margin: 1em 0; border-radius: 6px; border-left: 4px solid; }\n" +
            "        .alert-info { background: #e7f3ff; border-color: #0366d6; }\n" +
            "        .alert-warning { background: #fff8e1; border-color: #f9a825; }\n" +
            "        .alert-danger { background: #ffebee; border-color: #d32f2f; }\n" +
            "        details { margin: 1em 0; }\n" +
            "        summary { cursor: pointer; font-weight: 600; }\n" +
            "        @media print {\n" +
            "            body { max-width: none; padding: 0; }\n" +
            "            pre, blockquote, table, figure, img, .mermaid, .katex-display { page-break-inside: avoid; break-inside: avoid; }\n" +
            "            h1, h2, h3, h4, h5, h6 { page-break-after: avoid; break-after: avoid; }\n" +
            "            pre { white-space: pre-wrap; word-wrap: break-word; }\n" +
            "            a { color: inherit; }\n" +
            "        }\n" +
            "    </style>\n" +
            "</head>\n" +
            "<body>\n" +
            bodyHtml +
            "<script>\n" +
            "    // Initialize KaTeX math rendering\n" +
            "    document.addEventListener('DOMContentLoaded', function() {\n" +
            "        if (typeof renderMathInElement !== 'undefined') {\n" +
            "            renderMathInElement(document.body, {\n" +
            "                // Markdig already converts $...$ and $$...$$ to \\(...\\) and \\[...\\],\n" +
            "                // so only those delimiters are needed here. Leaving '$' enabled lets\n" +
            "                // stray dollar signs pair up and swallow chunks of the document.\n" +
            "                delimiters: [\n" +
            "                    {left: '\\\\(', right: '\\\\)', display: false},\n" +
            "                    {left: '\\\\[', right: '\\\\]', display: true}\n" +
            "                ]\n" +
            "            });\n" +
            "        }\n" +
            "    });\n" +
            "    // Initialize Mermaid diagrams\n" +
            "    if (typeof mermaid !== 'undefined') {\n" +
            "        mermaid.initialize({ startOnLoad: false, theme: 'default' });\n" +
            "        document.addEventListener('DOMContentLoaded', function() {\n" +
            "            mermaid.run();\n" +
            "        });\n" +
            "    }\n" +
            "</script>\n" +
            "</body>\n" +
            "</html>";
    }
}
