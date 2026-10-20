using System;
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
    private TextBox _searchBox;
    private TextBox? _replaceBox;
    private Button _findNextButton;
    private Button? _replaceButton;
    private Button? _replaceAllButton;
    private CheckBox _caseSensitiveCheckBox;
    private TextBlock _statusText;

    public FindReplaceDialog(RichTextBox editor, bool showReplace)
    {
        _editor = editor;
        _showReplace = showReplace;

        Title = showReplace ? "Find & Replace" : "Find";
        Width = 400;
        Height = showReplace ? 200 : 150;
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
        grid.Children.Add(_caseSensitiveCheckBox);
        Grid.SetRow(_caseSensitiveCheckBox, 2);

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

        // Status text next to buttons
        _statusText = new TextBlock { Text = "", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        buttonPanel.Children.Add(_statusText);

        grid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 3);

        Content = grid;
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        var searchText = _searchBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = _caseSensitiveCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // Get the full document text
        var fullText = GetEditorText();

        // Find current selection end in the full text
        var beforeSelection = new TextRange(_editor.Document.ContentStart, _editor.Selection.End);
        var beforeText = beforeSelection.Text;
        var selEndIndex = beforeText.Length;

        // Search from current position
        var index = fullText.IndexOf(searchText, selEndIndex, comparison);
        if (index < 0)
        {
            index = fullText.IndexOf(searchText, 0, comparison);
        }

        if (index >= 0)
        {
            // Use TextRange-based position finding for reliability
            var startPointer = GetPositionAtCharIndex(index);
            var endPointer = GetPositionAtCharIndex(index + searchText.Length);

            _editor.Selection.Select(startPointer, endPointer);
            // Scroll to make the found text visible
            var rect = _editor.Selection.Start.GetCharacterRect(LogicalDirection.Forward);
            _editor.ScrollToVerticalOffset(Math.Max(0, rect.Top - 50));
            _editor.Focus();
            _statusText.Text = "Found";
        }
        else
        {
            _statusText.Text = "Not found";
        }
    }

    /// <summary>
    /// Finds the TextPointer at a specific character index using TextRange length measurement.
    /// This is more reliable than GetPositionAtOffset or GetNextInsertionPosition.
    /// </summary>
    private TextPointer GetPositionAtCharIndex(int charIndex)
    {
        if (charIndex <= 0)
            return _editor.Document.ContentStart;

        var start = _editor.Document.ContentStart;
        var current = start;
        int currentLength = 0;

        while (currentLength < charIndex)
        {
            var next = current.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null || next == current)
                break;

            var testRange = new TextRange(start, next);
            var text = testRange.Text;
            // Strip trailing paragraph terminator for accurate measurement
            while (text.Length > 0 && (text[text.Length - 1] == '\r' || text[text.Length - 1] == '\n'))
                text = text.Substring(0, text.Length - 1);

            if (text.Length > charIndex)
                break;

            current = next;
            currentLength = text.Length;
        }

        // Fine-tune if we're short
        if (currentLength < charIndex && current != null)
        {
            int remaining = charIndex - currentLength;
            var adjusted = current.GetPositionAtOffset(remaining, LogicalDirection.Forward);
            if (adjusted != null && adjusted != current)
                current = adjusted;
        }

        return current ?? _editor.Document.ContentStart;
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (_editor.Selection.Text.Length > 0)
        {
            _editor.Selection.Text = _replaceBox?.Text ?? string.Empty;
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

        // Simple replace all using string replacement
        var newText = text.Replace(searchText, replaceText, comparison);
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        range.Text = newText;

        _statusText.Text = $"Replaced {count} occurrence(s)";
    }

    private string GetEditorText()
    {
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        return range.Text;
    }
}
