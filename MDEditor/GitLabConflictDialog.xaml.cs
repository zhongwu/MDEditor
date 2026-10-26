using System.Diagnostics;
using System.Windows;
using MDEditor.Core.Models;
using MDEditor.Themes;

namespace MDEditor;

/// <summary>
/// Modal dialog shown when a commit is rejected because the file changed on the GitLab server.
/// Reports its decision with the same <see cref="GitHubConflictChoice"/> values as the GitHub dialog.
/// </summary>
public partial class GitLabConflictDialog : Window
{
    private readonly GitLabDocument _document;

    public GitHubConflictChoice Choice { get; private set; } = GitHubConflictChoice.Cancel;

    public GitLabConflictDialog(GitLabDocument document)
    {
        InitializeComponent();
        _document = document;
        HeadlineText.Text = Loc.Format("GH_ConflictHeadline", document.FileName, document.ProjectPath, document.Branch);

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
