using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using MDEditor.Core.Services;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Modal dialog that commits the editor's text to a GitHub document - directly to its branch or
/// to a new branch (optionally opening a pull request). The commit runs inside the dialog so
/// errors can be fixed and retried without retyping the message.
/// </summary>
public partial class GitHubCommitDialog : Window
{
    private readonly IGitHubRepositoryService _repoService;
    private readonly GitHubDocument _document;
    private readonly string _text;
    private bool _busy;

    /// <summary>The successful commit, when <see cref="Window.DialogResult"/> is true.</summary>
    public GitHubCommitResult? Result { get; private set; }

    /// <summary>
    /// True when the direct commit was rejected because the file changed on GitHub; the caller
    /// decides how to resolve it. <see cref="CommitMessage"/> holds the message that was used.
    /// </summary>
    public bool ConflictDetected { get; private set; }

    /// <summary>The full commit message (summary + description) last submitted.</summary>
    public string CommitMessage { get; private set; } = string.Empty;

    /// <param name="conflictMode">
    /// The file is known to have changed on GitHub: only the new-branch option is offered.
    /// </param>
    public GitHubCommitDialog(IGitHubRepositoryService repoService, GitHubDocument document, string text,
        string accountLogin, string? initialMessage = null, bool conflictMode = false)
    {
        InitializeComponent();
        _repoService = repoService;
        _document = document;
        _text = text;

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);

        TargetText.Text = $"{document.RepoFullName}  -  {document.Path}";
        DirectRadio.Content = $"Commit directly to the {document.Branch} branch";
        PullRequestCheck.Content = $"Open a pull request into {document.Branch}";
        BranchNameBox.Text = $"{accountLogin.ToLowerInvariant()}-patch-{DateTime.Now:yyyyMMdd-HHmm}";

        var (summary, description) = SplitMessage(initialMessage ?? $"Update {document.FileName}");
        CommitMessageBox.Text = summary;
        DescriptionBox.Text = description;

        if (conflictMode)
        {
            ConflictBanner.Visibility = Visibility.Visible;
            DirectRadio.IsEnabled = false;
            NewBranchRadio.IsChecked = true;
        }
        else
        {
            DirectRadio.IsChecked = true;
        }

        Loaded += (s, e) =>
        {
            CommitMessageBox.Focus();
            CommitMessageBox.SelectAll();
        };
        Closing += (s, e) => e.Cancel = _busy; // don't abandon an in-flight commit
        UpdateState();
    }

    private static (string Summary, string Description) SplitMessage(string message)
    {
        var parts = message.Split('\n', 2);
        return (parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : string.Empty);
    }

    private bool UseNewBranch => NewBranchRadio.IsChecked == true;

    private void Target_Changed(object sender, RoutedEventArgs e) => UpdateState();

    private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateState();

    private void UpdateState()
    {
        // PullRequestCheck's IsChecked="True" raises Checked during InitializeComponent, before
        // the controls declared after it (CommitButton, StatusText) exist.
        if (CommitButton == null || StatusText == null)
            return;

        NewBranchPanel.IsEnabled = UseNewBranch && !_busy;
        CommitButton.Content = UseNewBranch && PullRequestCheck.IsChecked == true ? "Commit & Open PR" : "Commit";

        string? problem = null;
        if (string.IsNullOrWhiteSpace(CommitMessageBox.Text))
            problem = "Enter a commit message.";
        else if (UseNewBranch && !GitHubRepositoryService.IsValidBranchName(BranchNameBox.Text.Trim()))
            problem = "Branch names can't contain spaces or ~ ^ : ? * [ \\, or start/end with '/' or '.'.";
        else if (UseNewBranch && BranchNameBox.Text.Trim() == _document.Branch)
            problem = "The new branch needs a different name from the current one.";

        CommitButton.IsEnabled = problem == null && !_busy;
        if (problem != null && (BranchNameBox.IsKeyboardFocused || StatusText.Visibility == Visibility.Visible))
            ShowStatus(problem, isError: true);
        else if (problem == null && !_busy)
            StatusText.Visibility = Visibility.Collapsed;
    }

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        var summary = CommitMessageBox.Text.Trim();
        var description = DescriptionBox.Text.Trim();
        CommitMessage = description.Length > 0 ? $"{summary}\n\n{description}" : summary;

        var newBranch = UseNewBranch ? BranchNameBox.Text.Trim() : null;
        var prTitle = UseNewBranch && PullRequestCheck.IsChecked == true ? summary : null;

        SetBusy(true);
        ShowStatus(newBranch != null ? $"Creating branch {newBranch} and committing..." : "Committing...", isError: false);
        try
        {
            Result = await _repoService.CommitFileAsync(_document, _text, CommitMessage, newBranch, prTitle);
            SetBusy(false);
            DialogResult = true;
        }
        catch (GitHubConflictException)
        {
            ConflictDetected = true;
            SetBusy(false);
            DialogResult = false;
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
        CancelButton.IsEnabled = !busy;
        CommitMessageBox.IsEnabled = DescriptionBox.IsEnabled = !busy;
        DirectRadio.IsEnabled = !busy && ConflictBanner.Visibility != Visibility.Visible;
        NewBranchRadio.IsEnabled = !busy;
        UpdateState();
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.IndianRed : (Brush)FindResource("WindowForegroundBrush");
        StatusText.Visibility = Visibility.Visible;
    }
}
