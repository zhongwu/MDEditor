using System.Text.RegularExpressions;
using Markdig;
using MDEditor.Core.Interfaces;

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
            "    </style>\n" +
            "</head>\n" +
            "<body>\n" +
            bodyHtml +
            "</body>\n" +
            "</html>";
    }
}
