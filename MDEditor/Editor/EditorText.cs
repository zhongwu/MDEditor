using System.Text;
using System.Windows.Documents;

namespace MDEditor.Editor;

/// <summary>
/// Converts between WPF text pointers and plain character offsets.
/// Every method here uses the same walk, so offsets stay consistent:
/// one '\n' per paragraph, text runs concatenated, formatting ignored.
/// </summary>
public static class EditorText
{
    public static string GetText(FlowDocument document)
    {
        var builder = new StringBuilder();
        var position = document.ContentStart;

        while (position != null)
        {
            switch (position.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    var run = position.GetTextInRun(LogicalDirection.Forward);
                    builder.Append(run);
                    position = position.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
                    break;

                case TextPointerContext.ElementEnd when position.Parent is Paragraph:
                    builder.Append('\n');
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;

                default:
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Gets the character offset of <paramref name="target"/> within the document.</summary>
    public static int GetOffset(FlowDocument document, TextPointer target)
    {
        int offset = 0;
        var position = document.ContentStart;

        while (position != null && position.CompareTo(target) < 0)
        {
            switch (position.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    var run = position.GetTextInRun(LogicalDirection.Forward);
                    var next = position.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
                    if (next == null || next.CompareTo(target) > 0)
                    {
                        // Target sits inside this run: walk it character by character.
                        for (int i = 0; i < run.Length; i++)
                        {
                            var step = position.GetPositionAtOffset(i + 1, LogicalDirection.Forward);
                            if (step == null || step.CompareTo(target) >= 0)
                                return offset + i;
                        }

                        return offset + run.Length;
                    }

                    offset += run.Length;
                    position = next;
                    break;

                case TextPointerContext.ElementEnd when position.Parent is Paragraph:
                    offset++;
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;

                default:
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return offset;
    }

    /// <summary>Finds the text pointer for a character offset, or null when out of range.</summary>
    public static TextPointer? GetPointer(FlowDocument document, int targetOffset)
    {
        if (targetOffset < 0)
            return null;

        int offset = 0;
        var position = document.ContentStart;

        while (position != null)
        {
            switch (position.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    var run = position.GetTextInRun(LogicalDirection.Forward);
                    if (targetOffset <= offset + run.Length)
                        return position.GetPositionAtOffset(targetOffset - offset, LogicalDirection.Forward);

                    offset += run.Length;
                    position = position.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
                    break;

                case TextPointerContext.ElementEnd when position.Parent is Paragraph:
                    if (targetOffset <= offset)
                        return position;

                    offset++;
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;

                default:
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves many offsets in a single document walk. <paramref name="sortedOffsets"/>
    /// must be ascending; entries past the end of the document stay null.
    /// </summary>
    public static TextPointer?[] GetPointers(FlowDocument document, IReadOnlyList<int> sortedOffsets)
    {
        var result = new TextPointer?[sortedOffsets.Count];
        int next = 0;
        int offset = 0;
        var position = document.ContentStart;

        while (position != null && next < sortedOffsets.Count)
        {
            switch (position.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    var run = position.GetTextInRun(LogicalDirection.Forward);
                    while (next < sortedOffsets.Count && sortedOffsets[next] <= offset + run.Length)
                    {
                        result[next] = position.GetPositionAtOffset(Math.Max(0, sortedOffsets[next] - offset), LogicalDirection.Forward);
                        next++;
                    }

                    offset += run.Length;
                    position = position.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
                    break;

                case TextPointerContext.ElementEnd when position.Parent is Paragraph:
                    while (next < sortedOffsets.Count && sortedOffsets[next] <= offset)
                    {
                        result[next] = position;
                        next++;
                    }

                    offset++;
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;

                default:
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return result;
    }
}
