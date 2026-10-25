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
/// Modal history browser for a GitHub document: lists the commits that touched the file and
/// diffs a selected version against its parent or against the editor's current text. "Restore"
/// hands the selected version back via <see cref="RestoredText"/>.
/// </summary>
public partial class GitHubHistoryWindow : Window
{
    private static readonly Brush AddedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x2E, 0xA0, 0x43)));
    private static readonly Brush RemovedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xF8, 0x51, 0x49)));
    private static readonly Brush SkippedBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x18, 0x80, 0x80, 0x80)));

    private readonly IGitHubRepositoryService _repoService;
    private readonly GitHubDocument _document;
    private readonly string _currentText;

    // File text per commit SHA (null = file absent at that commit). Shared by both compare modes.
    private readonly Dictionary<string, string?> _contentCache = new();
    private int _loadVersion;

    /// <summary>Text of the version chosen with "Restore", when DialogResult is true.</summary>
    public string? RestoredText { get; private set; }

    /// <summary>The commit whose version was restored.</summary>
    public GitHubCommitInfo? RestoredCommit { get; private set; }

    public GitHubHistoryWindow(IGitHubRepositoryService repoService, GitHubDocument document, string currentText)
    {
        InitializeComponent();
        _repoService = repoService;
        _document = document;
        _currentText = currentText;

        Title = Loc.Format("GH_HistoryTitleFile", document.FileName);
        HeaderText.Text = $"{document.RepoFullName}@{document.Branch}";
        HeaderText.ToolTip = document.DisplayName;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += async (s, e) => await LoadHistoryAsync();
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private GitHubCommitInfo? SelectedCommit => (CommitList.SelectedItem as ListBoxItem)?.Tag as GitHubCommitInfo;

    private async Task LoadHistoryAsync()
    {
        SetStatus(Loc.Get("GH_LoadingHistory"));
        try
        {
            var commits = await _repoService.GetFileHistoryAsync(_document);
            foreach (var commit in commits)
                CommitList.Items.Add(new ListBoxItem { Content = BuildCommitHeader(commit), Tag = commit, ToolTip = commit.Message });

            if (commits.Count == 0)
            {
                ShowPlaceholder(Loc.Get("GH_NoCommits"));
                SetStatus(string.Empty);
                return;
            }

            SetStatus(commits.Count >= 100 ? Loc.Get("GH_Showing100") : Loc.Format("GH_CommitCount", commits.Count));
            CommitList.SelectedIndex = 0;
            CommitList.Focus();
        }
        catch (Exception ex)
        {
            ShowPlaceholder(Loc.Format("GH_LoadHistoryFailed", ex.Message));
            SetStatus(string.Empty);
        }
    }

    private static object BuildCommitHeader(GitHubCommitInfo commit)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 3, 2, 3) };
        panel.Children.Add(new TextBlock { Text = commit.Summary, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        panel.Children.Add(new TextBlock
        {
            Text = $"{commit.AuthorLogin ?? commit.AuthorName}  ·  {commit.Date.LocalDateTime.ToString("g", Loc.Culture)}  ·  {commit.ShortSha}",
            Opacity = 0.7,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return panel;
    }

    private async void CommitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasCommit = SelectedCommit != null;
        ViewOnGitHubButton.IsEnabled = hasCommit;
        RestoreButton.IsEnabled = false; // until the version's text is loaded
        await ShowDiffAsync();
    }

    private async void CompareMode_Changed(object sender, RoutedEventArgs e)
    {
        // CommitChangesRadio's IsChecked="True" raises Checked during InitializeComponent.
        if (!IsLoaded)
            return;
        await ShowDiffAsync();
    }

    private async Task ShowDiffAsync()
    {
        var commit = SelectedCommit;
        if (commit == null)
            return;

        int version = ++_loadVersion;
        ShowPlaceholder(Loc.Get("Common_Loading"));
        StatsText.Text = string.Empty;
        try
        {
            bool vsCurrent = VsCurrentRadio.IsChecked == true;
            var selectedText = await GetContentAsync(commit.Sha);
            string? oldText;
            string? newText;
            if (vsCurrent)
            {
                oldText = selectedText;
                newText = _currentText;
            }
            else
            {
                oldText = commit.ParentSha == null ? null : await GetContentAsync(commit.ParentSha);
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
            text = await _repoService.GetFileAtCommitAsync(_document, sha);
            _contentCache[sha] = text;
        }
        return text;
    }

    private void RenderDiff(string oldText, string newText, bool vsCurrent, bool missingAtCommit)
    {
        var lines = LineDiff.Compute(oldText, newText);
        var (added, removed) = LineDiff.Stats(lines);
        StatsText.Text = $"+{added}  -{removed}";
        StatsText.ToolTip = vsCurrent
            ? Loc.Get("GH_StatsTipVsCurrent")
            : Loc.Get("GH_StatsTipCommit");

        if (missingAtCommit)
        {
            DiffList.ItemsSource = null;
            ShowPlaceholder(Loc.Get("GH_FileMissingAtCommit"));
            return;
        }

        if (added == 0 && removed == 0)
        {
            DiffList.ItemsSource = null;
            ShowPlaceholder(vsCurrent ? Loc.Get("GH_IdenticalToCurrent") : Loc.Get("GH_CommitNoChange"));
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
        var commit = SelectedCommit;
        if (commit == null || !_contentCache.TryGetValue(commit.Sha, out var text) || text == null)
            return;

        RestoredText = text;
        RestoredCommit = commit;
        DialogResult = true;
    }

    private void ViewOnGitHub_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCommit is not { } commit)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(commit.HtmlUrl) { UseShellExecute = true });
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
