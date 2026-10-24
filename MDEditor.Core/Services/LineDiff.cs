namespace MDEditor.Core.Services;

/// <summary>
/// Kind of a line in a <see cref="LineDiff"/> result.
/// </summary>
public enum DiffLineKind
{
    Unchanged,
    Added,
    Removed,

    /// <summary>A collapsed run of unchanged lines (see <see cref="LineDiff.Collapse"/>).</summary>
    Skipped,
}

/// <summary>
/// One line of a diff. Line numbers are 1-based; null where the line doesn't exist on that side.
/// For <see cref="DiffLineKind.Skipped"/>, <see cref="SkippedCount"/> is the number of hidden lines.
/// </summary>
public record DiffLine(DiffLineKind Kind, string Text, int? OldLineNumber, int? NewLineNumber, int SkippedCount = 0);

/// <summary>
/// Line-based text diff (Myers' O(ND) algorithm), used to compare file versions.
/// </summary>
public static class LineDiff
{
    /// <summary>
    /// Above this many edits the full Myers trace gets expensive (memory grows with D²); the
    /// texts are then reported as entirely replaced, which is accurate enough for such rewrites.
    /// </summary>
    public const int MaxEditDistance = 4000;

    /// <summary>
    /// Computes the line diff turning <paramref name="oldText"/> into <paramref name="newText"/>.
    /// Line endings are ignored (CRLF and LF compare equal).
    /// </summary>
    public static List<DiffLine> Compute(string oldText, string newText)
    {
        var a = SplitLines(oldText);
        var b = SplitLines(newText);

        // Trim the common prefix/suffix first - typical edits touch a small middle section,
        // which keeps the Myers search tiny.
        int prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
            prefix++;

        int suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix
               && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix])
            suffix++;

        var result = new List<DiffLine>(Math.Max(a.Length, b.Length) + 8);
        for (int i = 0; i < prefix; i++)
            result.Add(new DiffLine(DiffLineKind.Unchanged, a[i], i + 1, i + 1));

        var middle = DiffMiddle(a, prefix, a.Length - suffix, b, prefix, b.Length - suffix);
        result.AddRange(middle);

        for (int i = suffix; i > 0; i--)
        {
            int ai = a.Length - i, bi = b.Length - i;
            result.Add(new DiffLine(DiffLineKind.Unchanged, a[ai], ai + 1, bi + 1));
        }

        return result;
    }

    /// <summary>
    /// Replaces runs of unchanged lines farther than <paramref name="context"/> lines from any
    /// change with a single <see cref="DiffLineKind.Skipped"/> line.
    /// </summary>
    public static List<DiffLine> Collapse(IReadOnlyList<DiffLine> lines, int context = 3)
    {
        var result = new List<DiffLine>();
        int i = 0;
        while (i < lines.Count)
        {
            if (lines[i].Kind != DiffLineKind.Unchanged)
            {
                result.Add(lines[i++]);
                continue;
            }

            int start = i;
            while (i < lines.Count && lines[i].Kind == DiffLineKind.Unchanged)
                i++;
            int end = i; // exclusive

            int keepHead = start == 0 ? 0 : context;          // context after the previous change
            int keepTail = end == lines.Count ? 0 : context;  // context before the next change
            int hidden = (end - start) - keepHead - keepTail;

            // Hiding a single line saves nothing - the marker takes a row too.
            if (hidden <= 1)
            {
                for (int j = start; j < end; j++)
                    result.Add(lines[j]);
                continue;
            }

            for (int j = start; j < start + keepHead; j++)
                result.Add(lines[j]);
            var firstHidden = lines[start + keepHead];
            result.Add(new DiffLine(DiffLineKind.Skipped, string.Empty, firstHidden.OldLineNumber, firstHidden.NewLineNumber, hidden));
            for (int j = end - keepTail; j < end; j++)
                result.Add(lines[j]);
        }

        return result;
    }

    /// <summary>Counts added and removed lines.</summary>
    public static (int Added, int Removed) Stats(IEnumerable<DiffLine> lines) =>
        (lines.Count(l => l.Kind == DiffLineKind.Added), lines.Count(l => l.Kind == DiffLineKind.Removed));

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
            return Array.Empty<string>();

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        // A trailing newline terminates the last line rather than starting an empty one.
        return text.EndsWith('\n') || text.EndsWith('\r') ? lines[..^1] : lines;
    }

    private static List<DiffLine> DiffMiddle(string[] a, int aStart, int aEnd, string[] b, int bStart, int bEnd)
    {
        int n = aEnd - aStart, m = bEnd - bStart;
        var result = new List<DiffLine>();
        if (n == 0 && m == 0)
            return result;

        var trace = ShortestEditTrace(a, aStart, n, b, bStart, m);
        if (trace == null)
        {
            for (int i = 0; i < n; i++)
                result.Add(new DiffLine(DiffLineKind.Removed, a[aStart + i], aStart + i + 1, null));
            for (int j = 0; j < m; j++)
                result.Add(new DiffLine(DiffLineKind.Added, b[bStart + j], null, bStart + j + 1));
            return result;
        }

        // Backtrack from (n, m) through the saved V snapshots, emitting lines in reverse.
        int x = n, y = m;
        for (int d = trace.Count - 1; d >= 0; d--)
        {
            var v = trace[d]; // V before step d, indexed by k + d
            int k = x - y;
            int prevK = (k == -d || (k != d && V(v, d, k - 1) < V(v, d, k + 1))) ? k + 1 : k - 1;
            // Step 0 is the initial diagonal snake from (0, 0).
            int prevX = d == 0 ? 0 : V(v, d, prevK);
            int prevY = d == 0 ? 0 : prevX - prevK;

            while (x > prevX && y > prevY)
            {
                x--; y--;
                result.Add(new DiffLine(DiffLineKind.Unchanged, a[aStart + x], aStart + x + 1, bStart + y + 1));
            }

            if (d == 0)
                break;

            if (x == prevX)
            {
                y--;
                result.Add(new DiffLine(DiffLineKind.Added, b[bStart + y], null, bStart + y + 1));
            }
            else
            {
                x--;
                result.Add(new DiffLine(DiffLineKind.Removed, a[aStart + x], aStart + x + 1, null));
            }
        }

        result.Reverse();
        return result;
    }

    private static int V(int[] snapshot, int d, int k) => snapshot[k + d];

    /// <summary>
    /// Runs the greedy Myers search, returning for each step d a snapshot of V over k ∈ [-d, d]
    /// taken before that step, or null if the edit distance exceeds <see cref="MaxEditDistance"/>.
    /// </summary>
    private static List<int[]>? ShortestEditTrace(string[] a, int aStart, int n, string[] b, int bStart, int m)
    {
        int max = n + m;
        int offset = max + 1;
        var v = new int[2 * max + 3];
        var trace = new List<int[]>();

        for (int d = 0; d <= max; d++)
        {
            if (d > MaxEditDistance)
                return null;

            var snapshot = new int[2 * d + 1];
            Array.Copy(v, offset - d, snapshot, 0, snapshot.Length);
            trace.Add(snapshot);

            for (int k = -d; k <= d; k += 2)
            {
                int x = (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]))
                    ? v[offset + k + 1]
                    : v[offset + k - 1] + 1;
                int y = x - k;

                while (x < n && y < m && a[aStart + x] == b[bStart + y])
                {
                    x++; y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                    return trace;
            }
        }

        return trace;
    }
}
