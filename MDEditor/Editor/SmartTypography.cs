namespace MDEditor.Editor;

/// <summary>
/// Applies typographic replacements (curly quotes, em dash) while typing.
/// Transformations are skipped inside fenced code blocks, inline code spans
/// and YAML front matter so code and metadata stay byte-for-byte intact.
/// </summary>
public static class SmartTypography
{
    private const char OpenDoubleQuote = '\u201C';   // “
    private const char CloseDoubleQuote = '\u201D';  // ”
    private const char OpenSingleQuote = '\u2018';   // ‘
    private const char CloseSingleQuote = '\u2019';  // ’
    private const char EmDash = '\u2014';            // —

    /// <summary>
    /// Returns the replacement for a typed character at <paramref name="caretOffset"/>.
    /// <paramref name="removeBefore"/> characters before the caret are replaced by
    /// <paramref name="insertText"/>.
    /// </summary>
    public static bool TryGetReplacement(
        string text,
        int caretOffset,
        char typed,
        out string insertText,
        out int removeBefore)
    {
        insertText = string.Empty;
        removeBefore = 0;

        if (caretOffset < 0 || caretOffset > text.Length)
            return false;

        if (IsInsideCodeOrFrontMatter(text, caretOffset))
            return false;

        switch (typed)
        {
            case '"':
                insertText = IsOpeningContext(text, caretOffset) ? OpenDoubleQuote.ToString() : CloseDoubleQuote.ToString();
                return true;

            case '\'':
                insertText = IsOpeningContext(text, caretOffset) ? OpenSingleQuote.ToString() : CloseSingleQuote.ToString();
                return true;

            case '-':
                // Only the third dash of a run becomes an em dash, and never at the
                // start of a line (that position is reserved for front matter,
                // setext underlines and horizontal rules).
                if (caretOffset >= 2 &&
                    text[caretOffset - 1] == '-' &&
                    text[caretOffset - 2] == '-' &&
                    !IsAtLineStart(text, caretOffset - 2))
                {
                    insertText = EmDash.ToString();
                    removeBefore = 2;
                    return true;
                }

                return false;
        }

        return false;
    }

    /// <summary>True when a quote typed here should be an opening quote.</summary>
    public static bool IsOpeningContext(string text, int caretOffset)
    {
        if (caretOffset <= 0)
            return true;

        char previous = text[caretOffset - 1];
        return char.IsWhiteSpace(previous)
            || previous is '(' or '[' or '{' or '<' or '"' or '\''
            || previous is '—' or '–' or '-' or '/' or '\\' or '>';
    }

    private static bool IsAtLineStart(string text, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == '\n' || c == '\r')
                return true;
            if (!char.IsWhiteSpace(c))
                return false;
        }

        return true;
    }

    /// <summary>
    /// True when the offset is inside a fenced code block, an inline code span,
    /// a YAML front matter header, or an escaped position.
    /// </summary>
    public static bool IsInsideCodeOrFrontMatter(string text, int caretOffset)
    {
        if (caretOffset < 0)
            return true;
        if (caretOffset > text.Length)
            caretOffset = text.Length;

        // YAML front matter: --- on the very first line, closed by --- or ...
        int contentStart = 0;
        if (text.StartsWith("---"))
        {
            int firstLineEnd = IndexOfLineEnd(text, 0);
            if (firstLineEnd >= 0 && text[..firstLineEnd].TrimEnd('\r').TrimEnd() == "---")
            {
                int scan = firstLineEnd + 1;
                while (scan <= text.Length)
                {
                    int lineEnd = IndexOfLineEnd(text, scan);
                    int end = lineEnd < 0 ? text.Length : lineEnd;
                    string line = text[scan..end].TrimEnd('\r');
                    if (line == "---" || line == "...")
                    {
                        if (caretOffset <= end)
                            return true;
                        contentStart = Math.Min(end + 1, text.Length);
                        break;
                    }

                    if (lineEnd < 0)
                        break;
                    scan = lineEnd + 1;
                }

                if (contentStart == 0)
                    contentStart = text.Length;
            }
        }

        bool inFence = false;
        char fenceChar = '`';
        int fenceLength = 0;
        int lineStart = contentStart;

        while (lineStart < caretOffset && lineStart < text.Length)
        {
            int lineEnd = IndexOfLineEnd(text, lineStart);
            int end = lineEnd < 0 ? text.Length : lineEnd;
            string line = text[lineStart..end].TrimEnd('\r').TrimStart();

            if (!inFence)
            {
                if (TryReadFence(line, out char ch, out int len))
                {
                    inFence = true;
                    fenceChar = ch;
                    fenceLength = len;
                    lineStart = end + 1;
                    continue;
                }
            }
            else if (TryReadFence(line, out char ch, out int len) && ch == fenceChar && len >= fenceLength)
            {
                inFence = false;
                lineStart = end + 1;
                continue;
            }

            if (caretOffset <= end)
                break;

            lineStart = end + 1;
        }

        if (inFence)
            return true;

        // Inline code span on the caret's line: an odd number of unescaped backticks
        // before the caret means the caret is inside the span.
        int lineBegin = text.LastIndexOf('\n', Math.Max(caretOffset - 1, 0));
        lineBegin = lineBegin < 0 ? 0 : lineBegin + 1;
        int ticks = 0;
        for (int i = lineBegin; i < caretOffset && i < text.Length; i++)
        {
            if (text[i] == '`' && (i == 0 || text[i - 1] != '\\'))
                ticks++;
        }

        return ticks % 2 == 1;
    }

    private static bool TryReadFence(string trimmedLine, out char fenceChar, out int length)
    {
        fenceChar = '`';
        length = 0;

        if (trimmedLine.Length < 3)
            return false;

        char first = trimmedLine[0];
        if (first != '`' && first != '~')
            return false;

        int count = 0;
        while (count < trimmedLine.Length && trimmedLine[count] == first)
            count++;

        if (count < 3)
            return false;

        fenceChar = first;
        length = count;
        return true;
    }

    private static int IndexOfLineEnd(string text, int start)
    {
        int index = text.IndexOf('\n', start);
        return index;
    }
}
