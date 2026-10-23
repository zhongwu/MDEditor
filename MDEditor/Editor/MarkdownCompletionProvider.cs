using System.IO;
using System.Text.RegularExpressions;

namespace MDEditor.Editor;

/// <summary>
/// Produces completion suggestions for Markdown syntax, emoji and file paths.
/// Pure string logic; the caller supplies the document text, caret offset and
/// the current file path.
/// </summary>
public static partial class MarkdownCompletionProvider
{
    private const int MaxSuggestions = 12;
    private const int MaxPathSuggestions = 20;

    [GeneratedRegex(@"(?<![\w:]):([a-z0-9_+\-]{0,24})$", RegexOptions.IgnoreCase)]
    private static partial Regex EmojiPattern();

    [GeneratedRegex(@"^[ \t]*(#{1,6}|[-*+]|\||>|\d+\.)[ \t]*$")]
    private static partial Regex LineMarkerPattern();

    [GeneratedRegex(@"(?:[A-Za-z]:[\\/]|[\w.\-]*[\\/])[^\s<>()\[\]{}]*$")]
    private static partial Regex PathTokenPattern();

    private static readonly (string Name, string Glyph)[] Emoji =
    {
        ("smile", "😄"), ("grin", "😁"), ("joy", "😂"), ("laughing", "😆"),
        ("wink", "😉"), ("blush", "😊"), ("heart_eyes", "😍"), ("thinking", "🤔"),
        ("neutral_face", "😐"), ("cry", "😢"), ("sob", "😭"), ("angry", "😠"),
        ("thumbsup", "👍"), ("+1", "👍"), ("thumbsdown", "👎"), ("-1", "👎"),
        ("ok_hand", "👌"), ("clap", "👏"), ("wave", "👋"), ("pray", "🙏"),
        ("muscle", "💪"), ("point_right", "👉"), ("point_left", "👈"), ("eyes", "👀"),
        ("heart", "❤️"), ("broken_heart", "💔"), ("sparkling_heart", "💖"), ("star", "⭐"),
        ("star2", "🌟"), ("zap", "⚡"), ("fire", "🔥"), ("boom", "💥"),
        ("tada", "🎉"), ("rocket", "🚀"), ("100", "💯"), ("white_check_mark", "✅"),
        ("heavy_check_mark", "✔️"), ("x", "❌"), ("warning", "⚠️"), ("no_entry", "⛔"),
        ("bulb", "💡"), ("memo", "📝"), ("book", "📖"), ("books", "📚"),
        ("rocket_launch", "🚀"), ("bug", "🐛"), ("wrench", "🔧"), ("hammer", "🔨"),
        ("gear", "⚙️"), ("computer", "💻"), ("keyboard", "⌨️"), ("floppy_disk", "💾"),
        ("cd", "💿"), ("file_folder", "📁"), ("open_file_folder", "📂"), ("page_facing_up", "📄"),
        ("clipboard", "📋"), ("pushpin", "📌"), ("paperclip", "📎"), ("link", "🔗"),
        ("lock", "🔒"), ("unlock", "🔓"), ("key", "🔑"), ("shield", "🛡️"),
        ("mag", "🔍"), ("microscope", "🔬"), ("test_tube", "🧪"), ("dna", "🧬"),
        ("chart_with_upwards_trend", "📈"), ("bar_chart", "📊"), ("abacus", "🧮"), ("triangular_ruler", "📐"),
        ("straight_ruler", "📏"), ("clock1", "🕐"), ("hourglass", "⌛"), ("calendar", "📅"),
        ("alarm_clock", "⏰"), ("stopwatch", "⏱️"), ("coffee", "☕"), ("beer", "🍺"),
        ("pizza", "🍕"), ("apple", "🍎"), ("sunny", "☀️"), ("cloud", "☁️"),
        ("umbrella", "☔"), ("snowflake", "❄️"), ("earth_asia", "🌏"), ("globe_with_meridians", "🌐"),
        ("satellite", "🛰️"), ("airplane", "✈️"), ("car", "🚗"), ("bike", "🚲"),
        ("robot", "🤖"), ("brain", "🧠"), ("cat", "🐱"), ("dog", "🐶"),
        ("bee", "🐝"), ("ant", "🐜"), ("snake", "🐍"), ("turtle", "🐢"),
        ("octocat", "🐙"), ("whale", "🐳"), ("dolphin", "🐬"), ("crab", "🦀"),
        ("sparkles", "✨"), ("crown", "👑"), ("trophy", "🏆"), ("medal_sports", "🏅"),
        ("gift", "🎁"), ("balloon", "🎈"), ("art", "🎨"), ("musical_note", "🎵"),
        ("headphones", "🎧"), ("camera", "📷"), ("video_camera", "📹"), ("movie_camera", "🎥"),
        ("phone", "☎️"), ("email", "📧"), ("envelope", "✉️"), ("incoming_envelope", "📨"),
        ("speech_balloon", "💬"), ("thought_balloon", "💭"), ("question", "❓"), ("exclamation", "❗"),
        ("information_source", "ℹ️"), ("recycle", "♻️"), ("arrow_right", "➡️"), ("arrow_left", "⬅️"),
        ("arrow_up", "⬆️"), ("arrow_down", "⬇️"), ("repeat", "🔁"), ("infinity", "♾️")
    };

