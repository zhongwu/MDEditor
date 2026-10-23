using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MDEditor.Editor;

namespace MDEditor;

/// <summary>
/// Inline find / replace bar above the editor, modelled on find in preview:
/// live search while typing, every match highlighted, "N of M" count,
/// Enter / F3 to step through matches and Esc to close.
/// </summary>
public partial class MainWindow
{
    private const int EditorFindMaxMatches = 5000;

    private DispatcherTimer? _editorFindTimer;
    private DispatcherTimer? _editorFindRefreshTimer;
    private List<(TextPointer Start, TextPointer End)> _editorFindMatches = new();
    private int _editorFindIndex = -1;
    private bool _editorFindCapped;
    private bool _editorFindReplacing;

    private void InitializeEditorFind()
    {
        EditorFindOverlay.Editor = MarkdownEditor;

        // Typing in the find box: search from the caret and jump to the nearest match.
        _editorFindTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _editorFindTimer.Tick += (s, e) =>
        {
            _editorFindTimer.Stop();
            RunEditorFind(GetSelectionStartOffset(), select: true);
        };

        // Editing the document while the bar is open: refresh the matches in place.
        _editorFindRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _editorFindRefreshTimer.Tick += (s, e) =>
        {
            _editorFindRefreshTimer.Stop();
            RefreshEditorFind();
        };

        MarkdownEditor.TextChanged += (s, e) =>
        {
            if (_editorFindReplacing || EditorFindBar.Visibility != Visibility.Visible)
                return;

            _editorFindRefreshTimer.Stop();
            _editorFindRefreshTimer.Start();
        };

        // F3 / Shift+F3 and Esc while the editor pane has focus.
        PreviewKeyDown += (s, e) =>
        {
            if (EditorFindBar.Visibility != Visibility.Visible || !EditorPane.IsKeyboardFocusWithin)
                return;

            if (e.Key == Key.F3)
            {
                e.Handled = true;
                StepEditorFind(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            }
            else if (e.Key == Key.Escape && MarkdownEditor.IsKeyboardFocusWithin && !_completionPopup.IsOpen)
            {
                e.Handled = true;
                CloseEditorFind();
            }
        };
    }

    private void ShowEditorFind(bool replace)
    {
        EditorFindBar.Visibility = Visibility.Visible;
        if (replace)
            SetEditorReplaceVisible(true);

        // Seed the query with a short single-line selection, like most editors do.
        var selected = MarkdownEditor.Selection.Text;
        if (!string.IsNullOrEmpty(selected) && selected.Length <= 200 && selected.IndexOfAny(new[] { '\r', '\n' }) < 0)
            EditorFindBox.Text = selected;

        _editorFindTimer?.Stop();

        if (replace && !string.IsNullOrEmpty(EditorFindBox.Text))
        {
            EditorReplaceBox.Focus();
            EditorReplaceBox.SelectAll();
        }
        else
        {
            EditorFindBox.Focus();
            EditorFindBox.SelectAll();
        }

        RunEditorFind(GetSelectionStartOffset(), select: true);
    }

    private void CloseEditorFind()
    {
        _editorFindTimer?.Stop();
        _editorFindRefreshTimer?.Stop();
        EditorFindBar.Visibility = Visibility.Collapsed;
        EditorFindStatus.Text = string.Empty;
        _editorFindMatches = new List<(TextPointer Start, TextPointer End)>();
        _editorFindIndex = -1;
        EditorFindOverlay.Clear();

        // The current match stays selected, so the user can carry on from it.
        MarkdownEditor.Focus();
    }

    private void SetEditorReplaceVisible(bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        EditorReplaceBox.Visibility = visibility;
        EditorReplaceActions.Visibility = visibility;
        EditorFindToggleReplace.Content = visible ? "" : "";
    }

    private StringComparison EditorFindComparison =>
        EditorFindMatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private int GetSelectionStartOffset() =>
        EditorText.GetOffset(MarkdownEditor.Document, MarkdownEditor.Selection.Start);

    /// <summary>
    /// Finds every match and makes the first one at or after <paramref name="anchorOffset"/>
    /// current. With <paramref name="select"/> the current match is also selected and scrolled to.
    /// </summary>
    private void RunEditorFind(int anchorOffset, bool select)
    {
        var query = EditorFindBox.Text;
        var offsets = FindEditorMatchOffsets(query, EditorFindMaxMatches, out _editorFindCapped);

        _editorFindMatches = ResolveMatches(offsets, query.Length);
        _editorFindIndex = -1;

        if (_editorFindMatches.Count > 0)
        {
            int index = offsets.FindIndex(o => o >= anchorOffset);
            _editorFindIndex = index >= 0 ? index : 0;
        }

        ApplyEditorFindIndex(select);
    }

    /// <summary>Re-runs the search after the document changed, keeping the current match without moving.</summary>
    private void RefreshEditorFind()
    {
        if (EditorFindBar.Visibility != Visibility.Visible)
            return;

        int anchor = _editorFindIndex >= 0 && _editorFindIndex < _editorFindMatches.Count
            ? EditorText.GetOffset(MarkdownEditor.Document, _editorFindMatches[_editorFindIndex].Start)
            : GetSelectionStartOffset();

        RunEditorFind(anchor, select: false);
    }

    private List<int> FindEditorMatchOffsets(string query, int limit, out bool capped)
    {
        var offsets = new List<int>();
        capped = false;

        // Matches never span paragraphs, so a query with a line break cannot match.
        if (string.IsNullOrEmpty(query) || query.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            return offsets;

        _cachedEditorText ??= EditorText.GetText(MarkdownEditor.Document);
        var text = _cachedEditorText;
        var comparison = EditorFindComparison;

        int index = text.IndexOf(query, 0, comparison);
        while (index >= 0)
        {
            if (offsets.Count >= limit)
            {
                capped = true;
                break;
            }

            offsets.Add(index);
            index = text.IndexOf(query, index + query.Length, comparison);
        }

        return offsets;
    }

    private List<(TextPointer Start, TextPointer End)> ResolveMatches(List<int> offsets, int length)
    {
        var bounds = new List<int>(offsets.Count * 2);
        foreach (var offset in offsets)
        {
            bounds.Add(offset);
            bounds.Add(offset + length);
        }

        var pointers = EditorText.GetPointers(MarkdownEditor.Document, bounds);
        var matches = new List<(TextPointer Start, TextPointer End)>(offsets.Count);
        for (int i = 0; i + 1 < pointers.Length; i += 2)
        {
            if (pointers[i] is { } start && pointers[i + 1] is { } end)
            {
                // Backward gravity keeps the end in place when text is inserted right after it
                // (Replace All works back to front, so this happens for adjacent matches).
                matches.Add((start, end.GetPositionAtOffset(0, LogicalDirection.Backward)));
            }
        }

        return matches;
    }

    private void ApplyEditorFindIndex(bool select)
    {
        EditorFindOverlay.SetMatches(_editorFindMatches, _editorFindIndex);
        UpdateEditorFindStatus();

        if (select && _editorFindIndex >= 0)
            SelectEditorMatch(_editorFindMatches[_editorFindIndex]);
    }

    private void StepEditorFind(int direction)
    {
        if (string.IsNullOrEmpty(EditorFindBox.Text))
            return;

        if (_editorFindTimer?.IsEnabled == true || _editorFindRefreshTimer?.IsEnabled == true)
        {
            // Search not run yet for the latest text: run it now instead of stepping.
            _editorFindTimer?.Stop();
            _editorFindRefreshTimer?.Stop();
            RunEditorFind(GetSelectionStartOffset(), select: true);
            return;
        }

        int count = _editorFindMatches.Count;
        if (count == 0)
            return;

        // Step relative to the selection so the search continues from wherever the caret was moved to.
        var selectionStart = MarkdownEditor.Selection.Start;
        int index;
        if (direction > 0)
        {
            index = _editorFindMatches.FindIndex(m => m.Start.CompareTo(selectionStart) > 0);
            if (index < 0) index = 0;
        }
        else
        {
            index = _editorFindMatches.FindLastIndex(m => m.Start.CompareTo(selectionStart) < 0);
            if (index < 0) index = count - 1;
        }

        _editorFindIndex = index;
        ApplyEditorFindIndex(select: true);
    }

    private void SelectEditorMatch((TextPointer Start, TextPointer End) match)
    {
        bool visible = IsEditorPositionVisible(match.Start);
        MarkdownEditor.Selection.Select(match.Start, match.End);

        if (visible)
            return;

        // WPF brings the selection into view by itself, which would leave the match
        // at the viewport edge. Center it once layout has settled so ours wins.
        Dispatcher.BeginInvoke(new Action(() => ScrollEditorToMiddle(match.Start)), DispatcherPriority.Loaded);
    }

    private bool IsEditorPositionVisible(TextPointer pointer)
    {
        var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty)
            return false;

        double viewportHeight = MarkdownEditor.ViewportHeight > 0 ? MarkdownEditor.ViewportHeight : MarkdownEditor.ActualHeight;
        double viewportWidth = MarkdownEditor.ViewportWidth > 0 ? MarkdownEditor.ViewportWidth : MarkdownEditor.ActualWidth;
        return rect.Top >= 0 && rect.Bottom <= viewportHeight && rect.Left >= 0 && rect.Right <= viewportWidth;
    }

    /// <summary>
    /// Scrolls the editor so the given position sits in the middle of the viewport.
    /// GetCharacterRect is relative to the visible area, so the scroll offset is added.
    /// </summary>
    private void ScrollEditorToMiddle(TextPointer pointer)
    {
        MarkdownEditor.UpdateLayout();

        var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty)
            rect = pointer.GetCharacterRect(LogicalDirection.Backward);
        if (rect.IsEmpty)
            return;

        double documentTop = MarkdownEditor.VerticalOffset + rect.Top;
        double viewportHeight = MarkdownEditor.ViewportHeight > 0 ? MarkdownEditor.ViewportHeight : MarkdownEditor.ActualHeight;
        MarkdownEditor.ScrollToVerticalOffset(Math.Max(0, documentTop - Math.Max(0, (viewportHeight - rect.Height) / 2.0)));

        // Only move horizontally when the match is actually off-screen.
        if (rect.Left < 0 || rect.Right > MarkdownEditor.ViewportWidth)
        {
            double documentLeft = MarkdownEditor.HorizontalOffset + rect.Left;
            MarkdownEditor.ScrollToHorizontalOffset(Math.Max(0, documentLeft - 20));
        }
    }

