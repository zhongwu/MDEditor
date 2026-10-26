using System.Net;
using System.Text;
using System.Text.Json;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Localization;
using MDEditor.Core.Models;

namespace MDEditor.Core.Services;

/// <summary>
/// <see cref="IGitLabRepositoryService"/> over GitLab's REST API v4, using the active account from
/// <see cref="IGitLabAuthService"/>.
/// </summary>
public class GitLabRepositoryService : IGitLabRepositoryService
{
    /// <summary>GitLab's Developer role: the lowest that can push to unprotected branches.</summary>
    private const int DeveloperAccessLevel = 30;

    private const int MaxTreePages = 50;
    private const int MaxProjectPages = 20;

    private readonly IGitLabAuthService _authService;
    private readonly ILoggingService _loggingService;

    public GitLabRepositoryService(IGitLabAuthService authService, ILoggingService loggingService)
    {
        _authService = authService;
        _loggingService = loggingService;
    }

    private GitLabApiClient Client =>
        _authService.CreateClient() ?? throw new InvalidOperationException(Loc.Get("GL_Err_SignInFirst"));

    public async Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var client = Client;

        // Administrators can read and push to every project without being a member, so for them
        // "mine" means everything on the server; for everyone else it means projects they belong to.
        var isAdmin = await IsAdministratorAsync(client, cancellationToken);
        var path = isAdmin
            ? "projects?order_by=last_activity_at&sort=desc"
            : "projects?membership=true&order_by=last_activity_at&sort=desc";