    private static readonly CompletionItem[] Snippets =
    {
        new("Bold", "**text**", "Bold text (Ctrl+B)", 2) { Glyph = "B" },
        new("Italic", "*text*", "Italic text (Ctrl+I)", 1) { Glyph = "I" },
        new("Strikethrough", "~~text~~", "Strikethrough text", 2) { Glyph = "S" },
        new("Inline code", "`code`", "Inline code span", 1) { Glyph = "<>" },
        new("Code fence", "```\n\n```", "Fenced code block", 4) { Glyph = "{}" },
        new("Heading 1", "# ", "Level 1 heading", 2) { Glyph = "H1" },
        new("Heading 2", "## ", "Level 2 heading", 3) { Glyph = "H2" },
        new("Heading 3", "### ", "Level 3 heading", 4) { Glyph = "H3" },
        new("Heading 4", "#### ", "Level 4 heading", 5) { Glyph = "H4" },
        new("Heading 5", "##### ", "Level 5 heading", 6) { Glyph = "H5" },
        new("Heading 6", "###### ", "Level 6 heading", 7) { Glyph = "H6" },
        new("Bulleted list", "- ", "Bulleted list item", 2) { Glyph = "•" },
        new("Numbered list", "1. ", "Numbered list item", 3) { Glyph = "1." },
        new("Task list", "- [ ] ", "Task list item", 6) { Glyph = "☐" },
        new("Blockquote", "> ", "Blockquote", 2) { Glyph = ">" },
        new("Link", "[text](url)", "Inline link", 1) { Glyph = "🔗" },
        new("Image", "![alt](url)", "Image", 2) { Glyph = "🖼️" },
        new("Table", "| Column | Column |\n| --- | --- |\n| Cell | Cell |", "Table skeleton", 2) { Glyph = "▦" },
        new("Horizontal rule", "\n---\n", "Horizontal rule", 5) { Glyph = "—" },
        new("Math block", "$$\n\n$$", "Display math", 3) { Glyph = "∑" },
        new("Inline math", "$ $", "Inline math", 1) { Glyph = "∑" },
        new("Footnote", "[^1]", "Footnote reference", 3) { Glyph = "[^]" },
        new("Mermaid diagram", "```mermaid\n\n```", "Mermaid diagram", 12) { Glyph = "◇" }
    };

    public static CompletionResult? GetCompletions(
        string text,
        int caretOffset,
        string? currentFilePath,
        CompletionTrigger trigger)
    {
        if (caretOffset < 0 || caretOffset > text.Length)
            return null;

        if (trigger == CompletionTrigger.Manual)
            return GetSnippets(text, caretOffset);

        // Type faces are only expanded outside code so that code stays literal.
        bool insideCode = SmartTypography.IsInsideCodeOrFrontMatter(text, caretOffset);
        if (insideCode)
            return null;

        var emoji = GetEmoji(text, caretOffset);

        return emoji
            ?? GetPath(text, caretOffset, currentFilePath)
            ?? GetLineMarker(text, caretOffset);
    }

