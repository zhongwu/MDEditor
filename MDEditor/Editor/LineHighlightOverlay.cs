using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MDEditor.Editor;

/// <summary>
/// Transparent layer that paints a full-width band behind one editor line
/// (a paragraph, including its wrapped rows), used by Go to Line to show
/// where the caret lands. Like the other overlays it never touches the document.
/// </summary>
public sealed class LineHighlightOverlay : FrameworkElement
{
    private static readonly Brush BandFill = CreateFrozen(new SolidColorBrush(Color.FromArgb(55, 0, 120, 215)))!;
    private static readonly Brush AccentFill = CreateFrozen(new SolidColorBrush(Color.FromRgb(0, 120, 215)))!;
    private const double AccentWidth = 3;

    private RichTextBox? _editor;
    private FrameworkElement? _viewport;
    private Paragraph? _line;

    public LineHighlightOverlay()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The editor whose line is highlighted.</summary>
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

    /// <summary>Highlights the given line (or clears with null), cancelling any fade in progress.</summary>
    public void SetLine(Paragraph? line)
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        _line = line;
        InvalidateVisual();
    }

    /// <summary>Keeps the current highlight for <paramref name="hold"/>, then fades it out and clears it.</summary>
    public void FadeOut(TimeSpan hold)
    {
        if (_line == null)
            return;

        var line = _line;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(700)) { BeginTime = hold };
        fade.Completed += (s, e) =>
        {
            // A newer SetLine call owns the overlay now; leave it alone.
            if (_line == line)
                SetLine(null);
        };
        BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_editor == null || _line == null || _line.Parent == null)
            return;

        var clip = GetViewportBounds();
        if (clip.IsEmpty)
            return;

        var first = _line.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        var last = _line.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
        if (first.IsEmpty || last.IsEmpty)
            return;

        // Character rects are relative to the editor; translate into local coordinates.
        double top = _editor.TranslatePoint(new Point(0, first.Top), this).Y;
        double bottom = _editor.TranslatePoint(new Point(0, Math.Max(first.Bottom, last.Bottom)), this).Y;
        if (bottom <= clip.Top || top >= clip.Bottom)
            return;

        drawingContext.PushClip(new RectangleGeometry(clip));
        drawingContext.DrawRectangle(BandFill, null, new Rect(clip.Left, top, clip.Width, bottom - top));
        drawingContext.DrawRectangle(AccentFill, null, new Rect(clip.Left, top, AccentWidth, bottom - top));
        drawingContext.Pop();
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
}
