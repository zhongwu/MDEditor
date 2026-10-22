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
        Width = 300;
        Height = 150;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid { Margin = new Thickness(10) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock { Text = "Line number:", Margin = new Thickness(0, 0, 0, 5) };
        grid.Children.Add(label);
        Grid.SetRow(label, 0);

        _lineNumberBox = new TextBox { Width = 200, Margin = new Thickness(0, 0, 0, 5) };
        grid.Children.Add(_lineNumberBox);
        Grid.SetRow(_lineNumberBox, 1);

        var button = new Button { Content = "Go", Width = 80 };
        button.Click += Go_Click;
        grid.Children.Add(button);
        Grid.SetRow(button, 1);

        _statusText = new TextBlock { Text = "", Margin = new Thickness(0, 5, 0, 0) };
        grid.Children.Add(_statusText);
        Grid.SetRow(_statusText, 2);

        Content = grid;
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_lineNumberBox.Text, out int lineNumber) || lineNumber < 1)
        {
            _statusText.Text = "Invalid line number";
            return;
        }

        var text = GetEditorText();
        var lines = text.Split('\n');

        if (lineNumber > lines.Length)
        {
            _statusText.Text = $"Max line is {lines.Length}";
            return;
        }

        // Calculate character offset to the start of the target line
        int offset = 0;
        for (int i = 0; i < lineNumber - 1; i++)
        {
            offset += lines[i].Length + 1; // +1 for newline
        }

        var doc = _editor.Document;
        var position = doc.ContentStart.GetPositionAtOffset(offset);
        _editor.CaretPosition = position;
        _editor.Focus();

        _statusText.Text = $"Navigated to line {lineNumber}";
    }

    private string GetEditorText()
    {
        var range = new TextRange(_editor.Document.ContentStart, _editor.Document.ContentEnd);
        return range.Text;
    }
}