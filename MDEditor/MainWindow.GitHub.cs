using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using MDEditor.Core.Models;

namespace MDEditor;

/// <summary>
/// Phase 5 GitHub integration: account status, opening files from a repository, and committing
/// them back (direct, new branch + pull request, conflict resolution).
/// </summary>
public partial class MainWindow
{
    private GitHubAccountsWindow? _gitHubAccountsWindow;

    // The GitHub file being edited, or null for local/untitled documents. Mutually exclusive
    // with _currentFilePath.
    private GitHubDocument? _gitHubDocument;

    // Suggested message for the next commit (e.g. after restoring an old version).
    private string? _pendingCommitMessage;

    internal void ShowGitHubAccountsWindow()
    {
        if (_gitHubAccountsWindow != null)
        {
            _gitHubAccountsWindow.Activate();
            return;
        }

        _gitHubAccountsWindow = new GitHubAccountsWindow(_gitHubAuthService) { Owner = this };
        _gitHubAccountsWindow.Closed += (s, e) => _gitHubAccountsWindow = null;
        _gitHubAccountsWindow.Show();
    }

    private void UpdateGitHubStatus()
    {
        var active = _gitHubAuthService.ActiveAccount;
        int count = _gitHubAuthService.Accounts.Count;
        StatusGitHubAccount.Text = active?.Login ?? "Sign in";
        StatusGitHubItem.ToolTip = active != null
            ? $"GitHub: signed in as {active.Login}{(count > 1 ? $" ({count} accounts)" : string.Empty)}. Click to manage accounts."
            : "GitHub: not signed in. Click to connect an account.";
    }

    private void StatusGitHub_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ShowGitHubAccountsWindow();

    /// <summary>
    /// True if an account is active; otherwise offers to open the accounts window.
    /// </summary>
    private bool EnsureGitHubSignedIn()
    {
        if (_gitHubAuthService.ActiveAccount != null)
            return true;

        var result = MessageBox.Show(this, "Sign in to GitHub first. Open GitHub Accounts now?", "GitHub",
            MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
            ShowGitHubAccountsWindow();
        return false;
    }

    private void OpenFromGitHub()
    {
        if (!EnsureGitHubSignedIn())
            return;

        if (_isDirty)
        {
            var result = MessageBox.Show("You have unsaved changes. Discard them?", "Open from GitHub", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        var browser = new GitHubBrowserWindow(_gitHubRepoService, _settingsService, _gitHubAuthService.ActiveAccount!.Login) { Owner = this };
        if (browser.ShowDialog() != true || browser.Result == null)
            return;

        LoadGitHubFile(browser.Result);
    }

    private void LoadGitHubFile(GitHubFileContent file)
    {
        _currentFilePath = null;
        _gitHubDocument = file.Document;
        _pendingCommitMessage = null;
        SetEditorText(file.Text);
    }

    private void ShowGitHubHistory()
    {
        var document = _gitHubDocument;
        if (document == null)
        {
            MessageBox.Show(this,
                "History is available for files opened from GitHub (File > Open from GitHub...).",
                "File History", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!EnsureGitHubSignedIn())
            return;

        var history = new GitHubHistoryWindow(_gitHubRepoService, document, GetEditorText()) { Owner = this };
        if (history.ShowDialog() != true || history.RestoredText == null || history.RestoredCommit == null)
            return;

        if (_isDirty)
        {
            var result = MessageBox.Show(this, "Replace your unsaved changes with this version?", "Restore Version",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        // Restoring only changes the editor; it reaches GitHub with the next save (commit), so
        // the old version becomes the newest commit rather than rewriting history.
        SetEditorText(history.RestoredText);
        MarkDirty();
        _pendingCommitMessage = $"Restore {document.FileName} to {history.RestoredCommit.ShortSha}";
    }

    /// <summary>
    /// Save for a GitHub document: shows the commit dialog and handles conflicts.
    /// </summary>
    private void CommitToGitHub() => CommitToGitHub(initialMessage: null, conflictMode: false);

    private void CommitToGitHub(string? initialMessage, bool conflictMode)
    {
        var document = _gitHubDocument;
        if (document == null)
        {
            MessageBox.Show(this,
                "This document isn't from GitHub. Use File > Open from GitHub... to edit a repository file, then save to commit it.",
                "Commit to GitHub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!EnsureGitHubSignedIn())
            return;

        if (!document.CanPush)
        {
            MessageBox.Show(this,
                $"Your account ({_gitHubAuthService.ActiveAccount!.Login}) doesn't have write access to {document.RepoFullName}. Use File > Save As... to keep a local copy.",
                "Commit to GitHub", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new GitHubCommitDialog(_gitHubRepoService, document, GetEditorText(),
            _gitHubAuthService.ActiveAccount!.Login, initialMessage ?? _pendingCommitMessage, conflictMode) { Owner = this };
        bool committed = dialog.ShowDialog() == true;

        if (committed && dialog.Result != null)
        {
            ApplyCommitResult(dialog.Result);
        }
        else if (dialog.ConflictDetected)
        {
            ResolveGitHubConflict(document, dialog.CommitMessage);
        }
    }

    private void ApplyCommitResult(GitHubCommitResult result)
    {
        // Later saves go to wherever this commit landed (a new branch keeps updating its PR).
        _gitHubDocument = result.Document;
        _pendingCommitMessage = null;
        _isDirty = false;
        UpdateTitleBar();

        if (result.PullRequestUrl != null)
        {
            var open = MessageBox.Show(this,
                $"Committed to the new branch {result.Document.Branch} and opened a pull request.\n\nView the pull request on GitHub?",
                "Pull Request Opened", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                OpenInBrowser(result.PullRequestUrl);
        }
    }

    private async void ResolveGitHubConflict(GitHubDocument document, string commitMessage)
    {
        var conflict = new GitHubConflictDialog(document) { Owner = this };
        conflict.ShowDialog();

        switch (conflict.Choice)
        {
            case GitHubConflictChoice.NewBranch:
                CommitToGitHub(commitMessage, conflictMode: true);
                break;

            case GitHubConflictChoice.Overwrite:
                await RunGitHubOperationAsync(async () =>
                {
                    var result = await _gitHubRepoService.CommitFileAsync(document, GetEditorText(), commitMessage, overwrite: true);
                    ApplyCommitResult(result);
                }, "Couldn't commit");
                break;

            case GitHubConflictChoice.LoadTheirs:
                await RunGitHubOperationAsync(async () =>
                {
                    LoadGitHubFile(await _gitHubRepoService.GetLatestAsync(document));
                }, "Couldn't load the latest version");
                break;
        }
    }

    private async Task RunGitHubOperationAsync(Func<Task> operation, string errorPrefix)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show(this, $"{errorPrefix}: {ex.Message}", "GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't open the browser: {ex.Message}\n\n{url}", "GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
