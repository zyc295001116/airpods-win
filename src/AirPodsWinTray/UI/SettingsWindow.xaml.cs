using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace AirPodsWinTray.UI;

public sealed partial class SettingsWindow : Window
{
    public const int WidthDip = 240;
    public const int HeightDip = 208;

    private readonly AppWindow _appWindow;
    private bool _shown;
    private bool _syncing;

    public SettingsWindow()
    {
        InitializeComponent();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.IsShownInSwitchers = false;

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        _appWindow.SetPresenter(presenter);
        HideFromTaskbar(hwnd);

        OpacitySlider.ValueChanged += (_, _) =>
        {
            if (!_syncing)
            {
                OpacityChanged?.Invoke(OpacitySlider.Value);
            }
        };
        Activated += (_, e) =>
        {
            if (_shown && e.WindowActivationState == WindowActivationState.Deactivated)
            {
                HideMenu();
            }
        };
        RootKey.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HideMenu();
            }
        };
        _appWindow.Hide();
    }

    public event Action? RefreshRequested;

    public event Action? ToggleSizeRequested;

    public event Action? ExitRequested;

    public event Action<double>? OpacityChanged;

    public event Action? Dismissed;

    public bool IsOpen => _shown;

    public void SetCompact(bool compact) => SizeToggle.Content = compact ? "大窗" : "小窗";

    public void SetOpacity(double value)
    {
        _syncing = true;
        OpacitySlider.Value = Math.Clamp(value, 0.25, 1);
        _syncing = false;
    }

    public void Resize(int width, int height) => _appWindow.Resize(new SizeInt32(width, height));

    public void ShowAt(int x, int y)
    {
        _appWindow.Move(new PointInt32(x, y));
        _shown = true;
        Activate();
        RootKey.Focus(FocusState.Programmatic);
    }

    private void HideMenu()
    {
        if (!_shown)
        {
            return;
        }

        _shown = false;
        _appWindow.Hide();
        Dismissed?.Invoke();
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        HideMenu();
        RefreshRequested?.Invoke();
    }

    private void OnToggleSize(object sender, RoutedEventArgs e)
    {
        HideMenu();
        ToggleSizeRequested?.Invoke();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        HideMenu();
        ExitRequested?.Invoke();
    }

    private static void HideFromTaskbar(nint hwnd)
    {
        const int styleIndex = -20;
        const int tool = 0x00000080;
        const int app = 0x00040000;
        var style = GetWindowLong(hwnd, styleIndex);
        SetWindowLong(hwnd, styleIndex, (style | tool) & ~app);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hwnd, int index, int value);
}
