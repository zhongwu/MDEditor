using MDEditor.Core.Services;

namespace MDEditor.Tests;

public class LineDiffTests
{
    private static string Render(IEnumerable<DiffLine> lines) => string.Join("|", lines.Select(l => l.Kind switch
    {
        DiffLineKind.Added => "+" + l.Text,
        DiffLineKind.Removed => "-" + l.Text,
        DiffLineKind.Skipped => $"~{l.SkippedCount}",
        _ => " " + l.Text,
    }));

    /// <summary>Applying the diff must reproduce both sides exactly.</summary>
    private static void AssertReconstructs(string oldText, string newText, List<DiffLine> diff)
    {
        static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
        Assert.Equal(Join(Split(oldText)), Join(diff.Where(l => l.Kind != DiffLineKind.Added).Select(l => l.Text)));
        Assert.Equal(Join(Split(newText)), Join(diff.Where(l => l.Kind != DiffLineKind.Removed).Select(l => l.Text)));

        // Line numbers are consecutive on each side.
        Assert.Equal(Enumerable.Range(1, Split(oldText).Length), diff.Where(l => l.OldLineNumber != null).Select(l => l.OldLineNumber!.Value));
        Assert.Equal(Enumerable.Range(1, Split(newText).Length), diff.Where(l => l.NewLineNumber != null).Select(l => l.NewLineNumber!.Value));
    }

    private static string[] Split(string text)
    {
        if (text.Length == 0) return Array.Empty<string>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return text.EndsWith('\n') ? lines[..^1] : lines;
    }

    [Fact]
    public void IdenticalTexts_AllUnchanged()
    {
        var diff = LineDiff.Compute("a\nb\nc\n", "a\nb\nc\n");
        Assert.Equal(" a| b| c", Render(diff));
    }

    [Fact]
    public void LineEndingsAreIgnored()
    {
        var diff = LineDiff.Compute("a\r\nb\r\n", "a\nb\n");
        Assert.All(diff, l => Assert.Equal(DiffLineKind.Unchanged, l.Kind));
    }

    [Fact]
    public void SingleLineChange_IsRemovePlusAdd()
    {
        var diff = LineDiff.Compute("a\nb\nc", "a\nB\nc");
        Assert.Equal(" a|-b|+B| c", Render(diff));
        Assert.Equal((1, 1), LineDiff.Stats(diff));
    }

    [Fact]
    public void Insertions_AndDeletions_InTheMiddle()
    {
        var diff = LineDiff.Compute("1\n2\n3\n4\n5", "1\n3\n4\nx\n5");
        Assert.Equal(" 1|-2| 3| 4|+x| 5", Render(diff));
    }

    [Fact]
    public void FromEmpty_AllAdded_ToEmpty_AllRemoved()
    {
        Assert.Equal("+a|+b", Render(LineDiff.Compute("", "a\nb")));
        Assert.Equal("-a|-b", Render(LineDiff.Compute("a\nb\n", "")));
        Assert.Empty(LineDiff.Compute("", ""));
    }

    [Fact]
    public void LineNumbers_TrackEachSide()
    {
        var diff = LineDiff.Compute("a\nb\nc", "a\nx\ny\nc");
        var c = diff.Last();
        Assert.Equal(3, c.OldLineNumber);
        Assert.Equal(4, c.NewLineNumber);
        Assert.Null(diff.Single(l => l.Text == "x").OldLineNumber);
        Assert.Null(diff.Single(l => l.Text == "b").NewLineNumber);
    }

    [Theory]
    [InlineData("a\nb\nc\nd\ne\nf", "b\nc\nX\ne\nf\ng")]
    [InlineData("x\ny\nx\ny\nx", "y\nx\ny\nx\ny")]
    [InlineData("# Title\n\nPara one.\n\nPara two.\n", "# New Title\n\nPara one.\n\nInserted.\n\nPara two.\nTail\n")]
    [InlineData("same\nsame\nsame", "same\nsame")]
    public void Diff_ReconstructsBothSides(string oldText, string newText)
    {
        AssertReconstructs(oldText, newText, LineDiff.Compute(oldText, newText));
    }

    [Fact]
    public void Diff_IsMinimal_ForInterleavedEdits()
    {
        // Myers finds the shortest edit script: here 2 removals + 2 additions, not a full rewrite.
        var diff = LineDiff.Compute("a\nb\nc\nd\ne\nf\ng", "a\nX\nc\nd\ne\nY\ng");
        Assert.Equal((2, 2), LineDiff.Stats(diff));
    }

    [Fact]
    public void RandomEdits_AlwaysReconstruct()
    {
        var random = new Random(12345);
        for (int round = 0; round < 200; round++)
        {
            var oldLines = Enumerable.Range(0, random.Next(0, 30)).Select(_ => ((char)('a' + random.Next(4))).ToString()).ToList();
            var newLines = oldLines.ToList();
            for (int edit = random.Next(0, 8); edit > 0; edit--)
            {
                int op = random.Next(3);
                if (op == 0 || newLines.Count == 0)
                    newLines.Insert(random.Next(newLines.Count + 1), ((char)('a' + random.Next(4))).ToString());
                else if (op == 1)
                    newLines.RemoveAt(random.Next(newLines.Count));
                else
                    newLines[random.Next(newLines.Count)] = "z";
            }

            var oldText = string.Join("\n", oldLines);
            var newText = string.Join("\n", newLines);
            AssertReconstructs(oldText, newText, LineDiff.Compute(oldText, newText));
        }
    }

    [Fact]
    public void LargeRewrite_BeyondEditLimit_FallsBackToFullReplace()
    {
        var oldText = string.Join("\n", Enumerable.Range(0, 2500).Select(i => "old" + i));
        var newText = string.Join("\n", Enumerable.Range(0, 2500).Select(i => "new" + i));

        var diff = LineDiff.Compute(oldText, newText);

        Assert.Equal((2500, 2500), LineDiff.Stats(diff));
        AssertReconstructs(oldText, newText, diff);
    }

    [Fact]
    public void Collapse_HidesDistantUnchangedLines_KeepingContext()
    {
        var oldLines = Enumerable.Range(1, 20).Select(i => i.ToString()).ToList();
        var newLines = oldLines.ToList();
        newLines[9] = "TEN"; // line 10 changed

        var diff = LineDiff.Compute(string.Join("\n", oldLines), string.Join("\n", newLines));
        var collapsed = LineDiff.Collapse(diff, context: 2);

        Assert.Equal("~7| 8| 9|-10|+TEN| 11| 12|~8", Render(collapsed));
        Assert.Equal(1, collapsed[0].OldLineNumber); // marker points at the first hidden line
        Assert.Equal(13, collapsed.Last().OldLineNumber);
    }

    [Fact]
    public void Collapse_KeepsShortGaps_BetweenChanges()
    {
        var diff = LineDiff.Compute("a\n1\n2\n3\nb", "A\n1\n2\n3\nB");
        // Gap of 3 unchanged lines with context 1 would hide only 1 line - not worth a marker.
        Assert.Equal("-a|+A| 1| 2| 3|-b|+B", Render(LineDiff.Collapse(diff, context: 1)));
    }
}
