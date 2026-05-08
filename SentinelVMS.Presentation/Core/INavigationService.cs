namespace SentinelVMS.Presentation.Core;

public interface INavigationService
{
    object? CurrentViewModel { get; }
    void Navigate(object viewModel);
}
