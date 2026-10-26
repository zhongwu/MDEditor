using System.Text.RegularExpressions;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Models;
using Octokit;

namespace MDEditor.Core.Services;

/// <summary>
/// Update check against the repository's latest GitHub release (anonymous, read-only).
/// Pre-releases and drafts are never offered: GitHub's "latest" endpoint excludes them.
/// </summary>
public class UpdateService : IUpdateService
{
    public const string DefaultOwner = "zhongwu";
    public const string DefaultRepository = "MDEditor";

    private static readonly Regex VersionPattern = new(@"^v?(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

    private readonly IReleasesClient _releases;
    private readonly string _owner;
    private readonly string _repository;

    public UpdateService()
        : this(new GitHubClient(new ProductHeaderValue("MDEditor")).Repository.Release)
    {
    }

    public UpdateService(IReleasesClient releases, string owner = DefaultOwner, string repository = DefaultRepository)
    {
        _releases = releases;
        _owner = owner;
        _repository = repository;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        Release release;
        try
        {
            release = await _releases.GetLatest(_owner, _repository).WaitAsync(cancellationToken);
        }
        catch (NotFoundException)
        {
            return null; // no releases published yet
        }

        if (release.Draft || release.Prerelease || !TryParseVersion(release.TagName, out var latest))
        {
            return null;
        }

        if (latest <= Normalize(currentVersion))
        {
            return null;
        }

        return new UpdateInfo
        {
            Version = latest,
            TagName = release.TagName,
            Name = release.Name,
            Notes = release.Body,
            HtmlUrl = release.HtmlUrl,
            PublishedAt = release.PublishedAt,
        };
    }

    /// <summary>Parses tags such as "v1.2.3", "1.2" or "v1.2.3-beta" (suffix ignored) into a 3-part version.</summary>
    public static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var m = VersionPattern.Match(tag.Trim());
        if (!m.Success)
        {
            return false;
        }

        version = new Version(
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value),
            m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
        return true;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
