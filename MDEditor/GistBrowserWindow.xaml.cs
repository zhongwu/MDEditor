using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Modal "Open Gist" dialog: the account's gists, or any gist by URL/ID, then one of its files.
/// On success the file is already downloaded and available as <see cref="Result"/>.
/// </summary>
public partial class GistBrowserWindow : Window
{
    private readonly IGitHubGistService _gistService;
    private IReadOnlyList<GitHubGistInfo> _gists = Array.Empty<GitHubGistInfo>();
    private GitHubGistInfo? _linkedGist; // opened by URL; listed first

    public GitHubGistFileContent? Result { get; private set; }

    public GistBrowserWindow(IGitHubGistService gistService)
    {
        InitializeComponent();
        _gistService = gistService;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += async (s, e) =>
        {
            // Pre-fill a gist link from the clipboard - the usual way people arrive here.
            try
            {
                var clip = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;
                if (clip.Contains("gist.github", StringComparison.OrdinalIgnoreCase) && GitHubGistService.ParseGistId(clip) != null)
                    UrlBox.Text = clip;
            }
            catch
            {
                // Clipboard may be locked by another process; pre-filling is only a convenience.
            }

            await LoadGistsAsync();
        };
    }

    private GitHubGistInfo? SelectedGist => (GistList.SelectedItem as ListBoxItem)?.Tag as GitHubGistInfo;
    private string? SelectedFile => (FileList.SelectedItem as ListBoxItem)?.Tag as string;

    private async Task LoadGistsAsync()
    {
        SetStatus(Loc.Get("Gist_Loading"));
        RefreshButton.IsEnabled = false;
        try
        {
            _gists = await _gistService.GetMyGistsAsync();
            PopulateGistList();
            SetStatus(Loc.Format("Gist_Count", _gists.Count));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.Format("Gist_LoadFailed", ex.Message), isError: true);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void PopulateGistList()
    {
        var selectedId = SelectedGist?.Id;
        var filter = FilterBox.Text.Trim();

        GistList.Items.Clear();
        var all = _linkedGist != null ? new[] { _linkedGist }.Concat(_gists.Where(g => g.Id != _linkedGist.Id)) : _gists;
        foreach (var gist in all)
        {
            if (filter.Length > 0
                && !gist.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !gist.FileNames.Any(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                continue;

            var item = new ListBoxItem { Content = BuildGistHeader(gist), Tag = gist, ToolTip = gist.HtmlUrl };
            GistList.Items.Add(item);
            if (gist.Id == selectedId)
                GistList.SelectedItem = item;
        }

        GistPlaceholder.Text = Loc.Get(filter.Length > 0 ? "Gist_NoMatches" : "Gist_None");
        GistPlaceholder.Visibility = GistList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private object BuildGistHeader(GitHubGistInfo gist)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Text = gist.IsPublic ? "" : "", // globe / lock
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Opacity = 0.8,
            ToolTip = Loc.Get(gist.IsPublic ? "Gist_Public" : "Gist_Secret"),
        });
        title.Children.Add(new TextBlock { Text = gist.Title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        panel.Children.Add(title);

        var owner = gist == _linkedGist && gist.Owner.Length > 0 ? gist.Owner + "  ·  " : string.Empty;
        panel.Children.Add(new TextBlock
        {
            Text = $"{owner}{(gist.FileNames.Count == 1 ? Loc.Get("Gist_FileCountOne") : Loc.Format("Gist_FileCount", gist.FileNames.Count))}  ·  {gist.UpdatedAt.LocalDateTime.ToString("g", Loc.Culture)}",
            Opacity = 0.7,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return panel;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => PopulateGistList();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadGistsAsync();

    private void GistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FileList.Items.Clear();
        if (SelectedGist is not { } gist)
        {
            UpdateOpenButton();
            return;
        }

        // Markdown first: that's what this editor is for, but any text file can be opened.
        foreach (var file in gist.FileNames.OrderBy(f => !GitHubRepositoryService.IsMarkdownPath(f)).ThenBy(f => f, StringComparer.OrdinalIgnoreCase))
            FileList.Items.Add(new ListBoxItem { Content = file, Tag = file });

        if (FileList.Items.Count > 0)
            FileList.SelectedIndex = 0;
        UpdateOpenButton();
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOpenButton();

    private void UpdateOpenButton() => OpenButton.IsEnabled = SelectedGist != null && SelectedFile != null;

    private async void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedFile != null)
            await OpenSelectedAsync();
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenSelectedAsync();

    private void UrlBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true; // don't trigger the default (Open) button
            Go_Click(sender, e);
        }
    }

    private async void Go_Click(object sender, RoutedEventArgs e)
    {
        var id = GitHubGistService.ParseGistId(UrlBox.Text);
        if (id == null)
        {
            SetStatus(Loc.Get("Gist_BadLink"), isError: true);
            return;
        }

        GoButton.IsEnabled = false;
        SetStatus(Loc.Get("Gist_LoadingOne"));
        try
        {
            _linkedGist = await _gistService.GetGistAsync(id);
            FilterBox.Text = string.Empty;
            PopulateGistList();
            GistList.SelectedIndex = 0;
            SetStatus(string.Empty);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            GoButton.IsEnabled = true;
        }
    }

    private async Task OpenSelectedAsync()
    {
        if (SelectedGist is not { } gist || SelectedFile is not { } file || !OpenButton.IsEnabled)
            return;

        OpenButton.IsEnabled = false;
        SetStatus(Loc.Format("GH_Downloading", file));
        try
        {
            Result = await _gistService.GetFileAsync(gist.Id, file);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetStatus(Loc.Format("GH_OpenFileFailed", file, ex.Message), isError: true);
            UpdateOpenButton();
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.IndianRed : (Brush)FindResource("WindowForegroundBrush");
    }
}
