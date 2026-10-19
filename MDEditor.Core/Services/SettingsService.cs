using System.Text.Json;
using MDEditor.Core.Interfaces;

namespace MDEditor.Core.Services;

/// <summary>
/// Service for reading and writing application settings persisted to disk using Windows.Storage.ApplicationData.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly Windows.Storage.ApplicationDataContainer _localSettings;
    private readonly Dictionary<string, object> _cache = new();

    public SettingsService()
    {
        _localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
    }

    /// <summary>
    /// Gets a setting value, returning the default if not found.
    /// </summary>
    public T Get<T>(string key, T defaultValue)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            return (T)cachedValue;
        }

        if (_localSettings.Values.TryGetValue(key, out var storedValue))
        {
            try
            {
                var json = storedValue as string ?? storedValue?.ToString() ?? string.Empty;
                var deserialized = JsonSerializer.Deserialize<T>(json) ?? defaultValue;
                _cache[key] = deserialized!;
                return deserialized;
            }
            catch
            {
                return defaultValue;
            }
        }

        _cache[key] = defaultValue!;
        return defaultValue;
    }

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        _localSettings.Values[key] = json;
        _cache[key] = value!;
    }

    /// <summary>
    /// Removes a setting value.
    /// </summary>
    public void Remove(string key)
    {
        _localSettings.Values.Remove(key);
        _cache.Remove(key);
    }

    /// <summary>
    /// Saves all pending changes to disk.
    /// </summary>
    public void Save()
    {
        // ApplicationData saves automatically, but this method exists for future batch operations.
    }
}
