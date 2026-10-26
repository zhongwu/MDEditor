using System.Windows;
using System.Windows.Input;
using MDEditor.Core.Models;

namespace MDEditor;

/// <summary>
/// Phase 5.4 gist support: open a gist file, save the document as a new gist, and save edits
/// back to the gist.
/// </summary>
public partial class MainWindow
{
    // The gist file being edited, or null. Mutually exclusive with _currentFilePath and
    // _gitHubDocument.
    private GitHubGistDocument? _gistDocument;

    private void OpenGist()
    {
        if (!EnsureGitHubSignedIn())
            return;

        if (_isDirty)
        {
            var result = MessageBox.Show(Loc.Get("Common_UnsavedDiscard"), Loc.Get("Gist_OpenTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        var browser = new GistBrowserWindow(_gistService) { Owner = this };
        if (browser.ShowDialog() == true && browser.Result != null)
            LoadGistFile(browser.Result);
    }

    private void LoadGistFile(GitHubGistFileContent file)
    {
        _currentFilePath = null;
        _gitHubDocument = null;
        _gitLabDocument = null;
        _gistDocument = file.Document;
        _pendingCommitMessage = null;
        SetEditorText(file.Text);
    }

    private void SaveAsGist()
    {
        if (!EnsureGitHubSignedIn())
            return;

        var dialog = new GistCreateDialog(_gistService, GetEditorText(), CurrentDocumentName, _gistDocument?.Description) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } gist)
            return;

        // The new gist is now the document: later saves update it.
        _currentFilePath = null;
        _gitHubDocument = null;
        _gitLabDocument = null;
        _gistDocument = gist;
        _isDirty = false;
        UpdateTitleBar();

        try
        {
            Clipboard.SetText(gist.HtmlUrl);
        }
        catch
        {
            // Clipboard can be locked by another process; the link is still offered below.
        }

        var open = MessageBox.Show(this, Loc.Get("Gist_Created"), Loc.Get("Gist_CreatedTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (open == MessageBoxResult.Yes)
            OpenInBrowser(gist.HtmlUrl);
    }

    private async void SaveGist() => await SaveGistAsync();

    /// <summary>Saves the editor text to the open gist. Returns true once it's saved.</summary>
    private async Task<bool> SaveGistAsync()
    {
        var document = _gistDocument;
        if (document == null)
            return false;

        if (!document.CanEdit)
        {
            MessageBox.Show(this, Loc.Format("Gist_ReadOnly", document.Owner), Loc.Get("Gist_ReadOnlyTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (!EnsureGitHubSignedIn())
            return false;

        var text = GetEditorText();
        bool saved = false;
        await RunGitHubOperationAsync(async () =>
        {
            GitHubGistDocument updated;
            try
            {
                updated = await _gistService.UpdateAsync(document, text);
            }
            catch (GitHubConflictException)
            {
                Mouse.OverrideCursor = null;
                var choice = MessageBox.Show(this, Loc.Get("Gist_Conflict"), Loc.Get("Gist_ConflictTitle"),
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.No)
                {
                    LoadGistFile(await _gistService.GetFileAsync(document.GistId, document.FileName));
                    return;
                }
                if (choice != MessageBoxResult.Yes)
                    return;

                Mouse.OverrideCursor = Cursors.Wait;
                updated = await _gistService.UpdateAsync(document, text, overwrite: true);
            }

            // Only mark clean if nothing was typed while the save was in flight.
            _gistDocument = updated;
            if (GetEditorText() == text)
                _isDirty = false;
            UpdateTitleBar();
            saved = true;
        }, Loc.Get("Gist_SaveFailed"));

        return saved;
    }

    /// <summary>Close-prompt path: wait for the gist save, then close if it succeeded.</summary>
    private async void SaveGistThenClose()
    {
        if (await SaveGistAsync() && !_isDirty)
            Close();
    }

    private void ViewGistOnGitHub()
    {
        if (_gistDocument != null)
            OpenInBrowser(_gistDocument.HtmlUrl);
    }

    private void ShowGistHistory()
    {
        var document = _gistDocument;
        if (document == null)
        {
            MessageBox.Show(this,
                Loc.Get("Gist_HistoryOnlyGist"),
                Loc.Get("Gist_HistoryTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!EnsureGitHubSignedIn())
            return;

        var history = new GistHistoryWindow(_gistService, document, GetEditorText()) { Owner = this };
        if (history.ShowDialog() != true || history.RestoredText == null)
            return;

        if (_isDirty)
        {
            var result = MessageBox.Show(this, Loc.Get("GH_ReplaceUnsavedWithVersion"), Loc.Get("GH_RestoreVersionTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        // Restoring only changes the editor; it reaches GitHub with the next save, since gists
        // have no per-save message to pre-fill (unlike repo commits).
        SetEditorText(history.RestoredText);
        MarkDirty();
    }
}
