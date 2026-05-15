using CommunityToolkit.Mvvm.Input;
using SentinelVMS.Presentation.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SentinelVMS.Presentation.ViewModels;

public partial class DetachedLiveViewViewModel : ViewModelBase, IDisposable
{
    private readonly ShellViewModel _shellViewModel;

    public DetachedLiveViewViewModel(ShellViewModel shellViewModel, LiveViewViewModel liveViewModel)
    {
        _shellViewModel = shellViewModel;
        LiveViewModel = liveViewModel;
        _shellViewModel.PropertyChanged += OnShellPropertyChanged;
    }

    public LiveViewViewModel LiveViewModel { get; }

    public ObservableCollection<DeviceTreeItemViewModel> DeviceTree => _shellViewModel.DeviceTree;

    public int ConnectedDeviceCount => _shellViewModel.ConnectedDeviceCount;

    public string MetricsText => _shellViewModel.MetricsText;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LiveViewModel.ReloadAsync();
    }

    [RelayCommand]
    private void SetGridLayout(string? layoutStr)
    {
        if (!int.TryParse(layoutStr, out var tileCount) || tileCount <= 0)
        {
            return;
        }

        var gridSize = (int)Math.Ceiling(Math.Sqrt(tileCount));
        LiveViewModel.SetGridLayout(gridSize);
    }

    public void Dispose()
    {
        _shellViewModel.PropertyChanged -= OnShellPropertyChanged;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ConnectedDeviceCount))
        {
            OnPropertyChanged(nameof(ConnectedDeviceCount));
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.MetricsText))
        {
            OnPropertyChanged(nameof(MetricsText));
        }
    }
}