    private static CompletionResult? GetEmoji(string text, int caretOffset)
    {
        string prefix = text[..caretOffset];
        var match = EmojiPattern().Match(prefix);
        if (!match.Success)
            return null;

        string query = match.Groups[1].Value;
        var items = Emoji
            .Where(e => e.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name.Length)
            .Take(MaxSuggestions)
            .Select(e => new CompletionItem($":{e.Name}:", $":{e.Name}: ", e.Name.Replace('_', ' '), $":{e.Name}: ".Length)
            {
                Glyph = e.Glyph
            })
            .ToList();

        return items.Count == 0
            ? null
            : new CompletionResult(match.Index, caretOffset, items, "emoji");
    }

    private static CompletionResult? GetPath(string text, int caretOffset, string? currentFilePath)
    {
        if (string.IsNullOrEmpty(currentFilePath))
            return null;

        string prefix = text[..caretOffset];
        var match = PathTokenPattern().Match(prefix);
        if (!match.Success)
            return null;

        string token = match.Value;
        if (token.Contains("://", StringComparison.Ordinal) || token.StartsWith("//", StringComparison.Ordinal))
            return null;

        string baseDirectory;
        string fragment;
        int lastSeparator = token.LastIndexOfAny(new[] { '/', '\\' });

        bool isAbsolute = token.StartsWith('/')
            || token.StartsWith('\\')
            || (token.Length > 1 && token[1] == ':');

        if (isAbsolute)
        {
            baseDirectory = token[..(lastSeparator + 1)];
            fragment = token[(lastSeparator + 1)..];
        }
        else
        {
            baseDirectory = Path.GetDirectoryName(currentFilePath) ?? string.Empty;
            if (baseDirectory.Length == 0)
                return null;

            string relative = token[..(lastSeparator + 1)];
            baseDirectory = Path.GetFullPath(Path.Combine(baseDirectory, relative));
            fragment = token[(lastSeparator + 1)..];
        }

        if (!Directory.Exists(baseDirectory))
            return null;

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(baseDirectory)
                .EnumerateFileSystemInfos()
                .Where(e => !e.Name.StartsWith('.') || fragment.StartsWith('.'))
                .Where(e => e.Name.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e is DirectoryInfo)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxPathSuggestions)
                .ToList();
        }
        catch (Exception)
        {
            return null;
        }

        var items = new List<CompletionItem>();
        foreach (var entry in entries)
        {
            bool isDirectory = entry is DirectoryInfo;
            string insertToken = token[..(lastSeparator + 1)] + entry.Name + (isDirectory ? "/" : string.Empty);
            items.Add(new CompletionItem(
                entry.Name + (isDirectory ? "/" : string.Empty),
                insertToken,
                isDirectory ? "Folder" : "File",
                insertToken.Length)
            {
                Glyph = isDirectory ? "📁" : "📄"
            });
        }

        return items.Count == 0
            ? null
            : new CompletionResult(match.Index, caretOffset, items, "path");
    }

    private static CompletionResult? GetLineMarker(string text, int caretOffset)
    {
        int lineStart = text.LastIndexOf('\n', Math.Max(caretOffset - 1, 0));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        string line = text[lineStart..caretOffset];

        if (!LineMarkerPattern().IsMatch(line))
            return null;

        string marker = line.TrimStart();
        var items = marker switch
        {
            var m when m.StartsWith('#') => Snippets.Where(s => s.Display.StartsWith("Heading")).ToList(),
            ">" => Snippets.Where(s => s.Display == "Blockquote").ToList(),
            "|" => Snippets.Where(s => s.Display == "Table").ToList(),
            var m when m.EndsWith('.') => Snippets.Where(s => s.Display == "Numbered list").ToList(),
            _ => Snippets.Where(s => s.Display is "Bulleted list" or "Task list").ToList()
        };

        return items.Count == 0
            ? null
            : new CompletionResult(lineStart, caretOffset, items, "syntax");
    }

    private static CompletionResult? GetSnippets(string text, int caretOffset)
    {
        int tokenStart = caretOffset;
        while (tokenStart > 0 && (char.IsLetterOrDigit(text[tokenStart - 1]) || text[tokenStart - 1] == '#'))
            tokenStart--;

        string query = text[tokenStart..caretOffset].TrimStart('#');

        // Manual completion shows the whole snippet catalogue (the popup scrolls).
        var items = Snippets
            .Where(s => query.Length == 0
                || s.Display.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                || s.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();

        return items.Count == 0
            ? null
            : new CompletionResult(tokenStart, caretOffset, items, "snippet");
    }
}
