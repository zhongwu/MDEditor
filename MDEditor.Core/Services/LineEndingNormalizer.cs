namespace MDEditor.Core.Services;

/// <summary>
/// Normalizes line endings before writing a file to disk.
/// </summary>
public static class LineEndingNormalizer
{
    /// <summary>Collapses all line-ending variants to \n, then expands to the target ending.</summary>
    /// <param name="text">The text to normalize.</param>
    /// <param name="targetEnding">"CRLF" or "LF" (anything else is treated as "LF").</param>
    public static string Normalize(string text, string targetEnding)
    {
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return targetEnding == "CRLF" ? normalized.Replace("\n", "\r\n") : normalized;
    }
}
