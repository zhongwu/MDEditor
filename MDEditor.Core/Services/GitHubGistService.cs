using System.Text.RegularExpressions;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Localization;
using MDEditor.Core.Models;
using Octokit;

namespace MDEditor.Core.Services;

/// <summary>
/// <see cref="IGitHubGistService"/> over Octokit, using the active account from
/// <see cref="IGitHubAuthService"/>.
/// </summary>
public partial class GitHubGistService : IGitHubGistService
{
    private static readonly HttpClient RawClient = new();

    private readonly IGitHubAuthService _authService;
    private readonly ILoggingService _loggingService;
    private readonly Func<string, Task<string>> _downloadRaw;

    public GitHubGistService(IGitHubAuthService authService, ILoggingService loggingService)
        : this(authService, loggingService, url => RawClient.GetStringAsync(url))
    {
    }

    /// <param name="downloadRaw">Fetches a raw file URL (used when the API truncates large files).</param>
    public GitHubGistService(IGitHubAuthService authService, ILoggingService loggingService, Func<string, Task<string>> downloadRaw)
    {
        _authService = authService;
        _loggingService = loggingService;
        _downloadRaw = downloadRaw;
    }

    [GeneratedRegex("^[0-9a-fA-F]{7,40}$")]
    private static partial Regex GistIdPattern();

