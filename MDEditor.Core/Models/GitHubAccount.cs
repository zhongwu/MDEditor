namespace MDEditor.Core.Models;

/// <summary>
/// How a GitHub account was connected.
/// </summary>
public enum GitHubAuthMethod
{
    /// <summary>OAuth device flow (sign in with browser).</summary>
    OAuthDevice,

    /// <summary>Personal Access Token pasted by the user.</summary>
    PersonalAccessToken,
}

/// <summary>
/// Non-secret metadata for a connected GitHub account. The access token itself lives in
/// <see cref="Interfaces.ITokenStore"/>, never in settings.json.
/// </summary>
public class GitHubAccount
{
    public string Login { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public GitHubAuthMethod AuthMethod { get; set; }

    /// <summary>
    /// OAuth scopes reported by GitHub for the token. Empty for fine-grained PATs, which
    /// don't advertise scopes.
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    public DateTimeOffset AddedAt { get; set; }
}
