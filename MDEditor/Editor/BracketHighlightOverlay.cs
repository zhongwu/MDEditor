using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MDEditor.Editor;

/// <summary>
/// Transparent layer that paints the bracket pair at the caret. It sits on top
/// of the editor in z-order (an adorner layer would render behind the text) and
/// never touches the document, so it cannot dirty the file or the undo stack.
/// </summary>
public sealed class BracketHighlightOverlay : FrameworkElement
{
    private static readonly Brush Fill = CreateFrozen(new SolidColorBrush(Color.FromArgb(80, 0, 120, 215)))!;
    private static readonly Pen Outline = CreateFrozen(new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 100, 190)), 1.5))!;

    private RichTextBox? _editor;
    private TextPointer? _open;
    private TextPointer? _close;

    public BracketHighlightOverlay()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The editor whose characters are highlighted.</summary>
    public RichTextBox? Editor
    {
        get => _editor;
        set
        {
            _editor = value;
            InvalidateVisual();
        }
    }

    /// <summary>Sets the bracket pair to highlight, or clears it with nulls.</summary>
    public void SetPair(TextPointer? open, TextPointer? close)
    {
        _open = open;
        _close = close;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_editor == null || _open == null || _close == null)
            return;

        DrawBracket(drawingContext, _open);
        DrawBracket(drawingContext, _close);
    }

    private void DrawBracket(DrawingContext drawingContext, TextPointer pointer)
    {
        var bounds = GetCharacterBounds(pointer);
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // GetCharacterBounds is relative to the editor; this element sits in the
        // same Grid cell but may be offset, so translate into local coordinates.
        var topLeft = _editor!.TranslatePoint(bounds.TopLeft, this);
        var rect = new Rect(topLeft, bounds.Size);
        rect.Inflate(1, 1);

        drawingContext.DrawRoundedRectangle(Fill, Outline, rect, 2, 2);
    }

    /// <summary>
    /// Returns the bounds of the character at <paramref name="pointer"/> in the
    /// editor's coordinate space. <see cref="TextPointer.GetCharacterRect"/>
    /// reports only an insertion point for a single character (zero width), so
    /// the width is measured from that character to the following one.
    /// </summary>
    private static Rect GetCharacterBounds(TextPointer pointer)
    {
        var start = pointer.GetCharacterRect(LogicalDirection.Forward);
        if (start.IsEmpty)
            return Rect.Empty;

        var next = pointer.GetPositionAtOffset(1, LogicalDirection.Forward);
        if (next == null)
            return start;

        var end = next.GetCharacterRect(LogicalDirection.Backward);
        double left = Math.Min(start.Left, end.Left);
        double right = Math.Max(start.Left, end.Left);
        double width = right - left;

        // Fall back to the caret width when the font reports an insertion point.
        if (width <= 0)
            width = Math.Max(1, start.Width);

        return new Rect(left, start.Top, width, start.Height);
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
