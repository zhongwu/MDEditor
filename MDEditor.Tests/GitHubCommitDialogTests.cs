using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using Moq;

namespace MDEditor.Tests;

public class GitHubCommitDialogTests
{
    private static readonly GitHubDocument Document = new()
    {
        Owner = "octo", Repo = "docs", Branch = "main", Path = "intro.md", Sha = "s", BaseCommitSha = "c", CanPush = true,
    };

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw new Exception("Dialog construction failed", error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructs_WithoutThrowing(bool conflictMode)
    {
        // Regression: PullRequestCheck's IsChecked="True" fired Checked during InitializeComponent,
        // before CommitButton existed -> NullReferenceException in UpdateState.
        RunOnSta(() =>
        {
            var dialog = new GitHubCommitDialog(Mock.Of<IGitHubRepositoryService>(), Document, "text", "Me", null, conflictMode);
            Assert.Null(dialog.Result);
            dialog.Close();
        });
    }

    [Fact]
    public void HistoryWindow_Constructs_WithoutThrowing()
    {
        // Same hazard: CommitChangesRadio is IsChecked="True" with a Checked handler.
        RunOnSta(() =>
        {
            var window = new GitHubHistoryWindow(Mock.Of<IGitHubRepositoryService>(), Document, "text");
            Assert.Null(window.RestoredText);
            window.Close();
        });
    }
}