    private void UpdateEditorFindStatus()
    {
        if (string.IsNullOrEmpty(EditorFindBox.Text))
        {
            EditorFindStatus.Text = string.Empty;
            EditorFindStatus.Foreground = Brushes.Gray;
        }
        else if (_editorFindMatches.Count == 0)
        {
            EditorFindStatus.Text = "No results";
            EditorFindStatus.Foreground = Brushes.IndianRed;
        }
        else
        {
            EditorFindStatus.Text = $"{_editorFindIndex + 1} of {_editorFindMatches.Count}{(_editorFindCapped ? "+" : "")}";
            EditorFindStatus.Foreground = Brushes.Gray;
        }
    }

    private void ReplaceEditorMatch()
    {
        if (_editorFindTimer?.IsEnabled == true || _editorFindRefreshTimer?.IsEnabled == true)
        {
            _editorFindTimer?.Stop();
            _editorFindRefreshTimer?.Stop();
            RunEditorFind(GetSelectionStartOffset(), select: true);
            return;
        }

        if (_editorFindIndex < 0 || _editorFindIndex >= _editorFindMatches.Count)
            return;

        var query = EditorFindBox.Text;
        var replacement = EditorReplaceBox.Text;
        var (start, end) = _editorFindMatches[_editorFindIndex];
        var range = new TextRange(start, end);

        // The document may have changed under the match: search again rather than replace stale text.
        if (!string.Equals(range.Text, query, EditorFindComparison))
        {
            RefreshEditorFind();
            return;
        }

        int startOffset = EditorText.GetOffset(MarkdownEditor.Document, start);

        _editorFindReplacing = true;
        try
        {
            range.Text = replacement;
        }
        finally
        {
            _editorFindReplacing = false;
        }

        // Continue with the next match after the replaced text.
        RunEditorFind(startOffset + replacement.Length, select: true);
    }

