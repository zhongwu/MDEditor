using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MDEditor.Core.Interfaces;
using MDEditor.Core.Services;

namespace MDEditor.Core;

/// <summary>
/// Extension methods for registering MDEditor core services in the DI container.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers all MDEditor core services.
    /// </summary>
    public static IServiceCollection AddMDEditorCore(this IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        // Core services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IMarkdownParser, MarkdownParser>();

        services.AddSingleton<IUpdateService, UpdateService>();

        // GitHub (Phase 5)
        services.AddSingleton<ITokenStore, CredentialManagerTokenStore>();
        services.AddSingleton<IGitHubAuthService>(sp => new GitHubAuthService(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<ILoggingService>()));
        services.AddSingleton<IGitHubRepositoryService, GitHubRepositoryService>();

        // GitLab (Phase 5.7): gitlab.com and self-hosted servers
        services.AddSingleton<IGitLabAuthService>(sp => new GitLabAuthService(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<ILoggingService>()));
        services.AddSingleton<IGitLabRepositoryService, GitLabRepositoryService>();
        services.AddSingleton<IGitHubGistService>(sp => new GitHubGistService(
            sp.GetRequiredService<IGitHubAuthService>(),
            sp.GetRequiredService<ILoggingService>()));

        return services;
    }
}
