using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MDEditor;

/// <summary>
/// A simple Go to Line dialog.
/// </summary>
public class GoToLineDialog : Window
{
    private readonly RichTextBox _editor;
    private TextBox _lineNumberBox;
    private TextBlock _statusText;

    public GoToLineDialog(RichTextBox editor)
    {
        _editor = editor;

        Title = "Go to Line";
        Width = 320;
        Height = 160;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid { Margin = new Thickness(10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock { Text = "Line number:", Margin = new Thickness(0, 0, 0, 5) };
        grid.Children.Add(label);
        Grid.SetRow(label, 0);
        Grid.SetColumnSpan(label, 2);

        _lineNumberBox = new TextBox { Width = 200, Margin = new Thickness(0, 0, 0, 5) };
        _lineNumberBox.PreviewTextInput += (s, e) =>
        {
            e.Handled = !int.TryParse(e.Text, out _);
        };
        DataObject.AddPastingHandler(_lineNumberBox, (s, e) =>
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string))!;
                if (!int.TryParse(text, out _))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        });
        _lineNumberBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Go_Click(s, new RoutedEventArgs());
                e.Handled = true;
            }
        };
        grid.Children.Add(_lineNumberBox);
        Grid.SetRow(_lineNumberBox, 1);
        Grid.SetColumn(_lineNumberBox, 0);

        var button = new Button { Content = "Go", Width = 80, Margin = new Thickness(5, 0, 0, 5) };
        button.Click += Go_Click;
        grid.Children.Add(button);
        Grid.SetRow(button, 1);
        Grid.SetColumn(button, 1);

        _statusText = new TextBlock { Text = "", Margin = new Thickness(0, 5, 0, 0) };
        grid.Children.Add(_statusText);
        Grid.SetRow(_statusText, 2);
        Grid.SetColumnSpan(_statusText, 2);

        Content = grid;
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_lineNumberBox.Text, out int lineNumber) || lineNumber < 1)
        {
            _statusText.Text = "Invalid line number";
            return;
        }

        // Try paragraph-based navigation first (works when Enter creates paragraphs)
        var paragraphs = new System.Collections.Generic.List<Paragraph>();
        foreach (Block block in _editor.Document.Blocks)
        {
            if (block is Paragraph p)
                paragraphs.Add(p);
        }

        // If paragraphs match the expected line count, use paragraph navigation
        var fullText = GetEditorText();
        var textLines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        // Remove trailing empty entry from trailing newline
        if (textLines.Length > 1 && string.IsNullOrEmpty(textLines[textLines.Length - 1]))
            textLines = textLines[..^1];

        if (paragraphs.Count == textLines.Length && lineNumber <= paragraphs.Count)
        {
            var targetParagraph = paragraphs[lineNumber - 1];
            var position = targetParagraph.ContentStart;
            _editor.CaretPosition = position;
            _editor.Focus();
            var rect = position.GetCharacterRect(LogicalDirection.Forward);
            _editor.ScrollToVerticalOffset(Math.Max(0, rect.Top - 20));
            _statusText.Text = $"Navigated to line {lineNumber}";
            return;
        }

        // Fallback: offset-based navigation using TextRange measurement
        if (lineNumber > textLines.Length)
        {
            _statusText.Text = $"Max line is {textLines.Length}";
            return;
        }

        int offset = 0;
        for (int i = 0; i < lineNumber - 1; i++)
        {
            offset += textLines[i].Length;
            // Add line ending length
            int pos = offset;
            if (pos < fullText.Length)
            {
                if (pos + 1 < fullText.Length && fullText[pos] == '\r' && fullText[pos + 1] == '\n')
                    offset += 2;
                else if (fullText[pos] == '\r' || fullText[pos] == '\n')
                    offset += 1;
            }
        }

        var position2 = NavigateToOffset(offset);
        _editor.CaretPosition = position2;
        _editor.Focus();
        var rect2 = position2.GetCharacterRect(LogicalDirection.Forward);
        _editor.ScrollToVerticalOffset(Math.Max(0, rect2.Top - 20));
        _statusText.Text = $"Navigated to line {lineNumber}";
    }

    private TextPointer NavigateToOffset(int charOffset)
    {
        if (charOffset <= 0)
            return _editor.Document.ContentStart;

        var start = _editor.Document.ContentStart;
        var current = start;
        int currentLength = 0;

        while (currentLength < charOffset)
        {
            var next = current.GetNextInsertionPosition(LogicalDirection.Forward);
            if (next == null || next == current)
                break;

            var testRange = new TextRange(start, next);
            var text = testRange.Text;
            while (text.Length > 0 && (text[text.Length - 1] == '\r' || text[text.Length - 1] == '\n'))
                text = text.Substring(0, text.Length - 1);

            if (text.Length > charOffset)
                break;

            current = next;
            currentLength = text.Length;
        }

        if (currentLength < charOffset && current != null)
        {
            int remaining = charOffset - currentLength;
            var adjusted = current.GetPositionAtOffset(remaining, LogicalDirection.Forward);
            if (adjusted != null && adjusted != current)
                current = adjusted;
        }

        return current ?? _editor.Document.ContentStart;
    }

    private string GetEditorText()
    {
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        return range.Text;
    }
}
