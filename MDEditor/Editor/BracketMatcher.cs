namespace MDEditor.Editor;

/// <summary>
/// Finds the bracket that pairs with the one next to the caret.
/// Pure string logic so it can be unit tested without WPF.
/// </summary>
public static class BracketMatcher
{
    private const string Openers = "([{";
    private const string Closers = ")]}";

    /// <summary>
    /// Looks for a bracket immediately after or before <paramref name="caretOffset"/>.
    /// Returns true and the character offsets of the pair when both are found.
    /// </summary>
    public static bool TryFindMatch(string text, int caretOffset, out int openOffset, out int closeOffset)
    {
        openOffset = -1;
        closeOffset = -1;

        if (string.IsNullOrEmpty(text))
            return false;

        if (caretOffset < 0 || caretOffset > text.Length)
            return false;

        // Prefer the character to the right of the caret, then the one to the left.
        if (caretOffset < text.Length)
        {
            char next = text[caretOffset];

            int openerIndex = Openers.IndexOf(next);
            if (openerIndex >= 0 && !IsEscaped(text, caretOffset))
                return ScanForward(text, caretOffset, next, Closers[openerIndex], out openOffset, out closeOffset);

            int closerIndex = Closers.IndexOf(next);
            if (closerIndex >= 0 && !IsEscaped(text, caretOffset))
                return ScanBackward(text, caretOffset, Openers[closerIndex], next, out openOffset, out closeOffset);
        }

        if (caretOffset > 0)
        {
            int previousIndex = caretOffset - 1;
            char previous = text[previousIndex];

            int closerIndex = Closers.IndexOf(previous);
            if (closerIndex >= 0 && !IsEscaped(text, previousIndex))
                return ScanBackward(text, previousIndex, Openers[closerIndex], previous, out openOffset, out closeOffset);

            int openerIndex = Openers.IndexOf(previous);
            if (openerIndex >= 0 && !IsEscaped(text, previousIndex))
                return ScanForward(text, previousIndex, previous, Closers[openerIndex], out openOffset, out closeOffset);
        }

        return false;
    }

    private static bool ScanForward(string text, int start, char open, char close, out int openOffset, out int closeOffset)
    {
        openOffset = start;
        closeOffset = -1;

        int depth = 0;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c == open && !IsEscaped(text, i))
            {
                depth++;
            }
            else if (c == close && !IsEscaped(text, i))
            {
                depth--;
                if (depth == 0)
                {
                    closeOffset = i;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ScanBackward(string text, int start, char open, char close, out int openOffset, out int closeOffset)
    {
        openOffset = -1;
        closeOffset = start;

        int depth = 0;
        for (int i = start; i >= 0; i--)
        {
            char c = text[i];
            if (c == close && !IsEscaped(text, i))
            {
                depth++;
            }
            else if (c == open && !IsEscaped(text, i))
            {
                depth--;
                if (depth == 0)
                {
                    openOffset = i;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEscaped(string text, int index)
    {
        int backslashes = 0;
        for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
            backslashes++;

        return backslashes % 2 == 1;
    }
}
