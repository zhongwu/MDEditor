using MDEditor.Core.Models;

namespace MDEditor.Core.Interfaces;

/// <summary>
/// Project browsing and file commit operations for the active GitLab account (REST API v4).
/// </summary>
public interface IGitLabRepositoryService
{
    /// <summary>Projects the active account is a member of, most recently active first.</summary>
    Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>Branch names of a project.</summary>
    Task<IReadOnlyList<string>> GetBranchesAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>All files and folders on a branch (recursive, flat).</summary>
    Task<GitHubTree> GetTreeAsync(long projectId, string branch, CancellationToken cancellationToken = default);

    /// <summary>Downloads a text file from a branch.</summary>
    Task<GitLabFileContent> GetFileAsync(GitLabProject project, string branch, string path, CancellationToken cancellationToken = default);

    /// <summary>Re-downloads the latest version of an opened document from its branch.</summary>
    Task<GitLabFileContent> GetLatestAsync(GitLabDocument document, CancellationToken cancellationToken = default);

    /// <summary>Commits on the document's branch that changed its file, newest first.</summary>
    Task<IReadOnlyList<GitHubCommitInfo>> GetFileHistoryAsync(GitLabDocument document, int maxCount = 100, CancellationToken cancellationToken = default);

    /// <summary>The document's file as of <paramref name="commitSha"/>, or null if it didn't exist then.</summary>
    Task<string?> GetFileAtCommitAsync(GitLabDocument document, string commitSha, CancellationToken cancellationToken = default);

    /// <summary>Commits <paramref name="text"/> to the document's file.</summary>
    /// <param name="newBranch">When set, the commit goes to a new branch started from <see cref="GitLabDocument.BaseCommitSha"/>.</param>
    /// <param name="mergeRequestTitle">When set (with a new branch), opens a merge request into the original branch.</param>
    /// <param name="overwrite">Commit even if the file changed on the server since it was opened, replacing those changes.</param>
    /// <exception cref="GitHubConflictException">The file changed on the server and <paramref name="overwrite"/> is false.</exception>
    Task<GitLabCommitResult> CommitFileAsync(GitLabDocument document, string text, string message,
        string? newBranch = null, string? mergeRequestTitle = null, bool overwrite = false,
        CancellationToken cancellationToken = default);
}
