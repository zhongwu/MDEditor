using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace MDEditor;

/// <summary>
/// A simple Find dialog for searching text in the RichTextBox editor.
/// </summary>
public class FindReplaceDialog : Window
{
    private readonly RichTextBox _editor;
    private readonly bool _showReplace;
    private readonly Action? _onNavigate;
    private TextBox _searchBox;
    private TextBox? _replaceBox;
    private Button _findNextButton;
    private Button? _replaceButton;
    private Button? _replaceAllButton;
    private Button _findAllButton;
    private CheckBox _caseSensitiveCheckBox;
    private TextBlock _statusText;

    // Cache for "Find All" results
    private List<(TextPointer Start, TextPointer End)>? _allMatches;
    private int _currentMatchIndex = -1;
    private string? _lastSearchText;
    private bool _lastCaseSensitive;

    public FindReplaceDialog(RichTextBox editor, bool showReplace, Action? onNavigate = null)
    {
        _editor = editor;
        _showReplace = showReplace;
        _onNavigate = onNavigate;

        Title = showReplace ? "Find & Replace" : "Find";
        Width = 400;
        Height = showReplace ? 220 : 150;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid();
        grid.Margin = new Thickness(10);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Search row
        var searchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
        searchPanel.Children.Add(new TextBlock { Text = "Find:", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        _searchBox = new TextBox { Width = 250 };
        _searchBox.TextChanged += (s, e) => InvalidateCache();
        searchPanel.Children.Add(_searchBox);
        grid.Children.Add(searchPanel);
        Grid.SetRow(searchPanel, 0);

        // Replace row (only if showReplace)
        if (showReplace)
        {
            var replacePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            replacePanel.Children.Add(new TextBlock { Text = "Replace:", Width = 60, VerticalAlignment = VerticalAlignment.Center });
            _replaceBox = new TextBox { Width = 250 };
            replacePanel.Children.Add(_replaceBox);
            grid.Children.Add(replacePanel);
            Grid.SetRow(replacePanel, 1);
        }

        // Case sensitive checkbox
        _caseSensitiveCheckBox = new CheckBox { Content = "Match case", Margin = new Thickness(0, 5, 0, 5) };
        _caseSensitiveCheckBox.Checked += (s, e) => InvalidateCache();
        _caseSensitiveCheckBox.Unchecked += (s, e) => InvalidateCache();
        grid.Children.Add(_caseSensitiveCheckBox);
        Grid.SetRow(_caseSensitiveCheckBox, showReplace ? 2 : 1);

        // Buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
        _findNextButton = new Button { Content = "Find Next", Width = 90, Margin = new Thickness(0, 0, 5, 0) };
        _findNextButton.Click += FindNext_Click;
        buttonPanel.Children.Add(_findNextButton);

        if (showReplace)
        {
            _replaceButton = new Button { Content = "Replace", Width = 90, Margin = new Thickness(0, 0, 5, 0) };
            _replaceButton.Click += Replace_Click;
            buttonPanel.Children.Add(_replaceButton);

            _replaceAllButton = new Button { Content = "Replace All", Width = 90, Margin = new Thickness(0, 0, 5, 0) };
            _replaceAllButton.Click += ReplaceAll_Click;
            buttonPanel.Children.Add(_replaceAllButton);
        }

        _findAllButton = new Button { Content = "Find All", Width = 90, Margin = new Thickness(0, 0, 5, 0) };
        _findAllButton.Click += FindAll_Click;
        buttonPanel.Children.Add(_findAllButton);

        // Status text next to buttons
        _statusText = new TextBlock { Text = "", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        buttonPanel.Children.Add(_statusText);

        grid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, showReplace ? 3 : 2);

        Content = grid;
    }

    private void InvalidateCache()
    {
        _allMatches = null;
        _currentMatchIndex = -1;
        _lastSearchText = null;
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        var searchText = _searchBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = _caseSensitiveCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // If search params changed, rebuild cache
        if (_allMatches == null || _lastSearchText != searchText || _lastCaseSensitive != _caseSensitiveCheckBox.IsChecked)
        {
            BuildMatchCache(searchText, comparison);
            _lastSearchText = searchText;
            _lastCaseSensitive = _caseSensitiveCheckBox.IsChecked ?? false;
        }

        if (_allMatches != null && _allMatches.Count > 0)
        {
            // Find next match after current selection END
            var selEnd = _editor.Selection.End;
            int nextIndex = 0;

            for (int i = 0; i < _allMatches.Count; i++)
            {
                if (_allMatches[i].Start.CompareTo(selEnd) > 0)
                {
                    nextIndex = i;
                    break;
                }
            }

            _currentMatchIndex = nextIndex;
            var match = _allMatches[nextIndex];
            SelectAndScroll(match.Start, match.End);
            _statusText.Text = $"Found {nextIndex + 1} of {_allMatches.Count}";
        }
        else
        {
            _statusText.Text = "Not found";
        }
    }

    private void FindAll_Click(object sender, RoutedEventArgs e)
    {
        var searchText = _searchBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = _caseSensitiveCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        BuildMatchCache(searchText, comparison);
        _lastSearchText = searchText;
        _lastCaseSensitive = _caseSensitiveCheckBox.IsChecked ?? false;

        if (_allMatches != null && _allMatches.Count > 0)
        {
            _currentMatchIndex = 0;
            var match = _allMatches[0];
            SelectAndScroll(match.Start, match.End);
            _statusText.Text = $"Found {_allMatches.Count} match(es)";
        }
        else
        {
            _statusText.Text = "Not found";
        }
    }

    /// <summary>
    /// Fast paragraph-based search. Uses paraText.IndexOf for finding match locations,
    /// then walks insertion positions to convert character index to TextPointer.
    /// </summary>
    private void BuildMatchCache(string searchText, StringComparison comparison)
    {
        _allMatches = new List<(TextPointer Start, TextPointer End)>();

        foreach (Block block in _editor.Document.Blocks)
        {
            if (block is Paragraph para)
            {
                var paraStart = para.ContentStart;
                var paraEnd = para.ContentEnd;
                
                // Get paragraph text
                var paraText = new TextRange(paraStart, paraEnd).Text;
                
                // Find all matches in this paragraph using string search
                int searchStart = 0;
                while (true)
                {
                    int foundAt = paraText.IndexOf(searchText, searchStart, comparison);
                    if (foundAt < 0)
                        break;

                    // Verify match is not in the trailing paragraph separator
                    int paraContentLength = paraText.Length;
                    while (paraContentLength > 0 && (paraText[paraContentLength - 1] == '\r' || paraText[paraContentLength - 1] == '\n'))
                        paraContentLength--;

                    if (foundAt + searchText.Length <= paraContentLength)
                    {
                        // Convert character index to TextPointer by walking insertion positions
                        var matchStart = GetTextPointerAtCharIndex(paraStart, paraEnd, foundAt);
                        var matchEnd = GetTextPointerAtCharIndex(paraStart, paraEnd, foundAt + searchText.Length);

                        if (matchStart != null && matchEnd != null)
                        {
                            // Verify exact match
                            var verifyRange = new TextRange(matchStart, matchEnd);
                            var verifyText = verifyRange.Text.TrimEnd('\r', '\n');
                            
                            if (string.Equals(verifyText, searchText, comparison))
                            {
                                _allMatches.Add((matchStart, matchEnd));
                                searchStart = foundAt + 1;
                                continue;
                            }
                        }
                    }

                    searchStart = foundAt + 1;
                }
            }
        }
    }

    /// <summary>
    /// Gets TextPointer at a specific character index within a paragraph.
    /// Walks insertion positions from paragraph start, measuring text length.
    /// </summary>
    private TextPointer GetTextPointerAtCharIndex(TextPointer paraStart, TextPointer paraEnd, int targetCharIndex)
    {
        if (targetCharIndex <= 0)
            return paraStart;

        var current = paraStart;
        const int MaxIterations = 5000; // Safety limit
        int iterations = 0;

        while (current != null && current.CompareTo(paraEnd) <= 0 && iterations < MaxIterations)
        {
            iterations++;
            
            // Measure text from paragraph start to current position
            var range = new TextRange(paraStart, current);
            var textSoFar = range.Text;
            
            // Remove trailing paragraph separator for accurate length
            int textLen = textSoFar.Length;
            while (textLen > 0 && (textSoFar[textLen - 1] == '\r' || textSoFar[textLen - 1] == '\n'))
                textLen--;

            if (textLen >= targetCharIndex)
                break;

            // Advance to next insertion position
            var next = current.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null || next.CompareTo(current) <= 0)
                break;
            
            current = next;
        }

        return current ?? paraStart;
    }

    private void SelectAndScroll(TextPointer start, TextPointer end)
    {
        _editor.Selection.Select(start, end);
        _editor.Focus();

        // WPF brings the caret into view by itself as soon as the selection
        // changes, which fights with our own scrolling and makes the view jump.
        // Do the final scroll once layout has settled so ours wins.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ScrollToMiddle(start);
            _onNavigate?.Invoke();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Scrolls the editor so the given position sits in the middle of the
    /// viewport. GetCharacterRect returns coordinates relative to the visible
    /// area, so the current scroll offset has to be added to get the position
    /// inside the document (same math GoToLineDialog uses).
    /// </summary>
    private void ScrollToMiddle(TextPointer pointer)
    {
        _editor.UpdateLayout();

        var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty)
            rect = pointer.GetCharacterRect(LogicalDirection.Backward);
        if (rect.IsEmpty)
            return;

        double documentTop = _editor.VerticalOffset + rect.Top;
        double viewportHeight = _editor.ViewportHeight > 0 ? _editor.ViewportHeight : _editor.ActualHeight;
        double targetVertical = documentTop - Math.Max(0, (viewportHeight - rect.Height) / 2.0);
        _editor.ScrollToVerticalOffset(Math.Max(0, targetVertical));

        // Only move horizontally when the match is actually off-screen,
        // otherwise selected text causes needless sideways jumps.
        if (rect.Left < 0 || rect.Right > _editor.ViewportWidth)
        {
            double documentLeft = _editor.HorizontalOffset + rect.Left;
            _editor.ScrollToHorizontalOffset(Math.Max(0, documentLeft - 20));
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Selection.Text.Length > 0)
        {
            _editor.Selection.Text = _replaceBox?.Text ?? string.Empty;
            InvalidateCache();
        }
        FindNext_Click(sender, e);
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var searchText = _searchBox.Text;
        var replaceText = _replaceBox?.Text ?? string.Empty;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = GetEditorText();
        var comparison = _caseSensitiveCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(searchText, index, comparison)) >= 0)
        {
            count++;
            index += searchText.Length;
        }

        var newText = text.Replace(searchText, replaceText, comparison);
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        range.Text = newText;

        InvalidateCache();
        _statusText.Text = $"Replaced {count} occurrence(s)";
    }

    private string GetEditorText()
    {
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        return range.Text;
    }
}