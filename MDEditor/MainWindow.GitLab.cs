using System.Windows;
using System.Windows.Input;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;

namespace MDEditor;

// GitLab (gitlab.com or self-hosted) documents: open from a project, commit, history. Mirrors the
// GitHub flow in MainWindow.GitHub.cs.
public partial class MainWindow
{
    // The GitLab file being edited, or null. Mutually exclusive with _currentFilePath,
    // _gitHubDocument and _gistDocument.
    private GitLabDocument? _gitLabDocument;
    private readonly IGitLabRepositoryService _gitLabRepoService;

    /// <summary>
    /// True if a GitLab account is active; otherwise offers to open the accounts window.
    /// </summary>
    private bool EnsureGitLabSignedIn()
    {
        if (_gitLabAuthService.ActiveAccount != null)
            return true;

        var result = MessageBox.Show(this, Loc.Get("GL_SignInFirstPrompt"), "GitLab",
            MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
            ShowGitLabAccountsWindow();
        return false;
    }

    private void OpenFromGitLab()
    {
        if (!EnsureGitLabSignedIn())
            return;

        if (_isDirty)
        {
            var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("GL_OpenTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        var browser = new GitLabBrowserWindow(_gitLabRepoService, _settingsService, _gitLabAuthService.ActiveAccount!.DisplayName) { Owner = this };
        if (browser.ShowDialog() != true || browser.Result == null)
            return;

        LoadGitLabFile(browser.Result);
    }

    private void LoadGitLabFile(GitLabFileContent file)
    {
        _currentFilePath = null;
        _gitHubDocument = null;
        _gistDocument = null;
        _gitLabDocument = file.Document;
        _pendingCommitMessage = null;
        SetEditorText(file.Text);
    }

    private void ShowGitLabHistory()
    {
        var document = _gitLabDocument;
        if (document == null || !EnsureGitLabSignedIn())
            return;

        var history = new GitLabHistoryWindow(_gitLabRepoService, document, GetEditorText()) { Owner = this };
        if (history.ShowDialog() != true || history.RestoredText == null || history.RestoredCommit == null)
            return;

        if (_isDirty)
        {
            var confirm = MessageBox.Show(this, Loc.Get("GH_ReplaceUnsavedWithVersion"), Loc.Get("GH_RestoreVersionTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;
        }

        SetEditorText(history.RestoredText);
        MarkDirty();
        _pendingCommitMessage = $"Restore {document.FileName} to {history.RestoredCommit.ShortSha}";
    }

    /// <summary>One Commit entry for both kinds of repository document.</summary>
    private void CommitRemote()
    {
        if (_gitLabDocument != null)
            CommitToGitLab(initialMessage: null, conflictMode: false);
        else
            CommitToGitHub();
    }

    private void CommitToGitLab(string? initialMessage, bool conflictMode)
    {
        var document = _gitLabDocument;
        if (document == null || !EnsureGitLabSignedIn())
            return;

        if (!document.CanPush)
        {
            MessageBox.Show(this,
                Loc.Format("GL_NoWriteAccess", _gitLabAuthService.ActiveAccount!.Username, document.ProjectPath),
                Loc.Get("GL_CommitTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new GitLabCommitDialog(_gitLabRepoService, document, GetEditorText(),
            _gitLabAuthService.ActiveAccount!.Username, initialMessage ?? _pendingCommitMessage, conflictMode) { Owner = this };
        var committed = dialog.ShowDialog() == true;

        if (committed && dialog.Result != null)
            ApplyGitLabCommitResult(dialog.Result);
        else if (dialog.ConflictDetected)
            ResolveGitLabConflict(document, dialog.CommitMessage);
    }

    private void ApplyGitLabCommitResult(GitLabCommitResult result)
    {
        // Later saves go to wherever this commit landed (a new branch keeps updating its merge request).
        _gitLabDocument = result.Document;
        _pendingCommitMessage = null;
        _isDirty = false;
        UpdateTitleBar();

        if (result.MergeRequestUrl != null)
        {
            var open = MessageBox.Show(this,
                Loc.Format("GL_MrOpened", result.Document.Branch),
                Loc.Get("GL_MrOpenedTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
                OpenInBrowser(result.MergeRequestUrl);
        }
    }

    private async void ResolveGitLabConflict(GitLabDocument document, string commitMessage)
    {
        var conflict = new GitLabConflictDialog(document) { Owner = this };
        conflict.ShowDialog();

        switch (conflict.Choice)
        {
            case GitHubConflictChoice.NewBranch:
                CommitToGitLab(commitMessage, conflictMode: true);
                break;

            case GitHubConflictChoice.Overwrite:
                await RunGitLabOperationAsync(async () =>
                {
                    var result = await _gitLabRepoService.CommitFileAsync(document, GetEditorText(), commitMessage, overwrite: true);
                    ApplyGitLabCommitResult(result);
                }, Loc.Get("GH_CommitFailed"));
                break;

            case GitHubConflictChoice.LoadTheirs:
                await RunGitLabOperationAsync(async () =>
                {
                    LoadGitLabFile(await _gitLabRepoService.GetLatestAsync(document));
                }, Loc.Get("GH_LoadLatestFailed"));
                break;
        }
    }

    private async Task RunGitLabOperationAsync(Func<Task> operation, string errorPrefix)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show(this, Loc.Format("GH_ErrorWithPrefix", errorPrefix, ex.Message), "GitLab", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }
}
