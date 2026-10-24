using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MDEditor.Core;
using MDEditor.Core.Interfaces;
using MDEditor.Themes;
using MDEditor.ViewModels;

namespace MDEditor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IHost? _host;

    /// <summary>
    /// Gets the shared service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build the dependency injection container
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddMDEditorCore();
                services.AddMDEditorViewModels();
            })
            .Build();

        Services = _host.Services;

        // Apply the persisted theme before MainWindow is constructed (StartupUri creates it
        // after OnStartup returns), so the window's DynamicResource bindings resolve themed
        // values from the first paint.
        var settingsService = Services.GetRequiredService<ISettingsService>();
        ThemeService.Apply(settingsService.Get("appearance.theme", "System"));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}