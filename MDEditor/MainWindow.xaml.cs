using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MDEditor.Core.Interfaces;
using MDEditor.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MDEditor;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private readonly IMarkdownParser _markdownParser;

    private bool _isDraggingSplitter;
    private double _initialEditorWidth;
    private double _initialMouseX;

    // Single-instance dialog tracking
    private FindReplaceDialog? _findDialog;
    private FindReplaceDialog? _replaceDialog;
    private GoToLineDialog? _goToLineDialog;

    // Line numbers state
    private bool _lineNumbersVisible = false;

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _markdownParser = App.Services.GetRequiredService<IMarkdownParser>();

        ViewModel.InsertMarkdownCallback = InsertMarkdown;
        ViewModel.UndoCallback = () => MarkdownEditor.Undo();
        ViewModel.RedoCallback = () => MarkdownEditor.Redo();
        ViewModel.CutCallback = () => MarkdownEditor.Cut();
        ViewModel.CopyCallback = () => MarkdownEditor.Copy();
        ViewModel.PasteCallback = () => MarkdownEditor.Paste();
        ViewModel.SelectAllCallback = () => MarkdownEditor.SelectAll();
        ViewModel.FindCallback = ShowFindDialog;
        ViewModel.ReplaceCallback = ShowReplaceDialog;
        ViewModel.GoToLineCallback = ShowGoToLineDialog;
        ViewModel.ToggleWordWrapCallback = ToggleWordWrap;

        // Line numbers toggle
        ViewModel.ToggleLineNumbersCallback = ToggleLineNumbers;

        DataContext = ViewModel;

        StatusZoomCombo.SelectedIndex = 2;

        InitializePreview();
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
        SplitterColumn.PreviewMouseLeftButtonDown += SplitterColumn_MouseLeftButtonDown;
        SplitterColumn.PreviewMouseMove += SplitterColumn_MouseMove;
        SplitterColumn.PreviewMouseLeftButtonUp += SplitterColumn_MouseLeftButtonUp;
    }

    private void SplitterColumn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSplitter = true;
        _initialEditorWidth = EditorColumn.ActualWidth;
        _initialMouseX = e.GetPosition(SplitterColumn).X;
        SplitterColumn.CaptureMouse();
    }

    private void SplitterColumn_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSplitter) return;

        var currentX = e.GetPosition(SplitterColumn).X;
        var delta = currentX - _initialMouseX;
        var newWidth = _initialEditorWidth + delta;

        newWidth = Math.Clamp(newWidth, 300, MainContentGrid.ActualWidth - 300 - SplitterColumn.ActualWidth);
        EditorColumn.Width = new GridLength(newWidth);
    }

    private void SplitterColumn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingSplitter) return;

        _isDraggingSplitter = false;
        SplitterColumn.ReleaseMouseCapture();
    }

    private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
        UpdateLineNumbers();
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
        var range = new System.Windows.Documents.TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        return range.Text;
    }

    private void InsertMarkdown(string before, string after)
    {
        try
        {
            var selection = MarkdownEditor.Selection;
            var selectedText = selection.Text;
            selection.Text = before + selectedText + after;
            System.Diagnostics.Debug.WriteLine($"InsertMarkdown: inserted '{before}{selectedText}{after}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InsertMarkdown error: {ex.Message}");
        }
    }

    private void ToggleWordWrap()
    {
        // WPF RichTextBox doesn't support TextWrapping directly
        // Toggle horizontal scrollbar visibility instead
        if (MarkdownEditor.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto)
        {
            MarkdownEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        else
        {
            MarkdownEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    private void ToggleLineNumbers()
    {
        _lineNumbersVisible = !_lineNumbersVisible;
        LineNumbers.Visibility = _lineNumbersVisible ? Visibility.Visible : Visibility.Collapsed;
        if (_lineNumbersVisible)
        {
            UpdateLineNumbers();
        }
    }

    private void UpdateLineNumbers()
    {
        if (!_lineNumbersVisible) return;

        var text = GetEditorText();
        var lines = text.Split('\n');
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= lines.Length; i++)
        {
            sb.AppendLine(i.ToString());
        }
        LineNumbers.Text = sb.ToString().TrimEnd('\r', '\n');
    }

    private void ShowFindDialog()
    {
        if (_findDialog != null && _findDialog.IsVisible)
        {
            _findDialog.Activate();
            _findDialog.Focus();
            return;
        }

        _findDialog = new FindReplaceDialog(MarkdownEditor, false);
        _findDialog.Owner = this;
        _findDialog.WindowStartupLocation = WindowStartupLocation.Manual;
        _findDialog.Left = this.Left + this.Width - 420;
        _findDialog.Top = this.Top + 80;
        _findDialog.Closed += (s, e) => _findDialog = null;
        _findDialog.Show();
    }

    private void ShowReplaceDialog()
    {
        if (_replaceDialog != null && _replaceDialog.IsVisible)
        {
            _replaceDialog.Activate();
            _replaceDialog.Focus();
            return;
        }

        _replaceDialog = new FindReplaceDialog(MarkdownEditor, true);
        _replaceDialog.Owner = this;
        _replaceDialog.WindowStartupLocation = WindowStartupLocation.Manual;
        _replaceDialog.Left = this.Left + this.Width - 420;
        _replaceDialog.Top = this.Top + 80;
        _replaceDialog.Closed += (s, e) => _replaceDialog = null;
        _replaceDialog.Show();
    }

    private void ShowGoToLineDialog()
    {
        if (_goToLineDialog != null && _goToLineDialog.IsVisible)
        {
            _goToLineDialog.Activate();
            _goToLineDialog.Focus();
            return;
        }

        _goToLineDialog = new GoToLineDialog(MarkdownEditor);
        _goToLineDialog.Owner = this;
        _goToLineDialog.Closed += (s, e) => _goToLineDialog = null;
        _goToLineDialog.Show();
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
