using System.Net;
using System.Text;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using Octokit;

namespace MDEditor.Core.Services;

/// <summary>
/// <see cref="IGitHubRepositoryService"/> over Octokit, using the active account from
/// <see cref="IGitHubAuthService"/>.
/// </summary>
public class GitHubRepositoryService : IGitHubRepositoryService
{
    private static readonly string[] MarkdownExtensions = { ".md", ".markdown", ".mdown", ".mkd", ".mdx" };

    private readonly IGitHubAuthService _authService;
    private readonly ILoggingService _loggingService;

    public GitHubRepositoryService(IGitHubAuthService authService, ILoggingService loggingService)
    {
        _authService = authService;
        _loggingService = loggingService;
    }

    /// <summary>True for paths with a Markdown file extension.</summary>
    public static bool IsMarkdownPath(string path) =>
        MarkdownExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks a branch name against git's ref-name rules (git check-ref-format), so a bad name
    /// is caught before any API call.
    /// </summary>
    public static bool IsValidBranchName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "@")
            return false;
        if (name.StartsWith('/') || name.EndsWith('/') || name.EndsWith('.') || name.EndsWith(".lock", StringComparison.Ordinal))
            return false;
        if (name.Contains("..") || name.Contains("//") || name.Contains("@{") || name.Contains("/."))
            return false;
        if (name.StartsWith('.') || name.StartsWith('-'))
            return false;
        return !name.Any(c => c <= ' ' || c == 0x7F || "~^:?*[\\".Contains(c));
    }

    /// <summary>"CRLF" if the text uses Windows line endings, otherwise "LF".</summary>
    public static string DetectLineEnding(string text) => text.Contains("\r\n") ? "CRLF" : "LF";

    private IGitHubClient Client =>
        _authService.CreateClient() ?? throw new InvalidOperationException("Sign in to GitHub first (Tools > GitHub Accounts...).");

    public async Task<IReadOnlyList<GitHubRepoInfo>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var request = new RepositoryRequest { Sort = RepositorySort.Updated, Direction = SortDirection.Descending };
        var repos = await Client.Repository.GetAllForCurrent(request);
        cancellationToken.ThrowIfCancellationRequested();

        return repos.Select(r => new GitHubRepoInfo
        {
            Owner = r.Owner.Login,
            Name = r.Name,
            DefaultBranch = string.IsNullOrEmpty(r.DefaultBranch) ? "main" : r.DefaultBranch,
            Description = r.Description,
            IsPrivate = r.Private,
            CanPush = r.Permissions?.Push ?? false,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var branches = await Client.Repository.Branch.GetAll(owner, repo);
        cancellationToken.ThrowIfCancellationRequested();
        return branches.Select(b => b.Name).ToList();
    }

    public async Task<GitHubTree> GetTreeAsync(string owner, string repo, string branch, CancellationToken cancellationToken = default)
    {
        TreeResponse tree;
        try
        {
            tree = await Client.Git.Tree.GetRecursive(owner, repo, branch);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // 409 "Git Repository is empty."
            return new GitHubTree();
        }
        cancellationToken.ThrowIfCancellationRequested();

        var entries = tree.Tree
            .Where(item => item.Type.Value is TreeType.Blob or TreeType.Tree)
            .Select(item => new GitHubTreeEntry
            {
                Path = item.Path,
                IsFolder = item.Type.Value == TreeType.Tree,
                Size = item.Size,
            })
            .ToList();

        return new GitHubTree { Entries = entries, Truncated = tree.Truncated };
    }

    public async Task<GitHubFileContent> GetFileAsync(GitHubRepoInfo repo, string branch, string path, CancellationToken cancellationToken = default)
    {
        var client = Client;

        // Pin the branch head first and read the file at that exact commit, so BaseCommitSha and
        // the blob SHA describe the same snapshot even if someone pushes in between.
        var head = await client.Repository.Branch.Get(repo.Owner, repo.Name, branch);
        cancellationToken.ThrowIfCancellationRequested();

        var (text, sha) = await ReadFileAsync(client, repo.Owner, repo.Name, path, head.Commit.Sha);
        cancellationToken.ThrowIfCancellationRequested();

        _loggingService.LogInformation("Opened {Repo}:{Path}@{Branch} from GitHub", repo.FullName, path, branch);

        return new GitHubFileContent
        {
            Text = text,
            Document = new GitHubDocument
            {
                Owner = repo.Owner,
                Repo = repo.Name,
                Branch = branch,
                Path = path,
                Sha = sha,
                BaseCommitSha = head.Commit.Sha,
                LineEnding = DetectLineEnding(text),
                CanPush = repo.CanPush,
            },
        };
    }

    public async Task<GitHubFileContent> GetLatestAsync(GitHubDocument document, CancellationToken cancellationToken = default)
    {
        var repo = new GitHubRepoInfo { Owner = document.Owner, Name = document.Repo, CanPush = document.CanPush };
        return await GetFileAsync(repo, document.Branch, document.Path, cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubCommitInfo>> GetFileHistoryAsync(GitHubDocument document, int maxCount = 100, CancellationToken cancellationToken = default)
    {
        var request = new CommitRequest { Path = document.Path, Sha = document.Branch };
        var options = new ApiOptions { PageSize = Math.Clamp(maxCount, 1, 100), PageCount = Math.Max(1, (maxCount + 99) / 100) };
        var commits = await Client.Repository.Commit.GetAll(document.Owner, document.Repo, request, options);
        cancellationToken.ThrowIfCancellationRequested();

        return commits.Take(maxCount).Select(c => new GitHubCommitInfo
        {
            Sha = c.Sha,
            Message = c.Commit?.Message ?? string.Empty,
            AuthorName = c.Commit?.Author?.Name ?? c.Author?.Login ?? "unknown",
            AuthorLogin = c.Author?.Login,
            Date = c.Commit?.Author?.Date ?? default,
            ParentSha = c.Parents?.FirstOrDefault()?.Sha,
            HtmlUrl = string.IsNullOrEmpty(c.HtmlUrl) ? $"https://github.com/{document.Owner}/{document.Repo}/commit/{c.Sha}" : c.HtmlUrl,
        }).ToList();
    }

    public async Task<string?> GetFileAtCommitAsync(GitHubDocument document, string commitSha, CancellationToken cancellationToken = default)
    {
        try
        {
            var (text, _) = await ReadFileAsync(Client, document.Owner, document.Repo, document.Path, commitSha);
            cancellationToken.ThrowIfCancellationRequested();
            return text;
        }
        catch (NotFoundException)
        {
            return null; // the file was added after this commit (or renamed)
        }
    }

    private static async Task<(string Text, string Sha)> ReadFileAsync(IGitHubClient client, string owner, string repo, string path, string reference)
    {
        var contents = await client.Repository.Content.GetAllContentsByRef(owner, repo, path, reference);
        var file = contents.FirstOrDefault(c => c.Type.Value == ContentType.File)
            ?? throw new InvalidOperationException($"'{path}' is not a file.");

        string text;
        if (!string.IsNullOrEmpty(file.Content) || file.Size == 0)
        {
            text = file.Content ?? string.Empty;
        }
        else
        {
            // The contents API omits the body for files over 1 MB; fall back to the raw endpoint.
            var raw = await client.Repository.Content.GetRawContentByRef(owner, repo, path, reference);
            text = Encoding.UTF8.GetString(raw);
        }

        return (text.TrimStart('﻿'), file.Sha);
    }

    public async Task<GitHubCommitResult> CommitFileAsync(GitHubDocument document, string text, string message,
        string? newBranch = null, string? pullRequestTitle = null, bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Commit message is empty.", nameof(message));

        var client = Client;
        var content = LineEndingNormalizer.Normalize(text, document.LineEnding);
        var targetBranch = document.Branch;
        var sha = document.Sha;

        if (!string.IsNullOrWhiteSpace(newBranch))
        {
            targetBranch = newBranch.Trim();
            try
            {
                await client.Git.Reference.Create(document.Owner, document.Repo,
                    new NewReference("refs/heads/" + targetBranch, document.BaseCommitSha));
            }
            catch (ApiValidationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"A branch named '{targetBranch}' already exists. Choose another name.", ex);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        else if (overwrite)
        {
            sha = await TryGetCurrentShaAsync(client, document, targetBranch);
        }

        RepositoryContentChangeSet changeSet;
        try
        {
            changeSet = sha == null
                ? await client.Repository.Content.CreateFile(document.Owner, document.Repo, document.Path,
                    new CreateFileRequest(message, content, targetBranch))
                : await client.Repository.Content.UpdateFile(document.Owner, document.Repo, document.Path,
                    new UpdateFileRequest(message, content, sha, targetBranch));
        }
        catch (ApiException ex) when (IsConflict(ex))
        {
            throw new GitHubConflictException($"{document.FileName} was changed on GitHub since you opened it.", ex);
        }
        catch (NotFoundException ex) when (!document.CanPush)
        {
            throw new InvalidOperationException($"You don't have permission to commit to {document.RepoFullName}.", ex);
        }

        _loggingService.LogInformation("Committed {Path} to {Repo}@{Branch}", document.Path, document.RepoFullName, targetBranch);

        string? pullRequestUrl = null;
        if (targetBranch != document.Branch && !string.IsNullOrWhiteSpace(pullRequestTitle))
        {
            var pr = await client.PullRequest.Create(document.Owner, document.Repo,
                new NewPullRequest(pullRequestTitle, targetBranch, document.Branch));
            pullRequestUrl = pr.HtmlUrl;
        }

        return new GitHubCommitResult
        {
            Document = new GitHubDocument
            {
                Owner = document.Owner,
                Repo = document.Repo,
                Branch = targetBranch,
                Path = document.Path,
                Sha = changeSet.Content.Sha,
                BaseCommitSha = changeSet.Commit.Sha,
                LineEnding = document.LineEnding,
                CanPush = document.CanPush,
            },
            CommitUrl = $"https://github.com/{document.Owner}/{document.Repo}/commit/{changeSet.Commit.Sha}",
            PullRequestUrl = pullRequestUrl,
        };
    }

    /// <summary>
    /// The file's current blob SHA on <paramref name="branch"/>, or null if it was deleted there.
    /// </summary>
    private static async Task<string?> TryGetCurrentShaAsync(IGitHubClient client, GitHubDocument document, string branch)
    {
        try
        {
            var contents = await client.Repository.Content.GetAllContentsByRef(document.Owner, document.Repo, document.Path, branch);
            return contents.FirstOrDefault()?.Sha;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    // GitHub answers a stale blob SHA with 409 Conflict, or 422 "... does not match <sha>".
    private static bool IsConflict(ApiException ex) =>
        ex.StatusCode == HttpStatusCode.Conflict
        || (ex.StatusCode == HttpStatusCode.UnprocessableEntity && ex.Message.Contains("does not match", StringComparison.OrdinalIgnoreCase));
}
