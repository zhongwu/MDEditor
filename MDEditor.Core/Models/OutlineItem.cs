namespace MDEditor.Core.Models;

/// <summary>
/// A heading in the document outline (table of contents).
/// </summary>
public sealed class OutlineItem
{
    public OutlineItem(int level, string title, int line, string? id)
    {
        Level = level;
        Title = title;
        Line = line;
        Id = id;
    }

    /// <summary>Heading level, 1-6.</summary>
    public int Level { get; }

    /// <summary>Plain-text heading title (markup stripped).</summary>
    public string Title { get; }

    /// <summary>Zero-based source line of the heading.</summary>
    public int Line { get; }

    /// <summary>HTML id generated for the heading in the rendered preview, if any.</summary>
    public string? Id { get; }

    /// <summary>Nested sub-headings.</summary>
    public List<OutlineItem> Children { get; } = new();

    /// <summary>
    /// Builds a nested tree from headings in document order. A heading becomes a
    /// child of the nearest preceding heading with a lower level, so skipped
    /// levels (e.g. H1 followed directly by H3) still nest sensibly.
    /// </summary>
    public static List<OutlineItem> BuildTree(IEnumerable<OutlineItem> headings)
    {
        var roots = new List<OutlineItem>();
        var stack = new Stack<OutlineItem>();

        foreach (var heading in headings)
        {
            while (stack.Count > 0 && stack.Peek().Level >= heading.Level)
                stack.Pop();

            if (stack.Count == 0)
                roots.Add(heading);
            else
                stack.Peek().Children.Add(heading);

            stack.Push(heading);
        }

        return roots;
    }
}
