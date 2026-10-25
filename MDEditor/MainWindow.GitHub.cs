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
        StatusGitHubAccount.Text = active?.Login ?? Loc.Get("Status_SignIn");
        StatusGitHubItem.ToolTip = active != null
            ? Loc.Format("Status_GitHubSignedIn", active.Login, count > 1 ? Loc.Format("Status_GitHubAccountCount", count) : string.Empty)
            : Loc.Get("Status_GitHubNotSignedIn");
    }

    /// <summary>
    /// Authorization header for preview image requests into the open document's repository, so
    /// images in private repos load. The token is only ever sent to that repo's raw URLs.
    /// </summary>
    private string? GetGitHubImageAuthorization(string url)
    {
        var document = _gitHubDocument;
        if (document == null)
            return null;

        var repoPrefix = $"https://raw.githubusercontent.com/{document.Owner}/{document.Repo}/";
        if (!url.StartsWith(repoPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = _gitHubAuthService.GetAccessToken();
        return token == null ? null : "token " + token;
    }

    private void StatusGitHub_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ShowGitHubAccountsWindow();

    /// <summary>
    /// True if an account is active; otherwise offers to open the accounts window.
    /// </summary>
    private bool EnsureGitHubSignedIn()
    {
        if (_gitHubAuthService.ActiveAccount != null)
            return true;

        var result = MessageBox.Show(this, Loc.Get("GH_SignInFirstPrompt"), "GitHub",
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
            var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("GH_OpenFromGitHubTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                Loc.Get("GH_HistoryOnlyGitHub"),
                Loc.Get("GH_HistoryTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!EnsureGitHubSignedIn())
            return;

        var history = new GitHubHistoryWindow(_gitHubRepoService, document, GetEditorText()) { Owner = this };
        if (history.ShowDialog() != true || history.RestoredText == null || history.RestoredCommit == null)
            return;

        if (_isDirty)
        {
            var result = MessageBox.Show(this, Loc.Get("GH_ReplaceUnsavedWithVersion"), Loc.Get("GH_RestoreVersionTitle"),
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
                Loc.Get("GH_NotFromGitHub"),
                Loc.Get("GH_CommitTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!EnsureGitHubSignedIn())
            return;

        if (!document.CanPush)
        {
            MessageBox.Show(this,
                Loc.Format("GH_NoWriteAccess", _gitHubAuthService.ActiveAccount!.Login, document.RepoFullName),
                Loc.Get("GH_CommitTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
                Loc.Format("GH_PrOpened", result.Document.Branch),
                Loc.Get("GH_PrOpenedTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information);
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
                }, Loc.Get("GH_CommitFailed"));
                break;

            case GitHubConflictChoice.LoadTheirs:
                await RunGitHubOperationAsync(async () =>
                {
                    LoadGitHubFile(await _gitHubRepoService.GetLatestAsync(document));
                }, Loc.Get("GH_LoadLatestFailed"));
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
            MessageBox.Show(this, Loc.Format("GH_ErrorWithPrefix", errorPrefix, ex.Message), "GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(this, Loc.Format("GH_BrowserOpenFailedUrl", ex.Message, url), "GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
