using MDEditor.Core.Models;

namespace MDEditor.Core.Interfaces;

/// <summary>
/// Repository browsing and file commit operations for the active GitHub account.
/// </summary>
public interface IGitHubRepositoryService
{
    /// <summary>
    /// Repositories the active account owns, collaborates on, or can see through organizations,
    /// most recently updated first.
    /// </summary>
    Task<IReadOnlyList<GitHubRepoInfo>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Branch names of a repository.
    /// </summary>
    Task<IReadOnlyList<string>> GetBranchesAsync(string owner, string repo, CancellationToken cancellationToken = default);

    /// <summary>
    /// All files and folders on a branch (recursive, flat).
    /// </summary>
    Task<GitHubTree> GetTreeAsync(string owner, string repo, string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a text file from a branch.
    /// </summary>
    Task<GitHubFileContent> GetFileAsync(GitHubRepoInfo repo, string branch, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-downloads the latest version of an opened document from its branch.
    /// </summary>
    Task<GitHubFileContent> GetLatestAsync(GitHubDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits <paramref name="text"/> to the document's file.
    /// </summary>
    /// <param name="newBranch">
    /// When set, the branch is created from <see cref="GitHubDocument.BaseCommitSha"/> and the
    /// commit goes there instead of the document's branch.
    /// </param>
    /// <param name="pullRequestTitle">When set (with a new branch), opens a pull request into the original branch.</param>
    /// <param name="overwrite">
    /// Commit even if the file changed on GitHub since it was opened, replacing those changes.
    /// </param>
    /// <exception cref="GitHubConflictException">The file changed on GitHub and <paramref name="overwrite"/> is false.</exception>
    Task<GitHubCommitResult> CommitFileAsync(GitHubDocument document, string text, string message,
        string? newBranch = null, string? pullRequestTitle = null, bool overwrite = false,
        CancellationToken cancellationToken = default);
}
