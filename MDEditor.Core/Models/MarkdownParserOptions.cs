namespace MDEditor.Core.Models;

/// <summary>
/// Markdown-flavor toggles for the shared rendering pipeline. These affect every output
/// (preview, export, copy) since they describe what markup is understood at all, not how it
/// looks.
/// </summary>
public sealed class MarkdownParserOptions
{
    public static MarkdownParserOptions Default { get; } = new();

    public bool EnableMath { get; init; } = true;
    public bool EnableDiagrams { get; init; } = true;
    public bool EnableEmoji { get; init; } = true;
    public bool EnableFootnotes { get; init; } = true;
    public bool EnableTaskLists { get; init; } = true;

    public bool Equals(MarkdownParserOptions? other) =>
        other is not null &&
        EnableMath == other.EnableMath &&
        EnableDiagrams == other.EnableDiagrams &&
        EnableEmoji == other.EnableEmoji &&
        EnableFootnotes == other.EnableFootnotes &&
        EnableTaskLists == other.EnableTaskLists;

    public override bool Equals(object? obj) => Equals(obj as MarkdownParserOptions);

    public override int GetHashCode() =>
        HashCode.Combine(EnableMath, EnableDiagrams, EnableEmoji, EnableFootnotes, EnableTaskLists);
}
