namespace MDEditor.Editor;

/// <summary>A single auto-complete suggestion.</summary>
public sealed record CompletionItem(string Display, string Insert, string Description, int CaretOffsetInInsert)
{
    /// <summary>Optional emoji glyph or icon shown before the display text.</summary>
    public string? Glyph { get; init; }
}

/// <summary>What triggered a completion request.</summary>
public enum CompletionTrigger
{
    /// <summary>Normal typing: emoji, file paths and line-start Markdown syntax.</summary>
    Typing,

    /// <summary>Explicit request (Ctrl+Space): every Markdown snippet.</summary>
    Manual
}

/// <summary>Completions for one token, with the range they replace.</summary>
public sealed record CompletionResult(
    int TokenStart,
    int TokenEnd,
    IReadOnlyList<CompletionItem> Items,
    string Kind);
