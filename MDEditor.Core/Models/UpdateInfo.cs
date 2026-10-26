namespace MDEditor.Core.Models;

/// <summary>A published release that is newer than the running version.</summary>
public sealed class UpdateInfo
{
    public required Version Version { get; init; }
    public required string TagName { get; init; }
    public string? Name { get; init; }
    public string? Notes { get; init; }
    public required string HtmlUrl { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
}
