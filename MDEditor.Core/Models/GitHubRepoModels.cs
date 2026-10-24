namespace MDEditor.Core.Models;

/// <summary>
/// A repository the signed-in account can see.
/// </summary>
public class GitHubRepoInfo
{
    public string Owner { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FullName => $"{Owner}/{Name}";
    public string DefaultBranch { get; init; } = "main";
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }

    /// <summary>True if the account can commit to this repository.</summary>
    public bool CanPush { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// A file or folder in a repository tree (flat; <see cref="Path"/> is repo-relative with '/').
/// </summary>
public class GitHubTreeEntry
{
    public string Path { get; init; } = string.Empty;
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];
    public bool IsFolder { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// The result of loading a repository tree.
/// </summary>
public class GitHubTree
{
    public IReadOnlyList<GitHubTreeEntry> Entries { get; init; } = Array.Empty<GitHubTreeEntry>();

    /// <summary>
    /// True when GitHub cut the listing short (very large repositories).
    /// </summary>
    public bool Truncated { get; init; }
}

/// <summary>
/// A file opened from GitHub: where it lives and which version the editor's copy is based on.
/// </summary>
public class GitHubDocument
{
    public string Owner { get; init; } = string.Empty;
    public string Repo { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;

    /// <summary>Blob SHA of the version that was opened (or last committed).</summary>
    public string Sha { get; init; } = string.Empty;

    /// <summary>
    /// Commit SHA of <see cref="Branch"/> when the file was opened (or last committed). A new
    /// branch for a conflicting save starts here, so the edit applies cleanly on top of it.
    /// </summary>
    public string BaseCommitSha { get; init; } = string.Empty;

    /// <summary>"LF" or "CRLF" - the file's original line endings, preserved on commit.</summary>
    public string LineEnding { get; init; } = "LF";

    public bool CanPush { get; init; }

    public string FileName => Path[(Path.LastIndexOf('/') + 1)..];
    public string RepoFullName => $"{Owner}/{Repo}";

    /// <summary>Display form, e.g. "octocat/docs:guide/intro.md@main".</summary>
    public string DisplayName => $"{RepoFullName}:{Path}@{Branch}";

    /// <summary>
    /// raw.githubusercontent.com URL of the repository root at <see cref="BaseCommitSha"/> (the
    /// branch if unknown), ending with '/'. Pinning the commit keeps images consistent with the
    /// opened text.
    /// </summary>
    public string RawRootUrl =>
        $"https://raw.githubusercontent.com/{Owner}/{Repo}/{(string.IsNullOrEmpty(BaseCommitSha) ? EscapePath(Branch) : BaseCommitSha)}/";

    /// <summary>raw.githubusercontent.com URL of the folder containing the file, ending with '/'.</summary>
    public string RawFolderUrl
    {
        get
        {
            var slash = Path.LastIndexOf('/');
            return slash < 0 ? RawRootUrl : RawRootUrl + EscapePath(Path[..slash]) + "/";
        }
    }

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public string HtmlUrl =>
        $"https://github.com/{Owner}/{Repo}/blob/{Uri.EscapeDataString(Branch)}/{EscapePath(Path)}";
}

/// <summary>
/// A file's contents together with the document identity it was loaded from.
/// </summary>
public class GitHubFileContent
{
    public GitHubDocument Document { get; init; } = new();
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// The outcome of a commit.
/// </summary>
public class GitHubCommitResult
{
    /// <summary>The document as it now exists on GitHub (new SHA, possibly new branch).</summary>
    public GitHubDocument Document { get; init; } = new();

    public string CommitUrl { get; init; } = string.Empty;

    /// <summary>URL of the pull request opened for this commit, if one was requested.</summary>
    public string? PullRequestUrl { get; init; }
}

/// <summary>
/// A commit in a file's history.
/// </summary>
public class GitHubCommitInfo
{
    public string Sha { get; init; } = string.Empty;
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
    public string Message { get; init; } = string.Empty;

    /// <summary>First line of <see cref="Message"/>.</summary>
    public string Summary => Message.Split('\n', 2)[0].Trim();

    public string AuthorName { get; init; } = string.Empty;

    /// <summary>GitHub login of the author, when the commit email maps to an account.</summary>
    public string? AuthorLogin { get; init; }

    public DateTimeOffset Date { get; init; }

    /// <summary>First parent commit, or null for a root commit.</summary>
    public string? ParentSha { get; init; }

    public string HtmlUrl { get; init; } = string.Empty;
}

/// <summary>
/// Thrown when a commit fails because the file changed on GitHub since it was opened.
/// </summary>
public class GitHubConflictException : Exception
{
    public GitHubConflictException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
