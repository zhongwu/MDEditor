namespace MDEditor.Core.Interfaces;

/// <summary>
/// Service for reading and writing application settings persisted to disk.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value, returning the default if not found.
    /// </summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// Removes a setting value.
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Saves all pending changes to disk.
    /// </summary>
    void Save();
}
