using CommunityToolkit.Mvvm.ComponentModel;

namespace SentinelVMS.Presentation.Core;

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    [ObservableProperty]
    private object? _currentViewModel;

    public void Navigate(object viewModel)
    {
        CurrentViewModel = viewModel;
    }
}
