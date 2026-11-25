using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Services;
using AirPodsWinTray.UI;
using Microsoft.UI.Xaml;

namespace AirPodsWinTray;

public partial class App : Application
{
    private OverlayWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var config = AppConfig.Load();
        StartupRegistration.Apply(config.StartWithWindows);
        _window = new OverlayWindow(config);
        _window.Activate();
    }
}
