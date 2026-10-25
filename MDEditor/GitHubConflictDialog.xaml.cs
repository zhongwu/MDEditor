using System;
using System.Diagnostics;
using System.Windows;
using MDEditor.Core.Models;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// How the user chose to resolve a commit conflict.
/// </summary>
public enum GitHubConflictChoice
{
    Cancel,
    NewBranch,
    Overwrite,
    LoadTheirs,
}

/// <summary>
/// Modal dialog shown when a commit is rejected because the file changed on GitHub.
/// </summary>
public partial class GitHubConflictDialog : Window
{
    private readonly GitHubDocument _document;

    public GitHubConflictChoice Choice { get; private set; } = GitHubConflictChoice.Cancel;

    public GitHubConflictDialog(GitHubDocument document)
    {
        InitializeComponent();
        _document = document;
        HeadlineText.Text = Loc.Format("GH_ConflictHeadline", document.FileName, document.RepoFullName, document.Branch);

        SourceInitialized += (s, e) => ThemeService.ApplyTitleBar(this, ThemeService.IsDark);
        Loaded += (s, e) => NewBranchButton.Focus();
    }

    private void Choose(GitHubConflictChoice choice)
    {
        Choice = choice;
        DialogResult = true;
    }

    private void NewBranch_Click(object sender, RoutedEventArgs e) => Choose(GitHubConflictChoice.NewBranch);

    private void Overwrite_Click(object sender, RoutedEventArgs e) => Choose(GitHubConflictChoice.Overwrite);

    private void LoadTheirs_Click(object sender, RoutedEventArgs e) => Choose(GitHubConflictChoice.LoadTheirs);

    private void ViewLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_document.HtmlUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.Format("Common_BrowserOpenFailed", ex.Message), Loc.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
