using System.Numerics;
using WinRT;
using System.Runtime.InteropServices;
using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Policy;
using AirPodsWinTray.Services;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace AirPodsWinTray.UI;

public sealed partial class OverlayWindow : Window
{
    private const int LargeWidthDip = 320;
    private const int LargeHeightDip = 300;
    private const int CompactWidthDip = 300;
    private const int CompactHeightDip = 72;
    private const int StripPx = 12;
    private const int SnapPx = 24;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;

    private static readonly Dictionary<nint, OverlayWindow> Subclassed = new();
    private static readonly SubclassProc SubclassDelegate = Subclass;

    private readonly AppConfig _config;
    private readonly HeadsetSession _session;
    private readonly HotKeyManager _hotkeys = new();
    private readonly AppWindow _appWindow;
    private DesktopAcrylicController? _acrylic;
    private SystemBackdropConfiguration? _backdrop;
    private bool _backdropAttached;
    private bool _playedIntro;
    private bool _closing;
    private bool _dragging;
    private bool _collapsed;
    private bool _syncingUi;
    private DockEdge _dock = DockEdge.None;
    private PointInt32 _dragWindowOrigin;
    private POINT _dragCursorOrigin;
    private readonly SettingsWindow _settings = new();
    private DispatcherQueueTimer? _collapseTimer;

    public OverlayWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        _session = new HeadsetSession(config, DispatcherQueue);
        _session.Changed += Render;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Title = "AirPods";
        _appWindow.IsShownInSwitchers = false;

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        _appWindow.SetPresenter(presenter);

        var width = DipToPx(hwnd, _config.OverlayCompact ? CompactWidthDip : LargeWidthDip);
        var height = DipToPx(hwnd, _config.OverlayCompact ? CompactHeightDip : LargeHeightDip);
        _appWindow.Resize(new SizeInt32(width, height));
        HideFromTaskbar(hwnd);
        Place(width, height);
        _dock = ParseDock(_config.OverlayDock);
        if (_dock != DockEdge.None)
        {
            Collapse();
        }

        _collapseTimer = DispatcherQueue.CreateTimer();
        _collapseTimer.Interval = TimeSpan.FromMilliseconds(300);
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (_dock == DockEdge.None || _dragging || _settings.IsOpen || _collapsed)
            {
                return;
            }

            if (CursorInsideWindow())
            {
                _collapseTimer.Start();
                return;
            }

