using System.Collections.Generic;
using MDEditor.Core.Interfaces;

namespace MDEditor.Core.Services;

/// <summary>
/// In-memory navigation service for navigating between ViewModels.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly Stack<string> _backStack = new();
    private string? _currentViewModel;

    public bool CanGoBack => _backStack.Count > 0;

    public void NavigateTo<TViewModel>() where TViewModel : IViewModel
    {
        var viewModelName = typeof(TViewModel).Name;

        if (!string.IsNullOrEmpty(_currentViewModel))
        {
            _backStack.Push(_currentViewModel);
        }

        _currentViewModel = viewModelName;
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        _currentViewModel = _backStack.Pop();
    }

    public string? GetCurrentViewModel() => _currentViewModel;
}
