using Microsoft.Extensions.DependencyInjection;
using MDEditor.ViewModels;

namespace MDEditor;

/// <summary>
/// Extension methods for registering MDEditor ViewModels in the DI container.
/// </summary>
public static class ViewModelServiceCollectionExtensions
{
    /// <summary>
    /// Registers all MDEditor ViewModels.
    /// </summary>
    public static IServiceCollection AddMDEditorViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}