    private void ReplaceAllEditorMatches()
    {
        _editorFindTimer?.Stop();
        _editorFindRefreshTimer?.Stop();

        var query = EditorFindBox.Text;
        var replacement = EditorReplaceBox.Text;
        var matches = ResolveMatches(FindEditorMatchOffsets(query, int.MaxValue, out _), query.Length);
        if (matches.Count == 0)
        {
            RunEditorFind(0, select: false);
            return;
        }

        _editorFindReplacing = true;
        MarkdownEditor.BeginChange();
        try
        {
            // Back to front so earlier matches are not shifted; one undo step for the lot.
            for (int i = matches.Count - 1; i >= 0; i--)
                new TextRange(matches[i].Start, matches[i].End).Text = replacement;
        }
        finally
        {
            MarkdownEditor.EndChange();
            _editorFindReplacing = false;
        }

        RunEditorFind(0, select: false);
        EditorFindStatus.Text = $"Replaced {matches.Count}";
        EditorFindStatus.Foreground = Brushes.Gray;
    }

    private void EditorFindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_editorFindTimer == null || EditorFindBar.Visibility != Visibility.Visible)
            return;

        _editorFindTimer.Stop();
        _editorFindTimer.Start();
    }

    private void EditorFindBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                StepEditorFind(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
                break;
            case Key.Escape:
                e.Handled = true;
                CloseEditorFind();
                break;
        }
    }

    private void EditorReplaceBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    ReplaceAllEditorMatches();
                else
                    ReplaceEditorMatch();
                break;
            case Key.Escape:
                e.Handled = true;
                CloseEditorFind();
                break;
        }
    }

    private void EditorFindMatchCase_Click(object sender, RoutedEventArgs e) =>
        RunEditorFind(GetSelectionStartOffset(), select: true);

    private void EditorFindToggleReplace_Click(object sender, RoutedEventArgs e)
    {
        bool show = EditorReplaceBox.Visibility != Visibility.Visible;
        SetEditorReplaceVisible(show);
        if (show) EditorReplaceBox.Focus(); else EditorFindBox.Focus();
    }

    private void EditorFindNext_Click(object sender, RoutedEventArgs e) => StepEditorFind(1);

    private void EditorFindPrevious_Click(object sender, RoutedEventArgs e) => StepEditorFind(-1);

    private void EditorFindClose_Click(object sender, RoutedEventArgs e) => CloseEditorFind();

    private void EditorReplace_Click(object sender, RoutedEventArgs e) => ReplaceEditorMatch();

    private void EditorReplaceAll_Click(object sender, RoutedEventArgs e) => ReplaceAllEditorMatches();
}
