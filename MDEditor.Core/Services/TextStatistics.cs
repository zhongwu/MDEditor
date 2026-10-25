namespace MDEditor.Core.Services;

/// <summary>Numbers shown in the status bar.</summary>
public static class TextStatistics
{
    /// <summary>
    /// Words in <paramref name="text"/>: runs of letters/digits count once, and every CJK
    /// ideograph, kana or hangul syllable counts on its own since those scripts have no spaces.
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 0;
        bool inWord = false;
        foreach (var ch in text)
        {
            if (IsCjk(ch))
            {
                count++;
                inWord = false;
            }
            else if (char.IsLetterOrDigit(ch) || (ch is '\'' or '’') && inWord)
            {
                if (!inWord)
                    count++;
                inWord = true;
            }
            else
            {
                inWord = false;
            }
        }
        return count;
    }

    /// <summary>1-based line and column of the caret, given all the text before it.</summary>
    public static (int Line, int Column) GetLineColumn(string textBeforeCaret)
    {
        int line = 1, lineStart = 0;
        for (int i = 0; i < textBeforeCaret.Length; i++)
        {
            if (textBeforeCaret[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        return (line, textBeforeCaret.Length - lineStart + 1);
    }

    private static bool IsCjk(char ch) =>
        ch is >= '぀' and <= 'ヿ'   // kana
            or >= '㐀' and <= '䶿'  // CJK extension A
            or >= '一' and <= '鿿'  // CJK ideographs
            or >= '가' and <= '힯'  // hangul syllables
            or >= '豈' and <= '﫿'; // compatibility ideographs
}
