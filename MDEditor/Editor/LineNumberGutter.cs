using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MDEditor.Editor;

/// <summary>
/// Line-number gutter for the editor. Each number is drawn at the measured
/// position of its paragraph, so it stays aligned with wrapped lines, resizes
/// and taller glyphs; only the visible lines are drawn. The caret's line is
/// emphasised.
/// </summary>
public sealed class LineNumberGutter : FrameworkElement
{
    private const double SidePadding = 8;

    private static readonly Typeface NumberTypeface = new(new FontFamily("Cascadia Code, Consolas, monospace"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface CurrentTypeface = new(new FontFamily("Cascadia Code, Consolas, monospace"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush NumberBrush = CreateFrozen(new SolidColorBrush(Color.FromRgb(140, 140, 140)))!;
    private static readonly Brush CurrentBrush = CreateFrozen(new SolidColorBrush(Color.FromRgb(40, 40, 40)))!;

    private RichTextBox? _editor;
    private int _digits = 2;

    public LineNumberGutter()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
    }

    public double NumberFontSize { get; set; } = 14;

    /// <summary>Connects the gutter to the editor and repaints whenever the text, caret or layout changes.</summary>
    public void Attach(RichTextBox editor)
    {
        _editor = editor;
        editor.TextChanged += (s, e) => Refresh();
        editor.SelectionChanged += (s, e) => InvalidateVisual();
        editor.SizeChanged += (s, e) => InvalidateVisual();
        Refresh();
    }

    /// <summary>Repaints, and resizes when the number of digits needed has changed.</summary>
    public void Refresh()
    {
        if (_editor == null || Visibility != Visibility.Visible)
            return;

        int digits = Math.Max(2, _editor.Document.Blocks.Count.ToString(CultureInfo.InvariantCulture).Length);
        if (digits != _digits)
        {
            _digits = digits;
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sample = CreateText(new string('0', _digits), NumberTypeface, NumberBrush);
        return new Size(Math.Ceiling(sample.Width) + SidePadding * 2, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (_editor == null || ActualHeight <= 0)
            return;

        // Transparent fill so the whole strip renders (and clips) consistently.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        var firstVisible = _editor.GetPositionFromPoint(new Point(0, 0), true)?.Paragraph;
        var caretLine = _editor.CaretPosition.Paragraph;

        // Line numbers are paragraph positions; count up to the first visible one.
        int number = 1;
        Block? block = _editor.Document.Blocks.FirstBlock;
        if (firstVisible != null)
        {
            while (block != null && block != firstVisible)
            {
                number++;
                block = block.NextBlock;
            }

            if (block == null)
            {
                block = _editor.Document.Blocks.FirstBlock;
                number = 1;
            }
        }

        for (; block != null; block = block.NextBlock, number++)
        {
            if (block is not Paragraph paragraph)
                continue;

            var rect = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            if (rect.IsEmpty)
                continue;

            double top = _editor.TranslatePoint(new Point(0, rect.Top), this).Y;
            if (top > ActualHeight)
                break;
            if (top + rect.Height < 0)
                continue;

            bool current = paragraph == caretLine;
            var text = CreateText(number.ToString(CultureInfo.InvariantCulture),
                current ? CurrentTypeface : NumberTypeface,
                current ? CurrentBrush : NumberBrush);

            double x = ActualWidth - SidePadding - text.Width;
            double y = top + (rect.Height - text.Height) / 2;
            drawingContext.DrawText(text, new Point(x, y));
        }
    }

    private FormattedText CreateText(string value, Typeface typeface, Brush brush) =>
        new(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, NumberFontSize, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Brush? CreateFrozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
