using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MDEditor.Editor;

/// <summary>
/// Transparent layer that paints every find match in the editor, with the
/// current match emphasised (same colours as find in preview). Like
/// <see cref="BracketHighlightOverlay"/> it never touches the document, so
/// highlighting cannot dirty the file or the undo stack.
/// </summary>
public sealed class FindHighlightOverlay : FrameworkElement
{
    private static readonly Brush MatchFill = CreateFrozen(new SolidColorBrush(Color.FromArgb(120, 255, 230, 80)))!;
    private static readonly Brush CurrentFill = CreateFrozen(new SolidColorBrush(Color.FromArgb(130, 255, 150, 50)))!;
    private static readonly Pen CurrentOutline = CreateFrozen(new Pen(new SolidColorBrush(Color.FromRgb(232, 117, 26)), 1))!;

    private RichTextBox? _editor;
    private FrameworkElement? _viewport;
    private IReadOnlyList<(TextPointer Start, TextPointer End)> _matches = Array.Empty<(TextPointer, TextPointer)>();
    private int _current = -1;

    public FindHighlightOverlay()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The editor whose matches are highlighted.</summary>
    public RichTextBox? Editor
    {
        get => _editor;
        set
        {
            _editor = value;
            _viewport = null;
            InvalidateVisual();
        }
    }

    /// <summary>Sets the matches (in document order) and the index of the current one.</summary>
    public void SetMatches(IReadOnlyList<(TextPointer Start, TextPointer End)> matches, int current)
    {
        _matches = matches;
        _current = current;
        InvalidateVisual();
    }

    public void Clear() => SetMatches(Array.Empty<(TextPointer, TextPointer)>(), -1);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_editor == null || _matches.Count == 0)
            return;

        // Clip to the text viewport so highlights never spill over the scrollbars.
        var clip = GetViewportBounds();
        if (clip.IsEmpty)
            return;

        // Only the visible slice of the matches needs measuring.
        var firstVisible = _editor.GetPositionFromPoint(new Point(0, 0), true);
        var lastVisible = _editor.GetPositionFromPoint(new Point(_editor.ActualWidth, _editor.ActualHeight), true);
        if (firstVisible == null || lastVisible == null)
            return;

        drawingContext.PushClip(new RectangleGeometry(clip));

        for (int i = FirstMatchEndingAfter(firstVisible); i < _matches.Count; i++)
        {
            var (start, end) = _matches[i];
            if (start.CompareTo(lastVisible) > 0)
                break;

            DrawMatch(drawingContext, start, end, clip, i == _current);
        }

        drawingContext.Pop();
    }

    private int FirstMatchEndingAfter(TextPointer position)
    {
        int lo = 0, hi = _matches.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_matches[mid].End.CompareTo(position) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private void DrawMatch(DrawingContext drawingContext, TextPointer start, TextPointer end, Rect clip, bool current)
    {
        var startRect = start.GetCharacterRect(LogicalDirection.Forward);
        var endRect = end.GetCharacterRect(LogicalDirection.Backward);
        if (startRect.IsEmpty || endRect.IsEmpty)
            return;

        var fill = current ? CurrentFill : MatchFill;
        var pen = current ? CurrentOutline : null;

        if (Math.Abs(startRect.Top - endRect.Top) < 1)
        {
            DrawRect(drawingContext, fill, pen, new Rect(startRect.Left, startRect.Top, Math.Max(1, endRect.Left - startRect.Left), startRect.Height));
            return;
        }

        // The match wraps onto the next line: paint the tail of the first line and the head of the last.
        var clipInEditor = TranslatePoint(clip.TopLeft, _editor!);
        double right = clipInEditor.X + clip.Width;
        double left = clipInEditor.X;
        DrawRect(drawingContext, fill, pen, new Rect(startRect.Left, startRect.Top, Math.Max(1, right - startRect.Left), startRect.Height));
        DrawRect(drawingContext, fill, pen, new Rect(left, endRect.Top, Math.Max(1, endRect.Left - left), endRect.Height));
    }

    private void DrawRect(DrawingContext drawingContext, Brush fill, Pen? pen, Rect editorRect)
    {
        // Character rects are relative to the editor; translate into local coordinates.
        var topLeft = _editor!.TranslatePoint(editorRect.TopLeft, this);
        drawingContext.DrawRoundedRectangle(fill, pen, new Rect(topLeft, editorRect.Size), 2, 2);
    }

    private Rect GetViewportBounds()
    {
        if (_viewport == null && _editor != null)
        {
            var scrollViewer = FindDescendant<ScrollViewer>(_editor);
            _viewport = scrollViewer?.Template?.FindName("PART_ScrollContentPresenter", scrollViewer) as FrameworkElement ?? _editor;
        }

        if (_viewport == null || !_viewport.IsVisible)
            return Rect.Empty;

        var topLeft = _viewport.TranslatePoint(new Point(0, 0), this);
        return new Rect(topLeft, _viewport.RenderSize);
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static Brush? CreateFrozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Pen? CreateFrozen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
