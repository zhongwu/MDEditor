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
using System.Windows.Documents;

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
    private System.Windows.Threading.DispatcherTimer? _highlightTimer;
    private System.Windows.Threading.DispatcherTimer? _previewTimer;
    private System.Windows.Threading.DispatcherTimer? _lineNumbersTimer;
    private string _lastRenderedMarkdown = string.Empty;

    // File management state
    private string? _currentFilePath = null;
    private bool _isDirty = false;

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
        ViewModel.NewFileCallback = NewFile;
        ViewModel.OpenFileCallback = OpenFile;
        ViewModel.SaveFileCallback = SaveFile;
        ViewModel.SaveAsFileCallback = SaveAsFile;
        ViewModel.ExportHtmlCallback = ExportHtml;

        // Line numbers toggle
        ViewModel.ToggleLineNumbersCallback = ToggleLineNumbers;

        DataContext = ViewModel;

        StatusZoomCombo.SelectedIndex = 2;

        InitializePreview();
        SetupSplitter();

        // Set paragraph style for consistent line height
        var style = new Style(typeof(Paragraph));
        style.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Paragraph.LineHeightProperty, 18.0));
        MarkdownEditor.Resources.Add(typeof(Paragraph), style);

        // Setup syntax highlighting timer (debounced - longer to preserve undo)
        _highlightTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(2000)
        };
        _highlightTimer.Tick += (s, e) =>
        {
            _highlightTimer.Stop();
            ApplySyntaxHighlighting();
        };

        // Setup preview refresh timer (debounced 1000ms for better performance)
        _previewTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };
        _previewTimer.Tick += (s, e) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };

        // Handle Tab/Shift+Tab for indentation
        MarkdownEditor.PreviewKeyDown += MarkdownEditor_PreviewKeyDown;

        // Handle editor mouse click to scroll preview to corresponding position
        MarkdownEditor.PreviewMouseLeftButtonUp += (s, e) =>
        {
            // Delay to ensure caret position is updated after click
            Dispatcher.BeginInvoke(new Action(() => SyncPreviewToCaret()), System.Windows.Threading.DispatcherPriority.Input);
        };

        // Setup line numbers update timer
        _lineNumbersTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _lineNumbersTimer.Tick += (s, e) =>
        {
            _lineNumbersTimer.Stop();
            UpdateLineNumbers();
        };
    }

    private void SyncPreviewToCaret()
    {
        if (MarkdownPreview.CoreWebView2 == null) return;

        try
        {
            // Use caret's pixel position for more accurate sync
            var caretRect = MarkdownEditor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            var scrollViewer = FindVisualChild<ScrollViewer>(MarkdownEditor);
            if (scrollViewer == null) return;

            // Calculate the absolute Y position of the caret in the document
            var caretY = caretRect.Top + scrollViewer.VerticalOffset;
            var totalHeight = scrollViewer.ExtentHeight;
            if (totalHeight <= 0) return;

            var scrollPercent = caretY / totalHeight;
            // Clamp to 0-1
            scrollPercent = Math.Max(0, Math.Min(1, scrollPercent));

            // Scroll the preview to the corresponding position
            var script = $@"
                (function() {{
                    var maxScroll = document.body.scrollHeight - window.innerHeight;
                    if (maxScroll < 0) maxScroll = 0;
                    window.scrollTo(0, maxScroll * {scrollPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)});
                }})();
            ";
            MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncPreviewToCaret error: {ex.Message}");
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    private void MarkdownEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab)
            return;

        e.Handled = true;
        const string indent = "    ";

        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (isShift)
        {
            if (MarkdownEditor.Selection.IsEmpty)
            {
                var caret = MarkdownEditor.CaretPosition;
                var lineStart = caret.GetLineStartPosition(0);
                if (lineStart != null)
                {
                    var lineEnd = caret.GetLineStartPosition(1) ?? MarkdownEditor.Document.ContentEnd;
                    var lineRange = new TextRange(lineStart, lineEnd);
                    var lineText = lineRange.Text;
                    if (lineText.StartsWith(indent))
                    {
                        lineRange.Text = lineText.Substring(indent.Length);
                    }
                }
            }
            else
            {
                var selectedText = MarkdownEditor.Selection.Text;
                var lines = selectedText.Split('\n');
                var unindented = string.Join("\n", Array.ConvertAll(lines, l =>
                    l.StartsWith(indent) ? l.Substring(indent.Length) : l));
                MarkdownEditor.Selection.Text = unindented;
            }
        }
        else
        {
            if (MarkdownEditor.Selection.IsEmpty)
            {
                MarkdownEditor.Selection.Text = indent;
                var newPos = MarkdownEditor.CaretPosition.GetPositionAtOffset(indent.Length, LogicalDirection.Forward);
                if (newPos != null)
                    MarkdownEditor.CaretPosition = newPos;
            }
            else
            {
                var selectedText = MarkdownEditor.Selection.Text;
                var lines = selectedText.Split('\n');
                var indented = string.Join("\n", Array.ConvertAll(lines, l => indent + l));
                MarkdownEditor.Selection.Text = indented;
            }
        }
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
        // Mark as dirty
        MarkDirty();

        // Debounce preview refresh (1000ms)
        _previewTimer?.Stop();
        _previewTimer?.Start();

        // Debounce syntax highlighting
        _highlightTimer?.Stop();
        _highlightTimer?.Start();

        // Line numbers updated via separate timer to avoid layout fighting with scroll
        _lineNumbersTimer?.Stop();
        _lineNumbersTimer?.Start();
    }

    private async void UpdatePreview()
    {
        if (MarkdownPreview.CoreWebView2 == null)
        {
            await MarkdownPreview.EnsureCoreWebView2Async();
            // Initial full page load
            var initMarkdown = GetEditorText();
            var initHtml = await Task.Run(() => _markdownParser.ToHtmlDocument(initMarkdown));
            MarkdownPreview.NavigateToString(initHtml);
            _lastRenderedMarkdown = initMarkdown;
            return;
        }

        var markdown = GetEditorText();

        // Skip if content hasn't changed
        if (markdown == _lastRenderedMarkdown)
            return;

        _lastRenderedMarkdown = markdown;

        // Run Markdig parsing on background thread to avoid UI freeze
        var bodyHtml = await Task.Run(() => _markdownParser.ToHtml(markdown));
        if (string.IsNullOrEmpty(bodyHtml))
            bodyHtml = "<p></p>";

        // Encode as base64 to avoid JavaScript string escaping issues
        var base64Html = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(bodyHtml));

        var script = $@"
            (async function() {{
                var body = document.body;
                // Decode base64 to UTF-8 properly
                var binaryString = atob('{base64Html}');
                var bytes = new Uint8Array(binaryString.length);
                for (var i = 0; i < binaryString.length; i++) {{
                    bytes[i] = binaryString.charCodeAt(i);
                }}
                var decoded = new TextDecoder('utf-8').decode(bytes);
                body.innerHTML = decoded;
                if (typeof renderMathInElement !== 'undefined') {{
                    renderMathInElement(body, {{
                        delimiters: [
                            {{left: '$$', right: '$$', display: true}},
                            {{left: '$', right: '$', display: false}},
                            {{left: '\\\\(', right: '\\\\)', display: false}},
                            {{left: '\\\\[', right: '\\\\]', display: true}}
                        ]
                    }});
                }}
                if (typeof mermaid !== 'undefined') {{
                    try {{ await mermaid.run(); }} catch(e) {{ console.error(e); }}
                }}
            }})();
        ";

        await MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);
    }

    private string GetEditorText()
    {
        var range = new System.Windows.Documents.TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        return range.Text;
    }

    private void CopyAsMarkdown_Click(object sender, RoutedEventArgs e)
    {
        var text = GetEditorText();
        Clipboard.SetText(text);
    }

    private void CopyAsHtml_Click(object sender, RoutedEventArgs e)
    {
        var markdown = GetEditorText();
        var html = _markdownParser.ToHtmlDocument(markdown);
        Clipboard.SetText(html);
    }

    private void UpdateTitleBar()
    {
        var fileName = string.IsNullOrEmpty(_currentFilePath)
            ? "Untitled.md"
            : System.IO.Path.GetFileName(_currentFilePath);
        var dirty = _isDirty ? " ●" : "";
        Title = $"MDEditor - {fileName}{dirty}";
        EditorFileName.Text = fileName;
        EditorDirtyIndicator.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetEditorText(string text)
    {
        var range = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        range.Text = text;
        _isDirty = false;
        UpdateTitleBar();
    }

    private void MarkDirty()
    {
        if (!_isDirty)
        {
            _isDirty = true;
            UpdateTitleBar();
        }
    }

    private void NewFile()
    {
        if (_isDirty)
        {
            var result = MessageBox.Show("You have unsaved changes. Discard them?", "New File", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        _currentFilePath = null;
        SetEditorText("");
    }

    private void OpenFile()
    {
        if (_isDirty)
        {
            var result = MessageBox.Show("You have unsaved changes. Discard them?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Markdown File",
            Filter = "Markdown Files (*.md;*.markdown)|*.md;*.markdown|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var content = System.IO.File.ReadAllText(dialog.FileName);
                _currentFilePath = dialog.FileName;
                SetEditorText(content);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAsFile();
            return;
        }

        try
        {
            var text = GetEditorText();
            System.IO.File.WriteAllText(_currentFilePath, text);
            _isDirty = false;
            UpdateTitleBar();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsFile()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Markdown File As",
            Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".md",
            FileName = string.IsNullOrEmpty(_currentFilePath)
                ? "Untitled.md"
                : System.IO.Path.GetFileName(_currentFilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var text = GetEditorText();
                System.IO.File.WriteAllText(dialog.FileName, text);
                _currentFilePath = dialog.FileName;
                _isDirty = false;
                UpdateTitleBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportHtml()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export as HTML",
            Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
            DefaultExt = ".html",
            FileName = string.IsNullOrEmpty(_currentFilePath)
                ? "Untitled.html"
                : System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(_currentFilePath), ".html")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var markdown = GetEditorText();
                var html = _markdownParser.ToHtmlDocument(markdown);
                System.IO.File.WriteAllText(dialog.FileName, html);
                MessageBox.Show($"Exported to {dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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

        var paragraphs = new System.Collections.Generic.List<Paragraph>();
        foreach (Block block in MarkdownEditor.Document.Blocks)
        {
            if (block is Paragraph p)
                paragraphs.Add(p);
        }

        int lineCount = paragraphs.Count;
        if (lineCount == 0)
            lineCount = 1;

        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            if (i > 1)
                sb.Append('\n');
            sb.Append(i.ToString());
        }
        LineNumbers.Text = sb.ToString();
    }

    private void ApplySyntaxHighlighting()
    {
        var text = GetEditorText();

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Save caret position and selection
        var caretPos = MarkdownEditor.CaretPosition;
        var selectionStart = MarkdownEditor.Selection.Start;
        var selectionEnd = MarkdownEditor.Selection.End;
        var hasSelection = !MarkdownEditor.Selection.IsEmpty;

        // Batch document changes to reduce layout passes
        MarkdownEditor.BeginChange();

        // Clear existing foreground formatting only
        var fullRange = new TextRange(MarkdownEditor.Document.ContentStart, MarkdownEditor.Document.ContentEnd);
        fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);

        // Pre-compute TextPointer lookup table (O(n) once, then O(1) per match)
        var pointerTable = new TextPointer[text.Length + 1];
        pointerTable[0] = MarkdownEditor.Document.ContentStart;
        var current = MarkdownEditor.Document.ContentStart;
        int idx = 0;
        while (idx < text.Length && current != null)
        {
            var next = current.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null || next == current)
                break;
            current = next;
            idx++;
            if (idx <= text.Length)
                pointerTable[idx] = current;
        }

        // Define highlighting patterns with colors
        var highlights = new (string pattern, Color color)[]
        {
            (@"^#{1,6}\s+.*$", Color.FromRgb(0x00, 0x66, 0xCC)),
            (@"^```[\s\S]*?^```", Color.FromRgb(0x22, 0x88, 0x22)),
            (@"`[^`]+`", Color.FromRgb(0x22, 0x88, 0x22)),
            (@"\*\*[^*]+\*\*", Color.FromRgb(0x88, 0x00, 0x88)),
            (@"\*[^*]+\*", Color.FromRgb(0x88, 0x00, 0x88)),
            (@"\[[^\]]+\]\([^)]+\)", Color.FromRgb(0x00, 0x66, 0xCC)),
            (@"!\[[^\]]*\]\([^)]+\)", Color.FromRgb(0x00, 0x66, 0xCC)),
            (@"^>\s+.*$", Color.FromRgb(0x66, 0x66, 0x66)),
            (@"^[\s]*[-*+]\s+.*$", Color.FromRgb(0xCC, 0x66, 0x00)),
            (@"^[\s]*\d+\.\s+.*$", Color.FromRgb(0xCC, 0x66, 0x00)),
            (@"^[-*_]{3,}\s*$", Color.FromRgb(0x66, 0x66, 0x66)),
            (@"~~[^~]+~~", Color.FromRgb(0x88, 0x00, 0x88)),
        };

        foreach (var (pattern, color) in highlights)
        {
            var brush = new SolidColorBrush(color);
            var regex = new System.Text.RegularExpressions.Regex(pattern,
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (System.Text.RegularExpressions.Match match in regex.Matches(text))
            {
                if (!match.Success)
                    continue;

                // O(1) lookup from pre-computed table
                int startIdx = match.Index;
                int endIdx = match.Index + match.Length;
                if (startIdx >= pointerTable.Length || endIdx >= pointerTable.Length)
                    continue;

                var startPointer = pointerTable[startIdx];
                var endPointer = pointerTable[endIdx];

                if (startPointer != null && endPointer != null)
                {
                    var range = new TextRange(startPointer, endPointer);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                }
            }
        }

        // End batched changes
        MarkdownEditor.EndChange();

        // Restore caret position and selection
        MarkdownEditor.CaretPosition = caretPos;
        if (hasSelection && selectionStart != null && selectionEnd != null)
        {
            MarkdownEditor.Selection.Select(selectionStart, selectionEnd);
        }
    }

    private TextPointer? GetTextPointerAtOffset(int offset)
    {
        if (offset <= 0)
            return MarkdownEditor.Document.ContentStart;

        var current = MarkdownEditor.Document.ContentStart;
        int count = 0;

        while (count < offset && current != null)
        {
            var next = current.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null || next == current)
                break;
            current = next;
            count++;
        }

        return current;
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