    /// <summary>
    /// Extracts a gist ID from a bare ID or a gist URL (gist.github.com/[user/]id[#file],
    /// gist.githubusercontent.com/user/id/raw/...). Returns null if it isn't one.
    /// </summary>
    public static string? ParseGistId(string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (GistIdPattern().IsMatch(text))
            return text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? candidate = uri.Host.ToLowerInvariant() switch
        {
            "gist.github.com" => segments.LastOrDefault(),
            "gist.githubusercontent.com" => segments.Length >= 2 ? segments[1] : null,
            _ => null,
        };

        if (candidate != null && candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[..^4];

        return candidate != null && GistIdPattern().IsMatch(candidate) ? candidate : null;
    }

    private IGitHubClient Client =>
        _authService.CreateClient() ?? throw new InvalidOperationException(Loc.Get("Err_SignInFirst"));

    /// <summary>Runs a GitHub call, turning a 401 (revoked/expired token) into a message that says what to do.</summary>
    private static async Task<T> GuardAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AuthorizationException ex) when (ex is not TwoFactorRequiredException)
        {
            throw new InvalidOperationException(Loc.Get("Err_SignInExpired"), ex);
        }
    }

    private bool IsMine(Gist gist) =>
        gist.Owner?.Login is { } owner && string.Equals(owner, _authService.ActiveAccount?.Login, StringComparison.OrdinalIgnoreCase);

    private static GitHubGistInfo ToInfo(Gist gist) => new()
    {
        Id = gist.Id,
        Description = gist.Description,
        IsPublic = gist.Public,
        Owner = gist.Owner?.Login ?? string.Empty,
        UpdatedAt = gist.UpdatedAt,
        FileNames = gist.Files?.Keys.ToList() ?? new List<string>(),
        HtmlUrl = gist.HtmlUrl,
    };

    public async Task<IReadOnlyList<GitHubGistInfo>> GetMyGistsAsync(CancellationToken cancellationToken = default)
    {
        var client = Client;
        var gists = await GuardAsync(() => client.Gist.GetAll());
        cancellationToken.ThrowIfCancellationRequested();
        return gists.OrderByDescending(g => g.UpdatedAt).Select(ToInfo).ToList();
    }

    public async Task<GitHubGistInfo> GetGistAsync(string gistId, CancellationToken cancellationToken = default)
    {
        var gist = await GetRawGistAsync(gistId);
        cancellationToken.ThrowIfCancellationRequested();
        return ToInfo(gist);
    }

    private async Task<Gist> GetRawGistAsync(string gistId)
    {
        try
        {
            var client = Client;
            return await GuardAsync(() => client.Gist.Get(gistId));
        }
        catch (NotFoundException ex)
        {
            throw new InvalidOperationException(Loc.Format("Err_GistNotFound", gistId), ex);
        }
    }

    public async Task<GitHubGistFileContent> GetFileAsync(string gistId, string fileName, CancellationToken cancellationToken = default)
    {
        var gist = await GetRawGistAsync(gistId);
        if (gist.Files == null || !gist.Files.TryGetValue(fileName, out var file))
            throw new InvalidOperationException(Loc.Format("Err_GistFileNotFound", fileName));

        // The API truncates file content beyond ~1 MB; the raw URL always has all of it.
        var text = file.Content;
        if (string.IsNullOrEmpty(text) && file.Size > 0 && !string.IsNullOrEmpty(file.RawUrl))
            text = await _downloadRaw(file.RawUrl);
        text = (text ?? string.Empty).TrimStart('﻿');
        cancellationToken.ThrowIfCancellationRequested();

        _loggingService.LogInformation("Opened gist {GistId} file {File}", gistId, fileName);

        return new GitHubGistFileContent
        {
            Text = text,
            Document = new GitHubGistDocument
            {
                GistId = gist.Id,
                FileName = fileName,
                Description = gist.Description,
                IsPublic = gist.Public,
                Owner = gist.Owner?.Login ?? string.Empty,
                HtmlUrl = gist.HtmlUrl,
                UpdatedAt = gist.UpdatedAt,
                LineEnding = GitHubRepositoryService.DetectLineEnding(text),
                CanEdit = IsMine(gist),
            },
        };
    }

    public async Task<GitHubGistDocument> CreateAsync(string fileName, string text, string? description, bool isPublic, CancellationToken cancellationToken = default)
    {
        fileName = fileName.Trim();
        if (fileName.Length == 0 || fileName.Contains('/') || fileName.Contains('\\'))
            throw new ArgumentException(Loc.Get("Err_GistFileName"), nameof(fileName));

        var lineEnding = GitHubRepositoryService.DetectLineEnding(text);
        var newGist = new NewGist { Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(), Public = isPublic };
        newGist.Files.Add(fileName, text);

        Gist gist;
        try
        {
            var client = Client;
            gist = await GuardAsync(() => client.Gist.Create(newGist));
        }
        catch (NotFoundException ex)
        {
            // GitHub answers 404 when the token lacks the gist scope.
            throw new InvalidOperationException(Loc.Get("Err_GistScope"), ex);
        }
        cancellationToken.ThrowIfCancellationRequested();

        _loggingService.LogInformation("Created {Visibility} gist {GistId}", isPublic ? "public" : "secret", gist.Id);

        return new GitHubGistDocument
        {
            GistId = gist.Id,
            FileName = fileName,
            Description = gist.Description,
            IsPublic = gist.Public,
            Owner = gist.Owner?.Login ?? _authService.ActiveAccount?.Login ?? string.Empty,
            HtmlUrl = gist.HtmlUrl,
            UpdatedAt = gist.UpdatedAt,
            LineEnding = lineEnding,
            CanEdit = true,
        };
    }

    public async Task<GitHubGistDocument> UpdateAsync(GitHubGistDocument document, string text, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var client = Client;

        if (!overwrite)
        {
            // Gists have no per-file SHA, so compare the gist's update time with the opened one.
            var current = await GetRawGistAsync(document.GistId);
            if (current.UpdatedAt > document.UpdatedAt.AddSeconds(1))
                throw new GitHubConflictException(Loc.Format("Err_GistConflict", document.FileName));
        }

        var update = new GistUpdate();
        update.Files.Add(document.FileName, new GistFileUpdate { Content = LineEndingNormalizer.Normalize(text, document.LineEnding) });

        Gist gist;
        try
        {
            gist = await GuardAsync(() => client.Gist.Edit(document.GistId, update));
        }
        catch (NotFoundException ex)
        {
            throw new InvalidOperationException(Loc.Get("Err_GistScope"), ex);
        }
        cancellationToken.ThrowIfCancellationRequested();

        _loggingService.LogInformation("Updated gist {GistId} file {File}", document.GistId, document.FileName);

        return new GitHubGistDocument
        {
            GistId = document.GistId,
            FileName = document.FileName,
            Description = gist.Description,
            IsPublic = gist.Public,
            Owner = document.Owner,
            HtmlUrl = gist.HtmlUrl ?? document.HtmlUrl,
            UpdatedAt = gist.UpdatedAt,
            LineEnding = document.LineEnding,
            CanEdit = document.CanEdit,
        };
    }

    public async Task<IReadOnlyList<GitHubGistRevisionInfo>> GetHistoryAsync(GitHubGistDocument document, CancellationToken cancellationToken = default)
    {
        var client = Client;
        var history = await GuardAsync(() => client.Gist.GetAllCommits(document.GistId));
        cancellationToken.ThrowIfCancellationRequested();

        var ordered = history.OrderByDescending(h => h.CommittedAt).ToList();
        return ordered.Select((h, i) => new GitHubGistRevisionInfo
        {
            Sha = h.Version,
            AuthorLogin = h.User?.Login,
            CommittedAt = h.CommittedAt,
            Additions = h.ChangeStatus?.Additions ?? 0,
            Deletions = h.ChangeStatus?.Deletions ?? 0,
            PreviousSha = i + 1 < ordered.Count ? ordered[i + 1].Version : null,
            HtmlUrl = $"https://gist.github.com/{document.Owner}/{document.GistId}/{h.Version}",
        }).ToList();
    }

    public async Task<string?> GetFileAtRevisionAsync(GitHubGistDocument document, string sha, CancellationToken cancellationToken = default)
    {
        var client = Client;
        Gist revision;
        try
        {
            // Octokit's IGistsClient has no Get(id, sha) overload for a specific historical
            // revision, so this hits GitHub's GET /gists/{gist_id}/{sha} directly.
            var response = await GuardAsync(() => client.Connection.Get<Gist>(
                new Uri($"gists/{document.GistId}/{sha}", UriKind.Relative), new Dictionary<string, string>()));
            revision = response.Body;
        }
        catch (NotFoundException)
        {
            return null;
        }
        cancellationToken.ThrowIfCancellationRequested();

        if (revision.Files == null || !revision.Files.TryGetValue(document.FileName, out var file))
            return null; // the file didn't exist (or had a different name) at this revision

        var text = file.Content;
        if (string.IsNullOrEmpty(text) && file.Size > 0 && !string.IsNullOrEmpty(file.RawUrl))
            text = await _downloadRaw(file.RawUrl);
        return (text ?? string.Empty).TrimStart('﻿');
    }
}
