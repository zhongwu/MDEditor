namespace MDEditor.Core.Models;

/// <summary>
/// Non-secret metadata for a connected GitLab account on one server. The access token lives in
/// <see cref="Interfaces.ITokenStore"/>, never in settings.json.
/// </summary>
public class GitLabAccount
{
    /// <summary>Normalized server root, e.g. "http://unicornlab.fun:35080" (no trailing slash, no /api/v4).</summary>
    public string ServerUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>GitLab version reported by the server when the account was added, if known.</summary>
    public string? ServerVersion { get; set; }

    /// <summary>Accept untrusted/self-signed TLS certificates for this server (self-hosted installs).</summary>
    public bool SkipCertificateValidation { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    /// <summary>"host[:port][/path]" without the scheme.</summary>
    public string ServerDisplay => ServerUrl.Contains("://") ? ServerUrl[(ServerUrl.IndexOf("://", StringComparison.Ordinal) + 3)..] : ServerUrl;

    /// <summary>Unique key of the account: the same user name on two servers is two accounts.</summary>
    public string Key => $"{Username}@{ServerDisplay}".ToLowerInvariant();

    public string DisplayName => $"{Username}@{ServerDisplay}";
}

/// <summary>A project (repository) the account is a member of.</summary>
public class GitLabProject
{
    public long Id { get; init; }

    /// <summary>"group/subgroup/project".</summary>
    public string PathWithNamespace { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
    public string DefaultBranch { get; init; } = "main";
    public string? Description { get; init; }

    /// <summary>True unless the project is public.</summary>
    public bool IsPrivate { get; init; }

    /// <summary>True if the account may commit (Developer access or higher) and the project isn't archived.</summary>
    public bool CanPush { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
    public string WebUrl { get; init; } = string.Empty;
}

/// <summary>
/// A file opened from GitLab: where it lives and which version the editor's copy is based on.
/// </summary>
public class GitLabDocument
{
    public string ServerUrl { get; init; } = string.Empty;
    public long ProjectId { get; init; }

    /// <summary>"group/subgroup/project".</summary>
    public string ProjectPath { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;

    /// <summary>Id of the last commit that touched the file when it was opened (or last committed). Used to detect conflicts.</summary>
    public string LastCommitId { get; init; } = string.Empty;

    /// <summary>Head commit of <see cref="Branch"/> when the file was opened (or last committed); new branches start here.</summary>
    public string BaseCommitSha { get; init; } = string.Empty;

    /// <summary>"LF" or "CRLF" - the file's original line endings, preserved on commit.</summary>
    public string LineEnding { get; init; } = "LF";

    public bool CanPush { get; init; }

    public string FileName => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>Display form, e.g. "group/docs:guide/intro.md@main".</summary>
    public string DisplayName => $"{ProjectPath}:{Path}@{Branch}";

    public string HtmlUrl => $"{ServerUrl}/{ProjectPath}/-/blob/{Uri.EscapeDataString(Branch)}/{EscapePath(Path)}";

    /// <summary>Raw URL of the repository root at <see cref="BaseCommitSha"/> (the branch if unknown), ending with '/'.</summary>
    public string RawRootUrl =>
        $"{ServerUrl}/{ProjectPath}/-/raw/{(string.IsNullOrEmpty(BaseCommitSha) ? Uri.EscapeDataString(Branch) : BaseCommitSha)}/";

    /// <summary>Raw URL of the folder containing the file, ending with '/'.</summary>
    public string RawFolderUrl
    {
        get
        {
            var slash = Path.LastIndexOf('/');
            return slash < 0 ? RawRootUrl : RawRootUrl + EscapePath(Path[..slash]) + "/";
        }
    }

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}

/// <summary>A file's contents together with the document identity it was loaded from.</summary>
public class GitLabFileContent
{
    public GitLabDocument Document { get; init; } = new();
    public string Text { get; init; } = string.Empty;
}

/// <summary>The outcome of a commit.</summary>
public class GitLabCommitResult
{
    /// <summary>The document as it now exists on GitLab (new commit id, possibly a new branch).</summary>
    public GitLabDocument Document { get; init; } = new();

    public string CommitUrl { get; init; } = string.Empty;

    /// <summary>URL of the merge request opened for this commit, if one was requested.</summary>
    public string? MergeRequestUrl { get; init; }
}
