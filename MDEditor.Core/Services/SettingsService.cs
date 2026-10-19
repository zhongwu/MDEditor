using System;
using System.IO;
using System.Text.Json;
using MDEditor.Core.Interfaces;

namespace MDEditor.Core.Services;

/// <summary>
/// Service for reading and writing application settings persisted to a JSON file.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly Dictionary<string, object> _settings = new();

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "MDEditor");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");

        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        _settings[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch
        {
            // Ignore loading errors, start with empty settings
        }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Ignore saving errors
        }
    }

    /// <summary>
    /// Gets a setting value, returning the default if not found.
    /// </summary>
    public T Get<T>(string key, T defaultValue)
    {
        if (_settings.TryGetValue(key, out var storedValue))
        {
            try
            {
                if (storedValue is JsonElement element)
                {
                    var deserialized = element.Deserialize<T>();
                    return deserialized ?? defaultValue;
                }
                return (T)storedValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    public void Set<T>(string key, T value)
    {
        _settings[key] = value!;
        SaveSettings();
    }

    /// <summary>
    /// Removes a setting value.
    /// </summary>
    public void Remove(string key)
    {
        _settings.Remove(key);
        SaveSettings();
    }

    /// <summary>
    /// Saves all pending changes to disk.
    /// </summary>
    public void Save()
    {
        SaveSettings();
    }
}
