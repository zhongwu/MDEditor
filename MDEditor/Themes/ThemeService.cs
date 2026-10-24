using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace MDEditor.Themes;

/// <summary>
/// Resolves and applies the app's Light/Dark/System theme: swaps the merged resource
/// dictionary so every existing DynamicResource consumer updates automatically, and notifies
/// non-XAML painters (LineNumberGutter, MinimapControl, CompletionPopup, the preview pane)
/// that can't consume DynamicResource directly.
/// </summary>
public static class ThemeService
{
    /// <summary>Whether the currently-applied theme is dark (after resolving "System").</summary>
    public static bool IsDark { get; private set; }

    /// <summary>Raised after the theme has been applied, with the resolved dark/light state.</summary>
    public static event Action<bool>? ThemeChanged;

    /// <summary>Applies "Light", "Dark", or "System" (resolved from the Windows app theme setting).</summary>
    public static void Apply(string themeSetting)
    {
        bool isDark = themeSetting switch
        {
            "Dark" => true,
            "Light" => false,
            _ => ResolveSystemIsDark(),
        };

        IsDark = isDark;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(dictionary);

        ThemeChanged?.Invoke(isDark);
    }

    // Read once at launch/selection rather than subscribing to live OS theme-change
    // notifications (SystemEvents.UserPreferenceChanged), which would need an explicit
    // unsubscribe in App.OnExit to avoid a classic WinForms-interop leak - not worth it for v1.
    private static bool ResolveSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme)
                return appsUseLightTheme == 0;
        }
        catch
        {
            // Registry unavailable/denied - fall back to light.
        }

        return false;
    }

    private const int DwmwaUseImmersiveDarkMode = 20; // Windows 10 20H1+ / Windows 11
    private const int DwmwaUseImmersiveDarkModeOld = 19; // earlier Windows 10 20H1 builds

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Colors the window's non-client title bar to match the theme (best-effort; silently
    /// no-ops on Windows versions that don't support it).</summary>
    public static void ApplyTitleBar(Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int useDark = isDark ? 1 : 0;
            int hr = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
            if (hr != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeOld, ref useDark, sizeof(int));
        }
        catch
        {
            // Not supported on this Windows version - degrade gracefully (light title bar).
        }
    }
}
