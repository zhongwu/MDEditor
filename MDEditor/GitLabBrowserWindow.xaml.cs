using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
/// Modal "Open from GitLab" dialog: pick a repository, branch and file. On success the file is
/// already downloaded and available as <see cref="Result"/>.
/// </summary>
public partial class GitLabBrowserWindow : Window
{
    private const string LastRepoKey = "gitlab.lastProject";

    private readonly IGitLabRepositoryService _repoService;
    private readonly ISettingsService _settingsService;

    private IReadOnlyList<GitLabProject> _repos = Array.Empty<GitLabProject>();
    private GitHubTree _tree = new();

    // Bumped whenever the repo/branch selection changes, so results of a slower, superseded
    // request are dropped instead of overwriting the newer selection.
    private int _loadVersion;
    private bool _suppressBranchChange;

    public GitLabFileContent? Result { get; private set; }

    public GitLabBrowserWindow(IGitLabRepositoryService repoService, ISettingsService settingsService, string accountName)
    {
        InitializeComponent();
        _repoService = repoService;
        _settingsService = settingsService;
        Title = Loc.Format("GL_OpenTitleAccount", accountName);

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += async (s, e) =>
        {
            RepoFilterBox.Focus();
            await LoadRepositoriesAsync();
        };
    }

    private GitLabProject? SelectedRepo => (RepoList.SelectedItem as ListBoxItem)?.Tag as GitLabProject;
    private string? SelectedBranch => BranchCombo.SelectedItem as string;
    private string? SelectedFilePath => (FileTree.SelectedItem as TreeViewItem)?.Tag as string;

    private async Task LoadRepositoriesAsync()
    {
        SetStatus(Loc.Get("GH_LoadingRepos"));
        RefreshButton.IsEnabled = false;
        try
        {
            _repos = await _repoService.GetProjectsAsync();
            PopulateRepoList();
            SetStatus(Loc.Format("GH_RepoCount", _repos.Count));

            // Reselect the repository used last time, which is usually where the next file is.
            var last = _settingsService.Get<string?>(LastRepoKey, null);
            var lastItem = RepoList.Items.OfType<ListBoxItem>()
                .FirstOrDefault(i => string.Equals((i.Tag as GitLabProject)?.PathWithNamespace, last, StringComparison.OrdinalIgnoreCase));
            if (lastItem != null)
            {
                RepoList.SelectedItem = lastItem;
                RepoList.ScrollIntoView(lastItem);
            }
        }
        catch (Exception ex)
        {
            SetStatus(Loc.Format("GH_LoadReposFailed", ex.Message), isError: true);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void PopulateRepoList()
    {
        var selected = SelectedRepo?.PathWithNamespace;
        var filter = RepoFilterBox.Text.Trim();

        RepoList.Items.Clear();
        foreach (var repo in _repos)
        {
            if (filter.Length > 0 && !repo.PathWithNamespace.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var item = new ListBoxItem
            {
                Content = BuildRepoHeader(repo),
                Tag = repo,
                ToolTip = repo.Description,
            };
            RepoList.Items.Add(item);
            if (repo.PathWithNamespace == selected)
                RepoList.SelectedItem = item;
        }
    }

    private static object BuildRepoHeader(GitLabProject repo)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Text = repo.IsPrivate ? "" : "", // lock / folder
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Opacity = 0.8,
        });
        var slash = repo.PathWithNamespace.LastIndexOf('/');
        if (slash >= 0)
            panel.Children.Add(new TextBlock { Text = repo.PathWithNamespace[..(slash + 1)], Opacity = 0.7 });
        panel.Children.Add(new TextBlock { Text = slash >= 0 ? repo.PathWithNamespace[(slash + 1)..] : repo.PathWithNamespace });
        if (!repo.CanPush)
            panel.Children.Add(new TextBlock { Text = Loc.Get("GH_ReadOnly"), Opacity = 0.6 });
        return panel;
    }

    private void RepoFilterBox_TextChanged(object sender, TextChangedEventArgs e) => PopulateRepoList();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadRepositoriesAsync();

    private async void RepoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var repo = SelectedRepo;
        if (repo == null)
            return;

        // Filtering rebuilds the list and reselects the same repo; don't reload it.
        if (e.RemovedItems.OfType<ListBoxItem>().FirstOrDefault()?.Tag is GitLabProject previous && previous.Id == repo.Id)
            return;

        int version = ++_loadVersion;
        ClearTree(Loc.Get("GH_LoadingBranches"));
        _suppressBranchChange = true;
        BranchCombo.Items.Clear();
        _suppressBranchChange = false;

