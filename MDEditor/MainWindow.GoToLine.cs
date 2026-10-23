using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MDEditor.Editor;

namespace MDEditor;

/// <summary>
/// Inline "Go to line" bar above the editor (shares the find bar's slot).
/// Accepts "line" or "line:column", previews the target while typing,
/// Enter jumps there and Esc restores the original view.
/// Lines are paragraphs, the same as the line-number gutter.
/// </summary>
public partial class MainWindow
{
    private static readonly Regex GoToLinePattern = new(@"^\s*(\d+)\s*(?:[:,]\s*(\d+))?\s*$", RegexOptions.Compiled);

    private double _goToLineOriginalVertical;
    private double _goToLineOriginalHorizontal;

    private void ShowGoToLine()
    {
        if (EditorFindBar.Visibility == Visibility.Visible)
            CloseEditorFind();

        GoToLineOverlay.Editor ??= MarkdownEditor;
        GoToLineOverlay.SetLine(null);

        _goToLineOriginalVertical = MarkdownEditor.VerticalOffset;
        _goToLineOriginalHorizontal = MarkdownEditor.HorizontalOffset;

        GoToLineBar.Visibility = Visibility.Visible;
        GoToLineBox.Text = string.Empty;
        ShowGoToLineHint();

        GoToLineBox.Focus();
    }

    private void CloseGoToLine(bool restoreView)
    {
        if (GoToLineBar.Visibility != Visibility.Visible)
            return;

        GoToLineBar.Visibility = Visibility.Collapsed;
        MarkdownEditor.Focus();

        if (restoreView)
        {
            GoToLineOverlay.SetLine(null);

            // Focusing the editor brings the caret into view; restore the original view after that.
            double vertical = _goToLineOriginalVertical, horizontal = _goToLineOriginalHorizontal;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MarkdownEditor.ScrollToVerticalOffset(vertical);
                MarkdownEditor.ScrollToHorizontalOffset(horizontal);
            }), DispatcherPriority.Loaded);
        }
    }

    private string GetGoToLineText() =>
        _cachedEditorText ??= EditorText.GetText(MarkdownEditor.Document);

    /// <summary>Number of lines: EditorText ends every paragraph with '\n'.</summary>
    private static int CountLines(string text)
    {
        int count = 0;
        foreach (var c in text)
            if (c == '\n') count++;
        return Math.Max(1, count);
    }

    private void ShowGoToLineHint()
    {
        var text = GetGoToLineText();
        int caret = Math.Min(EditorText.GetOffset(MarkdownEditor.Document, MarkdownEditor.CaretPosition), text.Length);

        int line = 1;
        for (int i = 0; i < caret; i++)
            if (text[i] == '\n') line++;

        int lineStart = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
        int column = caret - lineStart + 1;

        GoToLineStatus.Text = $"Ln {line}, Col {column} · type 1–{CountLines(text)}";
        GoToLineStatus.Foreground = Brushes.Gray;
    }

    /// <summary>
    /// Resolves the box input to a character offset, or null with an error shown.
    /// The column is clamped to the line length.
    /// </summary>
    private int? ResolveGoToLineOffset(out int line)
    {
        line = 0;
        var input = GoToLineBox.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            ShowGoToLineHint();
            return null;
        }

        var text = GetGoToLineText();
        int total = CountLines(text);
        var match = GoToLinePattern.Match(input);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out line) || line < 1 || line > total)
        {
            GoToLineStatus.Text = $"Enter a line from 1 to {total}";
            GoToLineStatus.Foreground = Brushes.IndianRed;
            return null;
        }

        int column = 1;
        if (match.Groups[2].Success && (!int.TryParse(match.Groups[2].Value, out column) || column < 1))
            column = 1;

        // Skip line - 1 line breaks to reach the start of the target line.
        int lineStart = 0;
        for (int i = 1; i < line; i++)
            lineStart = text.IndexOf('\n', lineStart) + 1;

        int lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = text.Length;

        GoToLineStatus.Text = match.Groups[2].Success ? $"Go to line {line}, column {column}" : $"Go to line {line} of {total}";
        GoToLineStatus.Foreground = Brushes.Gray;
        return lineStart + Math.Min(column - 1, lineEnd - lineStart);
    }

    private void GoToLineBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (GoToLineBar.Visibility != Visibility.Visible)
            return;

        // Preview: highlight the target line and bring it into view without moving the caret.
        if (ResolveGoToLineOffset(out _) is int offset &&
            EditorText.GetPointer(MarkdownEditor.Document, offset) is { } pointer)
        {
            GoToLineOverlay.SetLine(pointer.Paragraph);
            if (!IsEditorPositionVisible(pointer))
                ScrollEditorToMiddle(pointer);
        }
        else
        {
            GoToLineOverlay.SetLine(null);
        }
    }

    private void GoToLineBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                CommitGoToLine();
                break;
            case Key.Escape:
                e.Handled = true;
                CloseGoToLine(restoreView: true);
                break;
        }
    }

    private void CommitGoToLine()
    {
        if (ResolveGoToLineOffset(out _) is not int offset ||
            EditorText.GetPointer(MarkdownEditor.Document, offset) is not { } pointer)
            return;

        MarkdownEditor.Selection.Select(pointer, pointer);
        CloseGoToLine(restoreView: false);

        // Keep the landing line highlighted briefly so it is easy to spot, then fade it out.
        GoToLineOverlay.SetLine(pointer.Paragraph);
        GoToLineOverlay.FadeOut(TimeSpan.FromMilliseconds(1200));

        // Focusing the editor scrolls the caret to the viewport edge; center it once layout settles.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsEditorPositionVisible(pointer))
                ScrollEditorToMiddle(pointer);
            SyncPreviewToCaret();
        }), DispatcherPriority.Loaded);
    }

    private void GoToLineBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Clicking elsewhere dismisses the bar and keeps the current view.
        if (e.NewFocus is DependencyObject target && GoToLineBar.IsAncestorOf(target))
            return;

        if (GoToLineBar.Visibility != Visibility.Visible)
            return;

        GoToLineBar.Visibility = Visibility.Collapsed;
        GoToLineOverlay.SetLine(null);
    }

    private void GoToLineClose_Click(object sender, RoutedEventArgs e) => CloseGoToLine(restoreView: true);
}
