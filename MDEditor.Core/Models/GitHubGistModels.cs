namespace MDEditor.Core.Models;

/// <summary>
/// A gist in a listing.
/// </summary>
public class GitHubGistInfo
{
    public string Id { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public string Owner { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<string> FileNames { get; init; } = Array.Empty<string>();
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>The description, or the first file name for gists without one (as GitHub shows them).</summary>
    public string Title => string.IsNullOrWhiteSpace(Description) ? FileNames.FirstOrDefault() ?? Id : Description!;
}

/// <summary>
/// A gist file open in the editor.
/// </summary>
public class GitHubGistDocument
{
    public string GistId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;

    /// <summary>
    /// When the gist was last updated as far as the editor knows; a newer value on GitHub means
    /// someone else changed it (gists have no per-file SHA to compare).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>"LF" or "CRLF" - the file's original line endings, preserved on save.</summary>
    public string LineEnding { get; init; } = "LF";

    /// <summary>True when the active account owns the gist and can update it.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Short label for the title bar, e.g. "notes" or the gist ID.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Description) ? GistId[..Math.Min(8, GistId.Length)] : Description!;
}

/// <summary>
/// A gist file's text together with the document identity it was loaded from.
/// </summary>
public class GitHubGistFileContent
{
    public GitHubGistDocument Document { get; init; } = new();
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// One revision in a gist's history. Gists have no commit messages or branches - just a flat,
/// linear list of snapshots - so this mirrors <see cref="GitHubCommitInfo"/> without those fields.
/// </summary>
public class GitHubGistRevisionInfo
{
    public string Sha { get; init; } = string.Empty;
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
    public string? AuthorLogin { get; init; }
    public DateTimeOffset CommittedAt { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }

    /// <summary>The next-older revision, or null for the oldest one (mirrors GitHubCommitInfo.ParentSha).</summary>
    public string? PreviousSha { get; init; }

    public string HtmlUrl { get; init; } = string.Empty;
}