        try
        {
            var branches = await _repoService.GetBranchesAsync(repo.Id);
            if (version != _loadVersion) return;

            _suppressBranchChange = true;
            foreach (var branch in branches.OrderBy(b => b != repo.DefaultBranch).ThenBy(b => b, StringComparer.OrdinalIgnoreCase))
                BranchCombo.Items.Add(branch);
            _suppressBranchChange = false;

            if (BranchCombo.Items.Count == 0)
            {
                ClearTree(Loc.Get("GH_RepoEmpty"));
                return;
            }

            BranchCombo.SelectedIndex = 0; // default branch sorts first
            await LoadTreeAsync();
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
                ClearTree(Loc.Format("GH_LoadBranchesFailed", ex.Message));
        }
    }

    private async void BranchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBranchChange || SelectedBranch == null)
            return;

        ++_loadVersion;
        await LoadTreeAsync();
    }

    private async Task LoadTreeAsync()
    {
        var repo = SelectedRepo;
        var branch = SelectedBranch;
        if (repo == null || branch == null)
            return;

        int version = _loadVersion;
        ClearTree(Loc.Get("GH_LoadingFiles"));
        try
        {
            var tree = await _repoService.GetTreeAsync(repo.Id, branch);
            if (version != _loadVersion) return;

            _tree = tree;
            PopulateTree();
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
                ClearTree(Loc.Format("GH_LoadFilesFailed", ex.Message));
        }
    }

    private void MarkdownOnlyCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && _tree.Entries.Count > 0)
            PopulateTree();
    }

    private void PopulateTree()
    {
        FileTree.Items.Clear();
        var nodes = GitHubTreeBuilder.Build(_tree.Entries, MarkdownOnlyCheck.IsChecked == true);
        foreach (var node in nodes)
            FileTree.Items.Add(BuildTreeItem(node));

        // Small trees are easier to scan fully expanded.
        int fileCount = _tree.Entries.Count(entry => !entry.IsFolder);
        if (fileCount <= 40)
            foreach (TreeViewItem item in FileTree.Items)
                ExpandAll(item);

        if (FileTree.Items.Count == 0)
            ShowTreePlaceholder(MarkdownOnlyCheck.IsChecked == true ? Loc.Get("GH_NoMarkdownFiles") : Loc.Get("GH_NoFiles"));
        else
            TreePlaceholder.Visibility = Visibility.Collapsed;

        SetStatus(_tree.Truncated
            ? Loc.Get("GL_Truncated")
            : string.Empty, isError: _tree.Truncated);
        UpdateOpenButton();
    }

    private static TreeViewItem BuildTreeItem(GitHubTreeNode node)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Text = node.IsFolder ? "" : "", // folder / document
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Opacity = 0.8,
        });
        header.Children.Add(new TextBlock { Text = node.Name });

        // Folders carry no Tag, which is how selection tells files from folders.
        var item = new TreeViewItem { Header = header, Tag = node.IsFolder ? null : node.Path };
        foreach (var child in node.Children)
            item.Items.Add(BuildTreeItem(child));
        return item;
    }

    private static void ExpandAll(TreeViewItem item)
    {
        item.IsExpanded = true;
        foreach (TreeViewItem child in item.Items)
            ExpandAll(child);
    }

    private void ClearTree(string placeholder)
    {
        _tree = new GitHubTree();
        FileTree.Items.Clear();
        ShowTreePlaceholder(placeholder);
        UpdateOpenButton();
    }

    private void ShowTreePlaceholder(string text)
    {
        TreePlaceholder.Text = text;
        TreePlaceholder.Visibility = Visibility.Visible;
    }

    private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => UpdateOpenButton();

    private void UpdateOpenButton() => OpenButton.IsEnabled = SelectedFilePath != null;

    private async void FileTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Double-clicking a folder selects it (SelectedFilePath null) and toggles it as usual;
        // only files open.
        if (SelectedFilePath != null)
        {
            e.Handled = true;
            await OpenSelectedAsync();
        }
    }

    private async void FileTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SelectedFilePath != null)
        {
            e.Handled = true;
            await OpenSelectedAsync();
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenSelectedAsync();

    private async Task OpenSelectedAsync()
    {
        var repo = SelectedRepo;
        var branch = SelectedBranch;
        var path = SelectedFilePath;
        if (repo == null || branch == null || path == null || !OpenButton.IsEnabled)
            return;

        OpenButton.IsEnabled = false;
        SetStatus(Loc.Format("GH_Downloading", path));
        try
        {
            Result = await _repoService.GetFileAsync(repo, branch, path);
            _settingsService.Set(LastRepoKey, repo.PathWithNamespace);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetStatus(Loc.Format("GH_OpenFileFailed", path, ex.Message), isError: true);
            UpdateOpenButton();
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.IndianRed : (Brush)FindResource("WindowForegroundBrush");
    }
}
