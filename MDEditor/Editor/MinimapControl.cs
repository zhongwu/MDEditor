using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MDEditor.Editor;

/// <summary>
/// Compact overview of the whole document shown beside the editor. Renders the
/// document text at a tiny scale and overlays a viewport indicator; clicking or
/// dragging on it scrolls the editor to the corresponding location.
/// </summary>
public sealed class MinimapControl : Grid
{
    private readonly ScrollViewer _scrollViewer;
    private readonly TextBlock _textBlock;
    private readonly Canvas _viewportLayer;
    private readonly Rectangle _viewportRect;

    private RichTextBox? _editor;
    private ScrollViewer? _editorScroll;

    private bool _dragging;
    private bool _syncing;

    public MinimapControl()
    {
        Width = 100;
        Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));

        _textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 4,
            Foreground = new SolidColorBrush(Colors.Black),
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _textBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            IsHitTestVisible = false
        };

        _viewportLayer = new Canvas { IsHitTestVisible = false };
        _viewportRect = new Rectangle
        {
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb(220, 0, 120, 215)),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        _viewportLayer.Children.Add(_viewportRect);

        Children.Add(_scrollViewer);
        Children.Add(_viewportLayer);

        Cursor = Cursors.Hand;
        ClipToBounds = true;
    }

    public void Attach(RichTextBox editor)
    {
        _editor = editor;
        _editorScroll = FindScrollViewer(editor);

        if (_editorScroll != null)
        {
            _editorScroll.ScrollChanged += (_, _) => SyncFromEditor();
        }

        editor.TextChanged += (_, _) =>
        {
            RefreshText();
            SyncFromEditor();
        };
        editor.SizeChanged += (_, _) => SyncFromEditor();

        RefreshText();
        SyncFromEditor();
    }

    private void RefreshText()
    {
        if (_editor == null) return;
        try
        {
            _textBlock.Text = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd).Text;
        }
        catch
        {
            // ignore transient document states
        }
    }

    private void SyncFromEditor()
    {
        if (_editorScroll == null || _syncing) return;
        _syncing = true;
        try
        {
            double eExtent = _editorScroll.ExtentHeight;
            double eOffset = _editorScroll.VerticalOffset;
            double eViewport = _editorScroll.ViewportHeight;

            double mExtent = _scrollViewer.ExtentHeight;
            if (eExtent > 0 && mExtent > 0)
            {
                double ratio = eOffset / eExtent;
                _scrollViewer.ScrollToVerticalOffset(ratio * mExtent);
            }

            // Viewport rectangle
            double h = ActualHeight;
            if (h <= 0) h = _editorScroll.ViewportHeight;
            double vpTop = (eExtent > 0) ? (eOffset / eExtent) * h : 0;
            double vpHeight = (eExtent > 0) ? (eViewport / eExtent) * h : h;
            if (vpHeight < 4) vpHeight = 4;
            if (vpTop + vpHeight > h) vpTop = h - vpHeight;
            if (vpTop < 0) vpTop = 0;

            Canvas.SetLeft(_viewportRect, 0);
            Canvas.SetTop(_viewportRect, vpTop);
            _viewportRect.Width = ActualWidth;
            _viewportRect.Height = vpHeight;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        SyncFromEditor();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragging = true;
        CaptureMouse();
        ScrollEditorToMouse(e.GetPosition(this).Y);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            ScrollEditorToMouse(e.GetPosition(this).Y);
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void ScrollEditorToMouse(double y)
    {
        if (_editorScroll == null) return;
        double h = ActualHeight;
        if (h <= 0) return;
        double ratio = Math.Max(0, Math.Min(1, y / h));
        double offset = ratio * _editorScroll.ExtentHeight;
        offset = Math.Max(0, Math.Min(offset, _editorScroll.ExtentHeight - _editorScroll.ViewportHeight));
        _editorScroll.ScrollToVerticalOffset(offset);
    }
}