        var (items, _) = await client.GetAllPagesAsync(path, MaxProjectPages, cancellationToken);
        return items.Select(p => ParseProject(p, isAdmin)).ToList();
    }

    private static async Task<bool> IsAdministratorAsync(GitLabApiClient client, CancellationToken cancellationToken)
    {
        try
        {
            var user = await client.GetAsync("user", cancellationToken);
            return user.TryGetProperty("is_admin", out var admin) && admin.ValueKind == JsonValueKind.True;
        }
        catch (GitLabApiException)
        {
            return false; // not being able to tell must never widen what is listed or writable
        }
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var (items, _) = await Client.GetAllPagesAsync($"projects/{projectId}/repository/branches", 10, cancellationToken);
        return items.Select(b => b.GetProperty("name").GetString()!).ToList();
    }

    public async Task<GitHubTree> GetTreeAsync(long projectId, string branch, CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, truncated) = await Client.GetAllPagesAsync(
                $"projects/{projectId}/repository/tree?recursive=true&ref={Uri.EscapeDataString(branch)}", MaxTreePages, cancellationToken);

            return new GitHubTree
            {
                Entries = items.Select(i => new GitHubTreeEntry
                {
                    Path = i.GetProperty("path").GetString()!,
                    IsFolder = i.GetProperty("type").GetString() == "tree",
                }).ToList(),
                Truncated = truncated,
            };
        }
        catch (GitLabApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new GitHubTree(); // empty repository: no tree on the branch yet
        }
    }

    public async Task<GitLabFileContent> GetFileAsync(GitLabProject project, string branch, string path, CancellationToken cancellationToken = default)
    {
        var client = Client;
        var file = await ReadFileAsync(client, project.Id, path, branch, cancellationToken);
        var head = await client.GetAsync($"projects/{project.Id}/repository/branches/{Uri.EscapeDataString(branch)}", cancellationToken);

        return new GitLabFileContent
        {
            Text = file.Text,
            Document = new GitLabDocument
            {
                ServerUrl = client.ServerUrl,
                ProjectId = project.Id,
                ProjectPath = project.PathWithNamespace,
                Branch = branch,
                Path = path,
                LastCommitId = file.LastCommitId,
                BaseCommitSha = head.GetProperty("commit").GetProperty("id").GetString() ?? string.Empty,
                LineEnding = GitHubRepositoryService.DetectLineEnding(file.Text),
                CanPush = project.CanPush,
            },
        };
    }

    public async Task<GitLabFileContent> GetLatestAsync(GitLabDocument document, CancellationToken cancellationToken = default)
    {
        var project = new GitLabProject { Id = document.ProjectId, PathWithNamespace = document.ProjectPath, CanPush = document.CanPush };
        return await GetFileAsync(project, document.Branch, document.Path, cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubCommitInfo>> GetFileHistoryAsync(GitLabDocument document, int maxCount = 100, CancellationToken cancellationToken = default)
    {
        var pages = Math.Max(1, (maxCount + 99) / 100);
        var (items, _) = await Client.GetAllPagesAsync(
            $"projects/{document.ProjectId}/repository/commits?ref_name={Uri.EscapeDataString(document.Branch)}&path={Uri.EscapeDataString(document.Path)}",
            pages, cancellationToken);

        return items.Take(maxCount).Select(c => new GitHubCommitInfo
        {
            Sha = c.GetProperty("id").GetString()!,
            Message = GetString(c, "message") ?? GetString(c, "title") ?? string.Empty,
            AuthorName = GetString(c, "author_name") ?? "unknown",
            Date = DateTimeOffset.TryParse(GetString(c, "authored_date") ?? GetString(c, "created_at"), out var date) ? date : default,
            ParentSha = c.TryGetProperty("parent_ids", out var parents) && parents.ValueKind == JsonValueKind.Array && parents.GetArrayLength() > 0
                ? parents[0].GetString()
                : null,
            HtmlUrl = GetString(c, "web_url") ?? $"{document.ServerUrl}/{document.ProjectPath}/-/commit/{c.GetProperty("id").GetString()}",
        }).ToList();
    }

    public async Task<string?> GetFileAtCommitAsync(GitLabDocument document, string commitSha, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await ReadFileAsync(Client, document.ProjectId, document.Path, commitSha, cancellationToken)).Text;
        }
        catch (GitLabApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null; // the file was added after this commit (or renamed)
        }
    }

    public async Task<GitLabCommitResult> CommitFileAsync(GitLabDocument document, string text, string message,
        string? newBranch = null, string? mergeRequestTitle = null, bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Commit message is empty.", nameof(message));

        var client = Client;
        var content = LineEndingNormalizer.Normalize(text, document.LineEnding);
        var onNewBranch = !string.IsNullOrWhiteSpace(newBranch);
        var targetBranch = onNewBranch ? newBranch!.Trim() : document.Branch;
        var action = "update";
        string? lastCommitId = null;

        if (!onNewBranch)
        {
            // Compare against the server before committing: the file may have changed since it was opened.
            RemoteFile? current = null;
            try
            {
                current = await ReadFileAsync(client, document.ProjectId, document.Path, document.Branch, cancellationToken);
            }
            catch (GitLabApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Deleted on the server since it was opened.
            }

            if (overwrite)
            {
                action = current == null ? "create" : "update";
            }
            else
            {
                if (current == null || current.LastCommitId != document.LastCommitId)
                    throw new GitHubConflictException(Loc.Format("GL_Err_Conflict", document.FileName));
                lastCommitId = document.LastCommitId; // also guards the race between the check and the commit
            }
        }

        var fileAction = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["file_path"] = document.Path,
            ["content"] = content,
        };
        if (lastCommitId != null)
            fileAction["last_commit_id"] = lastCommitId;

        var body = new Dictionary<string, object?>
        {
            ["branch"] = targetBranch,
            ["commit_message"] = message,
            ["actions"] = new[] { fileAction },
        };
        if (onNewBranch)
        {
            if (string.IsNullOrEmpty(document.BaseCommitSha))
                body["start_branch"] = document.Branch;
            else
                body["start_sha"] = document.BaseCommitSha;
        }

        JsonElement commit;
        try
        {
            commit = await client.PostAsync($"projects/{document.ProjectId}/repository/commits", body, cancellationToken);
        }
        catch (GitLabApiException ex) when (onNewBranch && ex.ApiMessage?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException(Loc.Format("Err_BranchExists", targetBranch), ex);
        }
        catch (GitLabApiException ex) when (lastCommitId != null && ex.StatusCode == HttpStatusCode.BadRequest
            && ex.ApiMessage?.Contains("changed", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new GitHubConflictException(Loc.Format("GL_Err_Conflict", document.FileName), ex);
        }
        catch (GitLabApiException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(Loc.Format("Err_NoPermission", document.ProjectPath), ex);
        }

        var commitId = commit.GetProperty("id").GetString() ?? string.Empty;
        _loggingService.LogInformation("Committed {Path} to {Project}@{Branch}", document.Path, document.ProjectPath, targetBranch);

        string? mergeRequestUrl = null;
        if (onNewBranch && !string.IsNullOrWhiteSpace(mergeRequestTitle))
        {
            var mr = await client.PostAsync($"projects/{document.ProjectId}/merge_requests", new Dictionary<string, object?>
            {
                ["source_branch"] = targetBranch,
                ["target_branch"] = document.Branch,
                ["title"] = mergeRequestTitle,
            }, cancellationToken);
            mergeRequestUrl = GetString(mr, "web_url");
        }

        return new GitLabCommitResult
        {
            Document = new GitLabDocument
            {
                ServerUrl = document.ServerUrl,
                ProjectId = document.ProjectId,
                ProjectPath = document.ProjectPath,
                Branch = targetBranch,
                Path = document.Path,
                LastCommitId = commitId,
                BaseCommitSha = commitId,
                LineEnding = document.LineEnding,
                CanPush = document.CanPush,
            },
            CommitUrl = GetString(commit, "web_url") ?? $"{document.ServerUrl}/{document.ProjectPath}/-/commit/{commitId}",
            MergeRequestUrl = mergeRequestUrl,
        };
    }

    private sealed record RemoteFile(string Text, string LastCommitId);

    private static async Task<RemoteFile> ReadFileAsync(GitLabApiClient client, long projectId, string path, string reference, CancellationToken cancellationToken)
    {
        var file = await client.GetAsync(
            $"projects/{projectId}/repository/files/{GitLabApiClient.EscapeFilePath(path)}?ref={Uri.EscapeDataString(reference)}", cancellationToken);

        var encoded = GetString(file, "content") ?? string.Empty;
        var isBase64 = !file.TryGetProperty("encoding", out var enc) || enc.GetString() == "base64";
        var text = isBase64 ? Encoding.UTF8.GetString(Convert.FromBase64String(encoded)) : encoded;

        return new RemoteFile(text.TrimStart('﻿'), GetString(file, "last_commit_id") ?? string.Empty);
    }

    private static GitLabProject ParseProject(JsonElement p, bool isAdmin)
    {
        var access = Math.Max(AccessLevel(p, "project_access"), AccessLevel(p, "group_access"));
        var archived = p.TryGetProperty("archived", out var a) && a.ValueKind == JsonValueKind.True;

        return new GitLabProject
        {
            Id = p.GetProperty("id").GetInt64(),
            PathWithNamespace = p.GetProperty("path_with_namespace").GetString()!,
            Name = GetString(p, "name") ?? string.Empty,
            DefaultBranch = GetString(p, "default_branch") ?? "main",
            Description = GetString(p, "description"),
            IsPrivate = GetString(p, "visibility") != "public",
            CanPush = !archived && (isAdmin || access >= DeveloperAccessLevel),
            UpdatedAt = DateTimeOffset.TryParse(GetString(p, "last_activity_at"), out var updated) ? updated : null,
            WebUrl = GetString(p, "web_url") ?? string.Empty,
        };
    }

    private static int AccessLevel(JsonElement project, string kind)
    {
        if (project.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Object
            && perms.TryGetProperty(kind, out var entry) && entry.ValueKind == JsonValueKind.Object
            && entry.TryGetProperty("access_level", out var level) && level.ValueKind == JsonValueKind.Number)
        {
            return level.GetInt32();
        }

        return 0;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
