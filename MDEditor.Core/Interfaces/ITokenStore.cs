namespace MDEditor.Core.Interfaces;

/// <summary>
/// Secure per-user storage for secrets such as access tokens.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Stores (or replaces) the secret for <paramref name="key"/>.
    /// </summary>
    void Save(string key, string secret);

    /// <summary>
    /// Gets the secret for <paramref name="key"/>, or null if none is stored.
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Deletes the secret for <paramref name="key"/>. No-op if none is stored.
    /// </summary>
    void Delete(string key);
}
