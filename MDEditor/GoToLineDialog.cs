using System;
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
    private readonly Action? _onNavigated;
    private TextBox _lineNumberBox;
    private TextBlock _statusText;

    public GoToLineDialog(RichTextBox editor, Action? onNavigated = null)
    {
        _editor = editor;
        _onNavigated = onNavigated;

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

        _lineNumberBox = new TextBox { Width = 150, Margin = new Thickness(0, 0, 0, 5) };
        _lineNumberBox.PreviewTextInput += (s, e) => e.Handled = !int.TryParse(e.Text, out _);
        DataObject.AddPastingHandler(_lineNumberBox, (s, e) =>
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string))!;
                if (!int.TryParse(text, out _)) e.CancelCommand();
            }
            else e.CancelCommand();
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

        // Count lines from text - split by \r (paragraph separator in RichTextBox)
        var text = GetEditorText();
        var lines = text.Split('\r');
        if (lines.Length > 1 && string.IsNullOrEmpty(lines[lines.Length - 1]))
            lines = lines[..^1];

        int totalLines = lines.Length;
        if (totalLines == 0)
            totalLines = 1;

        if (lineNumber > totalLines)
        {
            _statusText.Text = $"Max line is {totalLines}";
            return;
        }

        // Use offset-based approach to find the line, then move to paragraph start for column 1
        int offset = 0;
        for (int i = 0; i < lineNumber - 1; i++)
        {
            offset += lines[i].Length + 1; // +1 for \r
        }

        var docPosition = _editor.Document.ContentStart.GetPositionAtOffset(offset);
        if (docPosition != null)
        {
            // Move to the start of the paragraph (column 1)
            var para = docPosition.Paragraph;
            if (para != null)
            {
                docPosition = para.ContentStart;
            }

            _editor.CaretPosition = docPosition;
            _editor.Focus();
            _editor.UpdateLayout();

            // Scroll so the target line is near the top (20px padding)
            var rect = docPosition.GetCharacterRect(LogicalDirection.Forward);
            _editor.ScrollToVerticalOffset(Math.Max(0, _editor.VerticalOffset + rect.Top - 20));

            _statusText.Text = $"Navigated to line {lineNumber}";

            // Notify caller to sync preview
            _onNavigated?.Invoke();
        }
        else
        {
            _statusText.Text = "Could not navigate to line";
        }
    }

    private string GetEditorText()
    {
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        return range.Text;
    }
}