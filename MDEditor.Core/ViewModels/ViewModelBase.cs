using CommunityToolkit.Mvvm.ComponentModel;
using MDEditor.Core.Interfaces;

namespace MDEditor.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels using CommunityToolkit.Mvvm ObservableObject.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IViewModel
{
    protected ViewModelBase()
    {
    }
}

/// <summary>
/// Base class for ViewModels that support data validation using CommunityToolkit.Mvvm ObservableValidator.
/// </summary>
public abstract class ValidatableViewModelBase : ObservableValidator, IViewModel
{
    protected ValidatableViewModelBase()
    {
    }
}
