using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using MDEditor.Core.Interfaces;
using MDEditor.ViewModels;

namespace MDEditor;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly IMarkdownParser _markdownParser;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize ViewModel from DI container
        ViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _markdownParser = App.Services.GetRequiredService<IMarkdownParser>();

        // Wire up markdown insertion callback
        ViewModel.InsertMarkdownCallback = InsertMarkdown;

        DataContext = ViewModel;

        // Initialize zoom combo
        StatusZoomCombo.SelectedIndex = 2; // 100%

        // Initialize preview with blank content
        InitializePreview();
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
                </div>
            </body>
            </html>
            """;
    }

    private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
        UpdateLineNumbers();
    }

    private void UpdateLineNumbers()
    {
        var text = GetEditorText();
        var lineCount = text.Split('\n').Length;
        var lineNumbers = string.Join("\n", Enumerable.Range(1, lineCount));
        LineNumbers.Text = lineNumbers;
    }

    private async void UpdatePreview()
    {
        if (MarkdownPreview.CoreWebView2 == null)
        {
            await MarkdownPreview.EnsureCoreWebView2Async();
        }

        var markdown = GetEditorText();
        var html = _markdownParser.ToHtmlDocument(markdown);
        MarkdownPreview.NavigateToString(html);
    }

    private string GetEditorText()
    {
        var range = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        return range.Text;
    }

    private void InsertMarkdown(string before, string after)
    {
        var range = MarkdownEditor.Selection;
        var selectedText = range.Text;

        range.Text = before + selectedText + after;

        // Move cursor after inserted text
        var newOffset = range.Start.GetOffsetToPosition(range.End) + before.Length + selectedText.Length + after.Length;
        MarkdownEditor.CaretPosition = range.Start.GetPositionAtOffset(before.Length + selectedText.Length);
    }

    private void MarkdownPreview_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
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