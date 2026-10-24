namespace MDEditor.Core.Models;

/// <summary>
/// The code the user enters at <see cref="VerificationUri"/> to approve a device-flow sign-in.
/// </summary>
public class GitHubDeviceCode
{
    public string UserCode { get; init; } = string.Empty;
    public string VerificationUri { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// The raw Octokit response, needed to poll for the token. Opaque to callers.
    /// </summary>
    internal Octokit.OauthDeviceFlowResponse Response { get; init; } = null!;
}
