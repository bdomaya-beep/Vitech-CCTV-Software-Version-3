using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SentinelVMS.Application.Configuration;
using SentinelVMS.Application.DependencyInjection;
using SentinelVMS.Infrastructure.Data;
using SentinelVMS.Infrastructure.DependencyInjection;
using SentinelVMS.Networking.DependencyInjection;
using SentinelVMS.Rendering.DependencyInjection;
using SentinelVMS.Streaming.DependencyInjection;
using SentinelVMS.Streaming.FFmpeg;
using SentinelVMS.Presentation.Application;
using SentinelVMS.Presentation.Core;
using SentinelVMS.Presentation.ViewModels;
using SentinelVMS.Presentation.Views;
using SentinelVMS.Rendering.Core;
using SentinelVMS.Rendering.Wpf;
using SentinelVMS.Streaming.Core;
using SentinelVMS.Streaming.Pipeline;

namespace SentinelVMS.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            System.IO.File.WriteAllText(logPath, $"Startup Error:\n{ex}\n\nInner: {ex.InnerException}");
            
            MessageBox.Show($"Application startup failed:\n\n{ex.Message}\n\nDetails logged to: {logPath}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task InitializeAsync()
    {
        // Keep the process alive while switching from login dialog to main window.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialize FFmpeg (optional - app can work without it)
        FfmpegBootstrapper.Initialize(AppDomain.CurrentDomain.BaseDirectory);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddApplication();
                services.AddInfrastructureServices(new AppDatabaseOptions
                {
                    Provider = DatabaseProvider.Sqlite,
                    ConnectionString = "Data Source=sentinel-vms.db"
                });
                services.AddNetworkingServices();
                services.AddStreamingServices();
                services.AddRenderingServices();

                // Register IFrameSink with D3DImageFrameSink
                services.AddSingleton<IFrameSink, D3DImageFrameSink>();

                services.AddSingleton<ShellState>();
                services.AddSingleton<INavigationService, NavigationService>();

                services.AddTransient<LoginViewModel>();
                services.AddTransient<LiveViewViewModel>();
                services.AddTransient<DeviceManagementViewModel>();
                services.AddTransient<PlaybackViewModel>();
                services.AddTransient<ShellViewModel>();

                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        try
        {
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Host startup failed: {ex.Message}", ex);
        }

        try
        {
            using var scope = _host.Services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Database initialization failed: {ex.Message}", ex);
        }

        try
        {
            var renderer = _host.Services.GetRequiredService<IDirectXRenderer>();
            renderer.Initialize();
        }
        catch (Exception ex)
        {
            throw new Exception($"DirectX renderer initialization failed: {ex.Message}", ex);
        }

        var login = _host.Services.GetRequiredService<LoginWindow>();
        var loginSucceeded = login.ShowDialog() == true;
        if (!loginSucceeded)
        {
            Shutdown();
            return;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}


