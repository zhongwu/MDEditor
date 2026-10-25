using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Modal "Save as Gist" dialog: file name, description and visibility. The gist is created
/// inside the dialog so a failure (e.g. a token without the gist scope) can be retried.
/// </summary>
public partial class GistCreateDialog : Window
{
    private readonly IGitHubGistService _gistService;
    private readonly string _text;
    private bool _busy;

    /// <summary>The created gist, when DialogResult is true.</summary>
    public GitHubGistDocument? Result { get; private set; }

    public GistCreateDialog(IGitHubGistService gistService, string text, string suggestedFileName, string? suggestedDescription)
    {
        InitializeComponent();
        _gistService = gistService;
        _text = text;

        FileNameBox.Text = suggestedFileName;
        DescriptionBox.Text = suggestedDescription ?? string.Empty;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += (s, e) =>
        {
            // Select the name without its extension, ready to be typed over.
            FileNameBox.Focus();
            var dot = FileNameBox.Text.LastIndexOf('.');
            FileNameBox.Select(0, dot > 0 ? dot : FileNameBox.Text.Length);
        };
        Closing += (s, e) => e.Cancel = _busy;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (CreateButton != null)
            CreateButton.IsEnabled = !_busy && IsValidFileName(FileNameBox.Text);
    }

    private static bool IsValidFileName(string name) =>
        name.Trim().Length > 0 && name.IndexOfAny(new[] { '/', '\\' }) < 0;

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (!IsValidFileName(FileNameBox.Text))
            return;

        SetBusy(true);
        ShowStatus(Loc.Get("Gist_Creating"), isError: false);
        try
        {
            Result = await _gistService.CreateAsync(FileNameBox.Text, _text, DescriptionBox.Text, PublicRadio.IsChecked == true);
            SetBusy(false);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetBusy(false);
            ShowStatus(ex.Message, isError: true);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CreateButton.IsEnabled = !busy && IsValidFileName(FileNameBox.Text);
        CancelButton.IsEnabled = FileNameBox.IsEnabled = DescriptionBox.IsEnabled = SecretRadio.IsEnabled = PublicRadio.IsEnabled = !busy;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.IndianRed : (Brush)FindResource("WindowForegroundBrush");
        StatusText.Visibility = Visibility.Visible;
    }
}
