using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MDEditor.Editor;
using MDEditor.Preview;

namespace MDEditor;

/// <summary>
/// Phase 6.2 preview enhancements: outline/TOC panel, find in preview,
/// print preview and full-screen presentation mode.
/// </summary>
public partial class MainWindow
{
    private const double OutlineMinWidth = 140;
    private const double OutlineMaxWidth = 520;

    // Outline panel
    private bool _outlineVisible;
    private List<OutlineNode> _outlineRoots = new();
    private List<OutlineNode> _outlineFlat = new();
    private OutlineNode? _currentOutlineNode;
    private int _outlineVersion;
    private DispatcherTimer? _outlineCurrentTimer;

    // Find in preview
    private DispatcherTimer? _previewFindTimer;

    // Stand-alone preview windows (single instance each)
    private PreviewWindow? _presentationWindow;
    private PreviewWindow? _printPreviewWindow;

    private void InitializePreviewEnhancements()
    {
        _outlineVisible = _settingsService.Get("editor.outline", false);
        var width = _settingsService.Get("editor.outlineWidth", 240.0);
        OutlinePane.Width = Math.Clamp(width, OutlineMinWidth, OutlineMaxWidth);

        // Highlight the section containing the caret (debounced: offset lookup walks the document).
        _outlineCurrentTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _outlineCurrentTimer.Tick += (s, e) =>
        {
            _outlineCurrentTimer.Stop();
            UpdateCurrentOutlineNode();
        };
        MarkdownEditor.SelectionChanged += (s, e) =>
        {
            if (!_outlineVisible || _outlineFlat.Count == 0) return;
            _outlineCurrentTimer.Stop();
            _outlineCurrentTimer.Start();
        };

        _previewFindTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _previewFindTimer.Tick += (s, e) =>
        {
            _previewFindTimer.Stop();
            RunPreviewFind();
        };

        // Ctrl+F while the preview has focus searches the preview rather than the editor.
        MarkdownPreview.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                ShowPreviewFind();
            }
        };

        // F3 / Shift+F3 step through preview matches while the preview pane has focus.
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key != Key.F3 || PreviewFindBar.Visibility != Visibility.Visible || !PreviewPane.IsKeyboardFocusWithin)
                return;

            e.Handled = true;
            StepPreviewFind(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
        };
    }

    #region Outline / TOC panel

    private void ToggleOutline()
    {
        _outlineVisible = !_outlineVisible;
        _settingsService.Set("editor.outline", _outlineVisible);
        ApplyOutlineVisibility();

        if (_outlineVisible)
            OutlineTree.Focus();
    }

    private void ApplyOutlineVisibility()
    {
        OutlinePane.Visibility = _outlineVisible ? Visibility.Visible : Visibility.Collapsed;
        OutlineMenuItem.IsChecked = _outlineVisible;

        if (_outlineVisible)
        {
            UpdateOutline();
        }
        else
        {
            // Release the tree while hidden; it is rebuilt when the panel is shown again.
            _outlineVersion++;
            _outlineRoots = new List<OutlineNode>();
            _outlineFlat = new List<OutlineNode>();
            _currentOutlineNode = null;
            OutlineTree.ItemsSource = null;
        }
    }

    private async void UpdateOutline()
    {
        if (!_outlineVisible)
            return;

        int version = ++_outlineVersion;
        var text = _cachedEditorText ??= EditorText.GetText(MarkdownEditor.Document);

        IReadOnlyList<Core.Models.OutlineItem> items;
        try
        {
            items = await Task.Run(() => _markdownParser.GetOutline(text));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Outline update failed: {ex.Message}");
            return;
        }

        // A newer update started (or the panel closed) while parsing.
        if (version != _outlineVersion || !_outlineVisible)
            return;

        var collapsed = new HashSet<string>(
            _outlineFlat.Where(n => !n.IsExpanded).Select(n => n.Key));

        _outlineRoots = OutlineNode.Build(items, collapsed);
        _outlineFlat = OutlineNode.Flatten(_outlineRoots).ToList();
        _currentOutlineNode = null;

        OutlineTree.ItemsSource = _outlineRoots;
        OutlineEmptyText.Visibility = _outlineFlat.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateCurrentOutlineNode();
    }

    private void UpdateCurrentOutlineNode()
    {
        if (!_outlineVisible || _outlineFlat.Count == 0)
            return;

        var text = _cachedEditorText ??= EditorText.GetText(MarkdownEditor.Document);
        int caretOffset = EditorText.GetOffset(MarkdownEditor.Document, MarkdownEditor.CaretPosition);
        int caretLine = CountLines(text, caretOffset);

        OutlineNode? current = null;
        foreach (var node in _outlineFlat)
        {
            if (node.Line > caretLine)
                break;
            current = node;
        }

        if (ReferenceEquals(current, _currentOutlineNode))
            return;

        if (_currentOutlineNode != null)
            _currentOutlineNode.IsCurrent = false;
        _currentOutlineNode = current;
        if (current != null)
            current.IsCurrent = true;
    }

    private void OutlineTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        // Clicks on the expand/collapse arrow only toggle the node.
        for (var element = source; element != null && element != OutlineTree; element = GetParent(element))
        {
            if (element is ToggleButton)
                return;

            if (element is TreeViewItem item)
            {
                if (item.DataContext is OutlineNode node)
                    NavigateToOutlineNode(node);
                return;
            }
        }
    }

    private void OutlineTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OutlineTree.SelectedItem is OutlineNode node)
        {
            e.Handled = true;
            NavigateToOutlineNode(node);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            MarkdownEditor.Focus();
        }
    }

    private void OutlineExpandAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var node in _outlineFlat)
            node.IsExpanded = true;
    }

    private void OutlineCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var node in _outlineFlat)
            node.IsExpanded = false;
    }

    private void OutlineResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var maxForWindow = MainContentGrid.ActualWidth - EditorColumn.MinWidth - SplitterColumn.ActualWidth -
                           (ViewModel.IsPreviewVisible ? PreviewColumn.MinWidth : 0);
        var max = Math.Max(OutlineMinWidth, Math.Min(OutlineMaxWidth, maxForWindow));
        OutlinePane.Width = Math.Clamp(OutlinePane.Width + e.HorizontalChange, OutlineMinWidth, max);
    }

    private void OutlineResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _settingsService.Set("editor.outlineWidth", OutlinePane.Width);
    }

    private void NavigateToOutlineNode(OutlineNode node)
    {
        try
        {
            var document = MarkdownEditor.Document;
            var text = _cachedEditorText ??= EditorText.GetText(document);
            var pointer = EditorText.GetPointer(document, GetLineStartOffset(text, node.Line)) ?? document.ContentEnd;

            // Prevent the editor and preview scroll handlers from fighting the jump below.
            _syncCooldownUntil = DateTime.Now.AddMilliseconds(500);

            MarkdownEditor.Focus();
            MarkdownEditor.Selection.Select(pointer, pointer);

            // Put the heading at the top of the editor viewport.
            var scrollViewer = FindVisualChild<ScrollViewer>(MarkdownEditor);
            var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
            if (scrollViewer != null && !rect.IsEmpty)
                scrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollViewer.VerticalOffset + rect.Top - 4));

            ScrollPreviewToHeading(node);

            if (_currentOutlineNode != null)
                _currentOutlineNode.IsCurrent = false;
            _currentOutlineNode = node;
            node.IsCurrent = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Outline navigation failed: {ex.Message}");
        }
    }

    private void ScrollPreviewToHeading(OutlineNode node)
    {
        if (MarkdownPreview.CoreWebView2 == null || !ViewModel.IsPreviewVisible)
            return;

        var id = JsonSerializer.Serialize(node.Id ?? string.Empty);
        var title = JsonSerializer.Serialize(node.Title);
        var tag = JsonSerializer.Serialize("h" + node.Level.ToString(CultureInfo.InvariantCulture));

        // Prefer the generated id; fall back to matching heading text (e.g. duplicate-free docs without ids).
        var script = $@"(function(){{
            var el = {id} ? document.getElementById({id}) : null;
            if (!el) {{
                var hs = document.querySelectorAll({tag});
                for (var i = 0; i < hs.length; i++) {{
                    if (hs[i].textContent.replace(/\s+/g,' ').trim() === {title}) {{ el = hs[i]; break; }}
                }}
            }}
            if (el) el.scrollIntoView({{ block: 'start' }});
        }})();";

        _ = MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);
    }

    private static int GetLineStartOffset(string text, int line)
    {
        int offset = 0;
        for (int i = 0; i < line; i++)
        {
            int next = text.IndexOf('\n', offset);
            if (next < 0)
                return text.Length;
            offset = next + 1;
        }

        return offset;
    }

    private static int CountLines(string text, int offset)
    {
        int lines = 0;
        int end = Math.Min(offset, text.Length);
        for (int i = 0; i < end; i++)
        {
            if (text[i] == '\n')
                lines++;
        }

        return lines;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        return element is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(element)
            : LogicalTreeHelper.GetParent(element);
    }

    #endregion

    #region Find in preview

    private void ShowPreviewFind()
    {
        if (!ViewModel.IsPreviewVisible)
        {
            ViewModel.IsPreviewVisible = true;
            TogglePreview();
        }

        PreviewFindBar.Visibility = Visibility.Visible;

        // Seed the query with a short single-line editor selection, like most editors do.
        var selected = MarkdownEditor.Selection.Text;
        if (!string.IsNullOrEmpty(selected) && selected.Length <= 200 && selected.IndexOfAny(new[] { '\r', '\n' }) < 0)
            PreviewFindBox.Text = selected;

        PreviewFindBox.Focus();
        PreviewFindBox.SelectAll();
        RunPreviewFind();
    }

    private void ClosePreviewFind()
    {
        _previewFindTimer?.Stop();
        PreviewFindBar.Visibility = Visibility.Collapsed;
        PreviewFindStatus.Text = string.Empty;

        if (MarkdownPreview.CoreWebView2 != null)
            _ = MarkdownPreview.CoreWebView2.ExecuteScriptAsync("window.__mdeFind && window.__mdeFind.clear();");

        MarkdownEditor.Focus();
    }

    private void RunPreviewFind()
    {
        var query = JsonSerializer.Serialize(PreviewFindBox.Text);
        var matchCase = PreviewFindMatchCase.IsChecked == true ? "true" : "false";
        ExecutePreviewFind($"window.__mdeFind ? window.__mdeFind.search({query}, {matchCase}) : null");
    }

    private void StepPreviewFind(int direction)
    {
        if (string.IsNullOrEmpty(PreviewFindBox.Text))
            return;

        ExecutePreviewFind(direction < 0
            ? "window.__mdeFind ? window.__mdeFind.prev() : null"
            : "window.__mdeFind ? window.__mdeFind.next() : null");
    }

    /// <summary>Re-applies the active search after the preview content was replaced.</summary>
    private void RestorePreviewFind()
    {
        if (PreviewFindBar.Visibility != Visibility.Visible || string.IsNullOrEmpty(PreviewFindBox.Text))
            return;

        var query = JsonSerializer.Serialize(PreviewFindBox.Text);
        var matchCase = PreviewFindMatchCase.IsChecked == true ? "true" : "false";
        ExecutePreviewFind($"window.__mdeFind ? window.__mdeFind.restore({query}, {matchCase}) : null");
    }

    private async void ExecutePreviewFind(string script)
    {
        if (MarkdownPreview.CoreWebView2 == null)
        {
            PreviewFindStatus.Text = string.Empty;
            return;
        }

        try
        {
            var json = await MarkdownPreview.CoreWebView2.ExecuteScriptAsync(script);

            // Scrolling to a match must not drag the editor along.
            _syncCooldownUntil = DateTime.Now.AddMilliseconds(400);

            UpdatePreviewFindStatus(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Find in preview failed: {ex.Message}");
        }
    }

    private void UpdatePreviewFindStatus(string json)
    {
        if (string.IsNullOrEmpty(PreviewFindBox.Text) || string.IsNullOrEmpty(json) || json == "null")
        {
            PreviewFindStatus.Text = string.Empty;
            PreviewFindStatus.Foreground = Brushes.Gray;
            return;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        int count = root.GetProperty("count").GetInt32();
        int index = root.GetProperty("index").GetInt32();
        bool capped = root.TryGetProperty("capped", out var c) && c.GetBoolean();

        if (count == 0)
        {
            PreviewFindStatus.Text = "No results";
            PreviewFindStatus.Foreground = Brushes.IndianRed;
        }
        else
        {
            PreviewFindStatus.Text = $"{index + 1} of {count}{(capped ? "+" : "")}";
            PreviewFindStatus.Foreground = Brushes.Gray;
        }
    }

    private void PreviewFindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_previewFindTimer == null || PreviewFindBar.Visibility != Visibility.Visible)
            return;

        _previewFindTimer.Stop();
        _previewFindTimer.Start();
    }

    private void PreviewFindBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                if (_previewFindTimer?.IsEnabled == true)
                {
                    // Search not run yet for the latest text: run it now instead of stepping.
                    _previewFindTimer.Stop();
                    RunPreviewFind();
                }
                else
                {
                    StepPreviewFind(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
                }
                break;
            case Key.Escape:
                e.Handled = true;
                ClosePreviewFind();
                break;
        }
    }

    private void PreviewFindMatchCase_Click(object sender, RoutedEventArgs e) => RunPreviewFind();

    private void PreviewFindNext_Click(object sender, RoutedEventArgs e) => StepPreviewFind(1);

    private void PreviewFindPrevious_Click(object sender, RoutedEventArgs e) => StepPreviewFind(-1);

    private void PreviewFindClose_Click(object sender, RoutedEventArgs e) => ClosePreviewFind();

    #endregion

    #region Print preview & presentation mode

    private void ShowPrintPreview()
    {
        if (_printPreviewWindow != null)
        {
            _printPreviewWindow.Activate();
            return;
        }

        _printPreviewWindow = new PreviewWindow(_markdownParser, GetEditorText(), PreviewWindowMode.Print)
        {
            Owner = this
        };
        _printPreviewWindow.Closed += (s, e) => _printPreviewWindow = null;
        _printPreviewWindow.Show();
    }

    private void TogglePresentationMode()
    {
        if (_presentationWindow != null)
        {
            _presentationWindow.Close();
            return;
        }

        _presentationWindow = new PreviewWindow(_markdownParser, GetEditorText(), PreviewWindowMode.Presentation, GetEditorScrollPercent(), GetPreviewRenderOptions())
        {
            Owner = this
        };
        _presentationWindow.Closed += (s, e) =>
        {
            _presentationWindow = null;
            Activate();
            MarkdownEditor.Focus();
        };
        _presentationWindow.Show();
    }

    private double GetEditorScrollPercent()
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(MarkdownEditor);
        if (scrollViewer == null)
            return 0;

        var max = scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
        return max > 0 ? Math.Clamp(scrollViewer.VerticalOffset / max, 0, 1) : 0;
    }

    #endregion
}
