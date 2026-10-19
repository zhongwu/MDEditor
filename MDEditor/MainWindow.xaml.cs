using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using MDEditor.Core.Interfaces;
using MDEditor.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MDEditor;

/// <summary>
/// The application window with dual-pane markdown editor and live preview.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly IMarkdownParser _markdownParser;

    private bool _isDraggingSplitter;
    private double _initialEditorWidth;
    private double _initialMouseX;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize ViewModel from DI container
        ViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _markdownParser = App.Services.GetRequiredService<IMarkdownParser>();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Initialize zoom combo
        StatusZoomCombo.SelectedIndex = 2; // 100%

        // Initialize preview with blank content
        InitializePreview();

        // Setup splitter drag handling
        SetupSplitter();
    }

    private async void InitializePreview()
    {
        try
        {
            await MarkdownPreview.EnsureCoreWebView2Async();
            var html = GetEmptyPreviewHtml();
            MarkdownPreview.NavigateToString(html);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
        }
    }

    private string GetEmptyPreviewHtml()
    {
        return """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                        line-height: 1.6;
                        padding: 20px;
                        max-width: 900px;
                        margin: 0 auto;
                        color: #24292f;
                    }
                    .placeholder {
                        text-align: center;
                        color: #656d76;
                        padding: 60px 20px;
                    }
                    .placeholder h3 { margin-bottom: 8px; }
                    .placeholder kbd {
                        background: #f6f8fa;
                        border: 1px solid #d0d7de;
                        border-radius: 4px;
                        padding: 2px 6px;
                        font-family: monospace;
                    }
                </style>
            </head>
            <body>
                <div class="placeholder">
                    <h3>Live Preview</h3>
                    <p>Start typing in the editor to see the preview update in real-time.</p>
                    <p><kbd>Ctrl+Shift+P</kbd> to toggle preview</p>
                </div>
            </body>
            </html>
            """;
    }

    private void SetupSplitter()
    {
        SplitterColumn.PointerPressed += SplitterColumn_PointerPressed;
        SplitterColumn.PointerMoved += SplitterColumn_PointerMoved;
        SplitterColumn.PointerReleased += SplitterColumn_PointerReleased;
        SplitterColumn.PointerExited += SplitterColumn_PointerReleased;
        SplitterColumn.PointerEntered += SplitterColumn_PointerEntered;
    }

    private void SplitterColumn_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var coreWindow = CoreWindow.GetForCurrentThread();
        if (coreWindow != null)
        {
            coreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeWestEast, 0);
        }
    }

    private void MarkdownEditor_TextChanged(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private async void UpdatePreview()
    {
        if (MarkdownPreview.CoreWebView2 == null)
        {
            await MarkdownPreview.EnsureCoreWebView2Async();
        }

        MarkdownEditor.Document.GetText(TextGetOptions.FormatRtf, out string markdown);
        var html = _markdownParser.ToHtmlDocument(markdown);
        MarkdownPreview.NavigateToString(html);
    }

    // Splitter drag handling
    private void SplitterColumn_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSplitter = true;
        _initialEditorWidth = EditorColumn.ActualWidth;
        _initialMouseX = e.GetCurrentPoint(SplitterColumn).Position.X;
        SplitterColumn.CapturePointer(e.Pointer);
    }

    private void SplitterColumn_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter) return;

        var currentX = e.GetCurrentPoint(SplitterColumn).Position.X;
        var delta = currentX - _initialMouseX;
        var newWidth = _initialEditorWidth + delta;

        newWidth = Math.Clamp(newWidth, 300, MainContentGrid.ActualWidth - 300 - SplitterColumn.ActualWidth);
        EditorColumn.Width = new GridLength(newWidth);
    }

    private void SplitterColumn_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter) return;

        _isDraggingSplitter = false;
        SplitterColumn.ReleasePointerCapture(e.Pointer);
    }

    private void MarkdownPreview_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
    }

    private async void StatusZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusZoomCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && double.TryParse(tag, out double zoom))
        {
            if (MarkdownPreview.CoreWebView2 != null)
            {
                await MarkdownPreview.CoreWebView2.ExecuteScriptAsync($"document.body.style.zoom = '{zoom}';");
            }
        }
    }
}