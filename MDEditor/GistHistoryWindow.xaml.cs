using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Modal history browser for a gist document: lists its revisions and diffs a selected one
/// against the previous revision or against the editor's current text. "Restore" hands the
/// selected version back via <see cref="RestoredText"/>. Mirrors <see cref="GitHubHistoryWindow"/>;
/// gists have no commit messages or branches, just a flat, linear list of revisions.
/// </summary>
public partial class GistHistoryWindow : Window
{
    private static readonly Brush AddedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x2E, 0xA0, 0x43)));
    private static readonly Brush RemovedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xF8, 0x51, 0x49)));
    private static readonly Brush SkippedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x18, 0x80, 0x80, 0x80)));

    private readonly IGitHubGistService _gistService;
    private readonly GitHubGistDocument _document;
    private readonly string _currentText;

    // File text per revision SHA (null = file absent at that revision). Shared by both compare modes.
    private readonly Dictionary<string, string?> _contentCache = new();
    private int _loadVersion;

    /// <summary>Text of the version chosen with "Restore", when DialogResult is true.</summary>
    public string? RestoredText { get; private set; }

    /// <summary>The revision whose version was restored.</summary>
    public GitHubGistRevisionInfo? RestoredRevision { get; private set; }

    public GistHistoryWindow(IGitHubGistService gistService, GitHubGistDocument document, string currentText)
    {
        InitializeComponent();
        _gistService = gistService;
        _document = document;
        _currentText = currentText;

        Title = Loc.Format("GH_HistoryTitleFile", document.FileName);
        HeaderText.Text = document.DisplayName;
        HeaderText.ToolTip = document.FileName;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += async (s, e) => await LoadHistoryAsync();
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private GitHubGistRevisionInfo? SelectedRevision => (RevisionList.SelectedItem as ListBoxItem)?.Tag as GitHubGistRevisionInfo;

    private async Task LoadHistoryAsync()
    {
        SetStatus(Loc.Get("GH_LoadingHistory"));
        try
        {
            var revisions = await _gistService.GetHistoryAsync(_document);
            foreach (var revision in revisions)
                RevisionList.Items.Add(new ListBoxItem { Content = BuildRevisionHeader(revision), Tag = revision });

            if (revisions.Count == 0)
            {
                ShowPlaceholder(Loc.Get("Gist_NoRevisions"));
                SetStatus(string.Empty);
                return;
            }

            SetStatus(Loc.Format("Gist_RevisionCount", revisions.Count));
            RevisionList.SelectedIndex = 0;
            RevisionList.Focus();
        }
        catch (Exception ex)
        {
            ShowPlaceholder(Loc.Format("GH_LoadHistoryFailed", ex.Message));
            SetStatus(string.Empty);
        }
    }

    private static object BuildRevisionHeader(GitHubGistRevisionInfo revision)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
        panel.Children.Add(new TextBlock
        {
            Text = revision.AuthorLogin ?? Loc.Get("Gist_UnknownAuthor"),
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{revision.CommittedAt.LocalDateTime.ToString("g", Loc.Culture)}  ·  +{revision.Additions} -{revision.Deletions}  ·  {revision.ShortSha}",
            Opacity = 0.7,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return panel;
    }

    private async void RevisionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasRevision = SelectedRevision != null;
        ViewOnGitHubButton.IsEnabled = hasRevision;
        RestoreButton.IsEnabled = false; // until the version's text is loaded
        await ShowDiffAsync();
    }

    private async void CompareMode_Changed(object sender, RoutedEventArgs e)
    {
        // RevisionChangesRadio's IsChecked="True" raises Checked during InitializeComponent.
        if (!IsLoaded)
            return;
        await ShowDiffAsync();
    }

    private async Task ShowDiffAsync()
    {
        var revision = SelectedRevision;
        if (revision == null)
            return;

        int version = ++_loadVersion;
        ShowPlaceholder(Loc.Get("Common_Loading"));
        StatsText.Text = string.Empty;
        try
        {
            bool vsCurrent = VsCurrentRadio.IsChecked == true;
            var selectedText = await GetContentAsync(revision.Sha);
            string? oldText;
            string? newText;
            if (vsCurrent)
            {
                oldText = selectedText;
                newText = _currentText;
            }
            else
            {
                oldText = revision.PreviousSha == null ? null : await GetContentAsync(revision.PreviousSha);
                newText = selectedText;
            }

            if (version != _loadVersion)
                return;

            RestoreButton.IsEnabled = selectedText != null;
            RenderDiff(oldText ?? string.Empty, newText ?? string.Empty, vsCurrent, selectedText == null);
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
                ShowPlaceholder(Loc.Format("GH_LoadVersionFailed", ex.Message));
        }
    }

    private async Task<string?> GetContentAsync(string sha)
    {
        if (!_contentCache.TryGetValue(sha, out var text))
        {
            text = await _gistService.GetFileAtRevisionAsync(_document, sha);
            _contentCache[sha] = text;
        }
        return text;
    }

    private void RenderDiff(string oldText, string newText, bool vsCurrent, bool missingAtRevision)
    {
        var lines = LineDiff.Compute(oldText, newText);
        var (added, removed) = LineDiff.Stats(lines);
        StatsText.Text = $"+{added}  -{removed}";
        StatsText.ToolTip = vsCurrent
            ? Loc.Get("GH_StatsTipVsCurrent")
            : Loc.Get("Gist_StatsTipRevision");

        if (missingAtRevision)
        {
            DiffList.ItemsSource = null;
            ShowPlaceholder(Loc.Get("Gist_FileMissingAtRevision"));
            return;
        }

        if (added == 0 && removed == 0)
        {
            DiffList.ItemsSource = null;
            ShowPlaceholder(vsCurrent ? Loc.Get("GH_IdenticalToCurrent") : Loc.Get("Gist_RevisionNoChange"));
            return;
        }

        var shown = FullFileCheck.IsChecked == true ? lines : LineDiff.Collapse(lines);
        DiffList.ItemsSource = shown.Select(DiffRow.From).ToList();
        DiffList.ScrollIntoView(DiffList.Items[0]);
        DiffPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ShowPlaceholder(string text)
    {
        DiffPlaceholder.Text = text;
        DiffPlaceholder.Visibility = Visibility.Visible;
        if (text != Loc.Get("Common_Loading"))
            DiffList.ItemsSource = null;
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var revision = SelectedRevision;
        if (revision == null || !_contentCache.TryGetValue(revision.Sha, out var text) || text == null)
            return;

        RestoredText = text;
        RestoredRevision = revision;
        DialogResult = true;
    }

    private void ViewOnGitHub_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRevision is not { } revision)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(revision.HtmlUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.Format("Common_BrowserOpenFailed", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>View model for one diff row.</summary>
    internal sealed class DiffRow
    {
        public string OldNumber { get; init; } = string.Empty;
        public string NewNumber { get; init; } = string.Empty;
        public string Marker { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public Brush Background { get; init; } = Brushes.Transparent;
        public double Opacity { get; init; } = 1.0;
        public FontStyle FontStyle { get; init; } = FontStyles.Normal;

        public static DiffRow From(DiffLine line) => line.Kind switch
        {
            DiffLineKind.Added => new DiffRow { NewNumber = Num(line.NewLineNumber), Marker = "+", Text = line.Text, Background = AddedBrush },
            DiffLineKind.Removed => new DiffRow { OldNumber = Num(line.OldLineNumber), Marker = "-", Text = line.Text, Background = RemovedBrush },
            DiffLineKind.Skipped => new DiffRow
            {
                Text = line.SkippedCount == 1 ? Loc.Get("GH_UnchangedLine") : Loc.Format("GH_UnchangedLines", line.SkippedCount),
                Background = SkippedBrush,
                Opacity = 0.7,
                FontStyle = FontStyles.Italic,
            },
            _ => new DiffRow { OldNumber = Num(line.OldLineNumber), NewNumber = Num(line.NewLineNumber), Text = line.Text },
        };

        private static string Num(int? n) => n?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