            Collapse();
        };

        Subclassed[hwnd] = this;
        SetWindowSubclass(hwnd, SubclassDelegate, 1, 0);
        TryAcrylic();

        _settings.RefreshRequested += () => _session.RefreshDevices();
        _settings.ToggleSizeRequested += ToggleSize;
        _settings.ExitRequested += Close;
        _settings.OpacityChanged += value =>
        {
            _config.OverlayOpacity = value;
            _config.Save();
            ApplyOpacity();
        };
        _settings.Dismissed += OnMenuDismissed;
        _settings.SetOpacity(_config.OverlayOpacity);
        Activated += OnActivated;
        Closed += OnClosed;
        _session.Start();
        _hotkeys.Register(hwnd, _config.ConnectHotkey, () => DispatcherQueue.TryEnqueue(_session.Connect));
        _hotkeys.Register(hwnd, _config.DisconnectHotkey, () => DispatcherQueue.TryEnqueue(_session.ReleaseToPhone));
        _hotkeys.Register(hwnd, _config.ReleaseHotkey, () => DispatcherQueue.TryEnqueue(_session.ReleaseToPhone));
    }

    private void Card_Loaded(object sender, RoutedEventArgs e)
    {
        LeftPod.SetCaption("左");
        RightPod.SetCaption("右");
        CasePod.SetCaption("盒");
        LeftPod.Translation = new Vector3(0, 0, 0);
        RightPod.Translation = new Vector3(96, 0, 0);
        CasePod.Translation = new Vector3(192, 0, 0);
        ApplyOpacity();
        ApplySize(resize: false);
        Render();
        PlayIntro();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_backdrop == null)
        {
            return;
        }

        _backdrop.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
    }

    private void Render()
    {
        var unbound = _session.Picker == AirPodsPickerMode.None;
        var multiple = _session.Picker == AirPodsPickerMode.Multiple;
        var compact = _config.OverlayCompact && !unbound;
        EmptyPanel.Visibility = unbound ? Visibility.Visible : Visibility.Collapsed;
        LargeBody.Visibility = unbound || compact ? Visibility.Collapsed : Visibility.Visible;
        CompactBar.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        _settings.SetCompact(_config.OverlayCompact);

        if (unbound)
        {
            return;
        }

        var link = _session.Link;
        var title = _session.ModelTitle;
        var linkLabel = ShortLink(link);
        ModelName.Text = title;
        CompactName.Text = title;
        LinkText.Text = linkLabel;
        CompactLink.Text = linkLabel;
        DeviceCombo.Visibility = multiple ? Visibility.Visible : Visibility.Collapsed;
        ModelName.Visibility = multiple ? Visibility.Collapsed : Visibility.Visible;

        _syncingUi = true;
        DeviceCombo.ItemsSource = _session.AirPods;
        DeviceCombo.SelectedItem = _session.AirPods.FirstOrDefault(device => device.ContainerId == _session.SelectedId);
        _syncingUi = false;

        var note = _session.ListenFault ?? (_session.Beacon is { Parsed: true } ? null : "暂无电量");
        BatteryNote.Visibility = note == null ? Visibility.Collapsed : Visibility.Visible;
        BatteryNote.Text = note ?? "";
        CompactLevels.Text = note ?? CompactLevelText(_session.Beacon);

        var beacon = _session.Beacon;
        if (beacon is { Parsed: true, IsMono: true })
        {
            LeftPod.SetCaption("耳机");
            RightPod.Visibility = Visibility.Collapsed;
            ShowPod(LeftPod, beacon.Single, ear: false);
            ShowPod(CasePod, null, ear: false);
        }
        else
        {
            LeftPod.SetCaption("左");
            RightPod.Visibility = Visibility.Visible;
            ShowPod(LeftPod, beacon?.Left, ear: true);
            ShowPod(RightPod, beacon?.Right, ear: true);
            ShowPod(CasePod, beacon?.Case, ear: false);
        }

        var release = _session.HasSelection && HeadsetStateMachine.CanDisconnect(link);
        var canAct = _session.HasSelection && (release || HeadsetStateMachine.CanConnect(link));
        ActionButton.IsEnabled = canAct;
        ActionButton.Content = release ? "让路" : "连接";
        StatusMark.Fill = new SolidColorBrush(release
            ? Windows.UI.Color.FromArgb(255, 10, 132, 255)
            : Windows.UI.Color.FromArgb(255, 142, 142, 147));
        if (release)
        {
            ActionButton.Background = new SolidColorBrush(LightTheme
                ? Windows.UI.Color.FromArgb(0x14, 0x00, 0x00, 0x00)
                : Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            ActionButton.Foreground = new SolidColorBrush(LightTheme
                ? Windows.UI.Color.FromArgb(0xFF, 0x1D, 0x1D, 0x1F)
                : Windows.UI.Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF7));
        }
        else
        {
            ActionButton.ClearValue(Control.BackgroundProperty);
            ActionButton.ClearValue(Control.ForegroundProperty);
        }

        var hint = _session.ShowHfpHint && !compact;
        HfpHint.Visibility = hint ? Visibility.Visible : Visibility.Collapsed;
        HfpDismiss.Visibility = hint ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string CompactLevelText(ProximityBeaconState? beacon)
    {
        if (beacon is not { Parsed: true })
        {
            return "";
        }

        if (beacon.IsMono)
        {
            return Percent(beacon.Single);
        }

        return $"左 {Percent(beacon.Left)}   右 {Percent(beacon.Right)}   盒 {Percent(beacon.Case)}";
    }

    private static string Percent(PodBatteryInfo? pod) =>
        pod is null || pod.IsDisconnected ? "—" : pod.LevelPercent.ToString();

    private static void ShowPod(BatteryColumn column, PodBatteryInfo? pod, bool ear)
    {
        var missing = pod is null || pod.IsDisconnected;
        var percent = missing ? "—" : pod!.LevelPercent.ToString();
        var level = missing ? 0 : pod!.LevelPercent / 100d;
        var earText = !ear || missing ? "" : pod!.InEar ? "在耳" : "未入耳";
        column.Show(percent, level, earText, missing, animate: true);
    }

    private static string ShortLink(HeadsetLinkState state) => state switch
    {
        HeadsetLinkState.Connecting => "正在连接",
        HeadsetLinkState.Disconnecting => "正在断开",
        HeadsetLinkState.BoundActive => "已连接",
        _ => "未连接"
    };

    private void OnAction(object sender, RoutedEventArgs e)
    {
        if (HeadsetStateMachine.CanDisconnect(_session.Link))
        {
            _session.ReleaseToPhone();
        }
        else
        {
            _session.Connect();
        }
    }

    private async void OnBind(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
    }

    private void ToggleSize()
    {
        _config.OverlayCompact = !_config.OverlayCompact;
        _config.Save();
        ApplySize(resize: true);
        Render();
    }

    private void OnDismissHfp(object sender, RoutedEventArgs e)
    {
        _session.DismissHfpHint();
        Render();
    }

    private void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingUi || DeviceCombo.SelectedItem is not BtAudioDevice device)
        {
            return;
        }

        if (device.ContainerId == _session.SelectedId)
        {
            return;
        }

        _session.Select(device.ContainerId);
    }

    private void Card_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(Card).Properties.IsLeftButtonPressed || IsInteractive(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (_collapsed)
        {
            Expand();
        }

        _dragging = true;
        _collapseTimer?.Stop();
        _dragWindowOrigin = _appWindow.Position;
        GetCursorPos(out _dragCursorOrigin);
        Card.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Card_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        GetCursorPos(out var now);
        _appWindow.Move(new PointInt32(
            _dragWindowOrigin.X + now.X - _dragCursorOrigin.X,
            _dragWindowOrigin.Y + now.Y - _dragCursorOrigin.Y));
    }

    private void Card_PointerReleased(object sender, PointerRoutedEventArgs e) => FinishDrag();

    private void Card_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => FinishDrag();

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _collapseTimer?.Stop();
        if (_collapsed && _dock != DockEdge.None)
        {
            Expand();
        }
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging || _dock == DockEdge.None || _collapsed || _settings.IsOpen)
        {
            return;
        }

        _collapseTimer?.Start();
    }

    private bool CursorInsideWindow()
    {
        GetCursorPos(out var cursor);
        var pos = _appWindow.Position;
        var size = _appWindow.Size;
        const int slack = 16;
        return cursor.X >= pos.X - slack && cursor.X < pos.X + size.Width + slack
            && cursor.Y >= pos.Y - slack && cursor.Y < pos.Y + size.Height + slack;
    }

    private void Card_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_collapsed)
        {
            Expand();
        }

        _collapseTimer?.Stop();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var menuWidth = DipToPx(hwnd, SettingsWindow.WidthDip);
        var menuHeight = DipToPx(hwnd, SettingsWindow.HeightDip);
        _settings.Resize(menuWidth, menuHeight);
        var area = WorkArea();
        var pos = _appWindow.Position;
        var size = _appWindow.Size;
        var target = DockPlacement.Beside(
            pos.X,
            pos.Y,
            size.Width,
            size.Height,
            menuWidth,
            menuHeight,
            area.X,
            area.Y,
            area.Width,
            area.Height);
        _settings.SetCompact(_config.OverlayCompact);
        _settings.ShowAt(target.X, target.Y);
    }

    private void OnMenuDismissed()
    {
        if (_dock == DockEdge.None || _collapsed || _dragging)
        {
            return;
        }

        GetCursorPos(out var cursor);
        var pos = _appWindow.Position;
        var size = _appWindow.Size;
        var inside = cursor.X >= pos.X && cursor.X < pos.X + size.Width
            && cursor.Y >= pos.Y && cursor.Y < pos.Y + size.Height;
        if (!inside)
        {
            _collapseTimer?.Start();
        }
    }

    private void FinishDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Card.ReleasePointerCaptures();
        _dock = NearestEdge();
        _config.OverlayDock = _dock == DockEdge.None ? null : _dock.ToString();
        if (_dock == DockEdge.None)
        {
            _collapsed = false;
            SavePosition();
            return;
        }

        SavePosition();
        Collapse();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        SavePosition();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        RemoveWindowSubclass(hwnd, SubclassDelegate, 1);
        Subclassed.Remove(hwnd);
        _acrylic?.Dispose();
        _hotkeys.Dispose();
        _session.Dispose();
        Application.Current.Exit();
    }

    private void TryAcrylic()
    {
        if (Card.ActualTheme == ElementTheme.Default)
        {
            Card.ActualThemeChanged += (_, _) => ApplyAcrylicTheme();
        }
        else
        {
            Card.ActualThemeChanged += (_, _) => ApplyAcrylicTheme();
        }

        if (!DesktopAcrylicController.IsSupported())
        {
            Card.Background = FallbackBrush();
            return;
        }

        _backdrop = new SystemBackdropConfiguration { IsInputActive = true };
        _acrylic = new DesktopAcrylicController();
        ApplyAcrylicTheme();
        _acrylic.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        _acrylic.SetSystemBackdropConfiguration(_backdrop);
        _backdropAttached = true;
    }

    private void ApplyAcrylicTheme()
    {
        var light = LightTheme;
        if (_acrylic == null || _backdrop == null)
        {
            Card.Background = FallbackBrush();
            return;
        }

        _backdrop.Theme = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
        if (light)
        {
            _acrylic.TintColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            _acrylic.TintOpacity = (light ? 0.72f : 0.62f) * (float)Math.Clamp(_config.OverlayOpacity, 0.25, 1);
            _acrylic.FallbackColor = Windows.UI.Color.FromArgb(235, 255, 255, 255);
        }
        else
        {
            _acrylic.TintColor = Windows.UI.Color.FromArgb(255, 28, 28, 30);
            _acrylic.TintOpacity = (light ? 0.72f : 0.62f) * (float)Math.Clamp(_config.OverlayOpacity, 0.25, 1);
            _acrylic.FallbackColor = Windows.UI.Color.FromArgb(230, 28, 28, 30);
        }

        if (!_backdropAttached)
        {
            Card.Background = FallbackBrush();
        }
    }

    private bool LightTheme => Card.ActualTheme != ElementTheme.Dark;

    private SolidColorBrush FallbackBrush() => new(LightTheme
        ? Windows.UI.Color.FromArgb(0xEB, 0xFF, 0xFF, 0xFF)
        : Windows.UI.Color.FromArgb(0xE6, 0x1C, 0x1C, 0x1E));

    private void PlayIntro()
    {
        if (_playedIntro)
        {
            return;
        }

        _playedIntro = true;
        if (!new UISettings().AnimationsEnabled)
        {
            return;
        }

        AnimateIn(Card, 0);
        AnimateIn(LeftPod, 40);
        AnimateIn(RightPod, 80);
        AnimateIn(CasePod, 120);
    }

    private static void AnimateIn(UIElement element, int delayMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var delay = TimeSpan.FromMilliseconds(delayMs);

        visual.Opacity = 0;
        var fade = compositor.CreateSpringScalarAnimation();
        fade.InitialValue = 0;
        fade.FinalValue = 1;
        fade.DampingRatio = 0.9f;
        fade.Period = TimeSpan.FromMilliseconds(280);
        fade.DelayTime = delay;
        visual.StartAnimation("Opacity", fade);

        var offset = compositor.CreateSpringVector3Animation();
        offset.InitialValue = new Vector3(0, 10, 0);
        offset.FinalValue = Vector3.Zero;
        offset.DampingRatio = 0.82f;
        offset.Period = TimeSpan.FromMilliseconds(320);
        offset.DelayTime = delay;
        visual.StartAnimation("Offset", offset);

        var size = element.ActualSize;
        if (size.X <= 0 || size.Y <= 0)
        {
            return;
        }

        visual.CenterPoint = new Vector3(size.X / 2, size.Y / 2, 0);
        visual.Scale = new Vector3(0.96f, 0.96f, 1);
        var scale = compositor.CreateSpringVector3Animation();
        scale.InitialValue = visual.Scale;
        scale.FinalValue = Vector3.One;
        scale.DampingRatio = 0.82f;
        scale.Period = TimeSpan.FromMilliseconds(320);
        scale.DelayTime = delay;
        visual.StartAnimation("Scale", scale);
    }

    private void Place(int width, int height)
    {
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var x = _config.OverlayLeft ?? area.X + area.Width - width - 24;
        var y = _config.OverlayTop ?? area.Y + area.Height - height - 24;
        var maxX = Math.Max(area.X, area.X + area.Width - width);
        var maxY = Math.Max(area.Y, area.Y + area.Height - height);
        _appWindow.Move(new PointInt32(Math.Clamp(x, area.X, maxX), Math.Clamp(y, area.Y, maxY)));
    }

    private void ApplyOpacity()
    {
        Card.Opacity = Math.Clamp(_config.OverlayOpacity, 0.25, 1);
        ApplyAcrylicTheme();
    }

    private void ApplySize(bool resize)
    {
        if (!resize)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var width = DipToPx(hwnd, _config.OverlayCompact ? CompactWidthDip : LargeWidthDip);
        var height = DipToPx(hwnd, _config.OverlayCompact ? CompactHeightDip : LargeHeightDip);
        _appWindow.Resize(new SizeInt32(width, height));
        if (_collapsed)
        {
            Collapse();
        }
    }

    private DockEdge NearestEdge()
    {
        var area = WorkArea();
        var pos = _appWindow.Position;
        var size = _appWindow.Size;
        var left = pos.X - area.X;
        var right = area.X + area.Width - (pos.X + size.Width);
        var top = pos.Y - area.Y;
        var bottom = area.Y + area.Height - (pos.Y + size.Height);
        var best = DockEdge.None;
        var distance = SnapPx + 1;
        void Consider(int value, DockEdge edge)
        {
            if (value <= SnapPx && value < distance)
            {
                distance = value;
                best = edge;
            }
        }

        Consider(left, DockEdge.Left);
        Consider(right, DockEdge.Right);
        Consider(top, DockEdge.Top);
        Consider(bottom, DockEdge.Bottom);
        return best;
    }

    private void Collapse()
    {
        if (_dock == DockEdge.None)
        {
            return;
        }

        var area = WorkArea();
        var size = _appWindow.Size;
        var pos = _appWindow.Position;
        var target = _dock switch
        {
            DockEdge.Left => new PointInt32(area.X - size.Width + StripPx, pos.Y),
            DockEdge.Right => new PointInt32(area.X + area.Width - StripPx, pos.Y),
            DockEdge.Top => new PointInt32(pos.X, area.Y - size.Height + StripPx),
            _ => new PointInt32(pos.X, area.Y + area.Height - StripPx)
        };
        _collapsed = true;
        _appWindow.Move(target);
        _config.OverlayDock = _dock.ToString();
        _config.Save();
    }

    private void Expand()
    {
        _collapsed = false;
        _collapseTimer?.Stop();
        var area = WorkArea();
        var size = _appWindow.Size;
        var pos = _appWindow.Position;
        var target = DockPlacement.Expanded(
            _dock.ToString(),
            area.X,
            area.Y,
            area.Width,
            area.Height,
            size.Width,
            size.Height,
            pos.X,
            pos.Y);
        _appWindow.Move(new PointInt32(target.X, target.Y));
    }

    private RectInt32 WorkArea() =>
        DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

    private static DockEdge ParseDock(string? value) => value switch
    {
        "Left" => DockEdge.Left,
        "Right" => DockEdge.Right,
        "Top" => DockEdge.Top,
        "Bottom" => DockEdge.Bottom,
        _ => DockEdge.None
    };

    private enum DockEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    private void SavePosition()
    {
        if (_collapsed)
        {
            return;
        }

        var position = _appWindow.Position;
        _config.OverlayLeft = position.X;
        _config.OverlayTop = position.Y;
        _config.OverlayDock = _dock == DockEdge.None ? null : _dock.ToString();
        _config.Save();
    }

    private static bool IsInteractive(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button or ComboBox or Slider)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static int DipToPx(nint hwnd, int dip)
    {
        var dpi = GetDpiForWindow(hwnd);
        return (int)Math.Round(dip * dpi / 96.0);
    }

    private static void HideFromTaskbar(nint hwnd)
    {
        var style = GetWindowLong(hwnd, GwlExStyle);
        style = (style | WsExToolWindow) & ~WsExAppWindow;
        SetWindowLong(hwnd, GwlExStyle, style);
    }

    private static nint Subclass(nint hwnd, uint msg, nint wParam, nint lParam, nuint id, nint data)
    {
        if (msg == 0x0312 && Subclassed.TryGetValue(hwnd, out var window))
        {
            window._hotkeys.Handle(msg, wParam);
        }

        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    private delegate nint SubclassProc(nint hwnd, uint msg, nint wParam, nint lParam, nuint id, nint data);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nint data);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
