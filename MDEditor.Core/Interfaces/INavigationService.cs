namespace MDEditor.Core.Interfaces;

/// <summary>
/// Service for navigation between views in the application.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the specified view model.
    /// </summary>
    void NavigateTo<TViewModel>() where TViewModel : IViewModel;

    /// <summary>
    /// Navigates back to the previous view.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Gets a value indicating whether there is a previous view to navigate back to.
    /// </summary>
    bool CanGoBack { get; }
}
