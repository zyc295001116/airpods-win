using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Registry;
using AirPodsWinTray.Services;
using AirPodsWinTray.UI;

namespace AirPodsWinTray;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly AppConfig _config;
    private readonly BtAudioService _btAudio = new();
    private readonly AirPodsModelRegistry _registry = AirPodsModelRegistry.LoadEmbedded();
    private readonly BleStateService _bleState;
    private readonly MediaControlBridge _media = new();
    private readonly AudioPolicyHostedService _policy;
    private readonly HotKeyManager _hotkeys = new();
    private readonly MessagePumpForm _messageForm;
    private readonly BatteryPopupForm _popup = new();
    private readonly string _deviceName;

    public TrayApplicationContext(AppConfig config)
    {
        _messageForm = new MessagePumpForm(_hotkeys);
        _config = config;
        _deviceName = string.IsNullOrWhiteSpace(config.BoundContainerName) ? "AirPods" : config.BoundContainerName;

        _bleState = new BleStateService(_registry);
        _policy = new AudioPolicyHostedService(config, _btAudio, _media);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "AirPods WinTray"
        };

        BuildMenu();
        _messageForm.Show();
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _tray.ContextMenuStrip?.Show(Cursor.Position);
            }
        };

        _bleState.StateChanged += OnBleStateChanged;
        _bleState.CaseOpened += OnCaseOpened;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _media.InitializeAsync();
        _policy.Start();
        _bleState.Start();

        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        var hwnd = _messageForm.Handle;
        _hotkeys.Register(hwnd, _config.ConnectHotkey, Connect);
        _hotkeys.Register(hwnd, _config.DisconnectHotkey, Disconnect);
        _hotkeys.Register(hwnd, _config.ReleaseHotkey, () => _policy.RequestRelease());
    }

    private void BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("连接", null, (_, _) => Connect());
        menu.Items.Add("断开（让路 iPhone）", null, (_, _) => _policy.RequestRelease());
        menu.Items.Add("刷新设备", null, (_, _) => RefreshDevices());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        _tray.ContextMenuStrip = menu;
    }

    private void Connect()
    {
        _btAudio.ConnectByName(_deviceName);
        RefreshTrayText();
    }

    private void Disconnect()
    {
        _btAudio.DisconnectByName(_deviceName);
        RefreshTrayText();
    }

    private void RefreshDevices()
    {
        var devices = _btAudio.ListDevices();
        var names = string.Join(", ", devices.Select(d => $"{d.ContainerName} ({(d.IsConnected ? "已连" : "未连")})"));
        _tray.BalloonTipTitle = "蓝牙音频设备";
        _tray.BalloonTipText = string.IsNullOrWhiteSpace(names) ? "未找到设备" : names;
        _tray.ShowBalloonTip(3000);
        RefreshTrayText();
    }

    private void OnBleStateChanged(ProximityBeaconState state, AirPodsModelDefinition definition)
    {
        RefreshTrayText(state, definition);
        MaybePauseOnEarRemoval(state, definition);
    }

    private void MaybePauseOnEarRemoval(ProximityBeaconState state, AirPodsModelDefinition definition)
    {
        if (!_config.PauseOnEarRemoval)
        {
            return;
        }

        if (definition.EarDetectReliability == EarDetectReliability.Degraded)
        {
            return;
        }

        var noneInEar = !state.IsMono &&
                        state.Left is { InEar: false } &&
                        state.Right is { InEar: false };

        if (noneInEar)
        {
            _ = _media.TryPauseAsync();
        }
    }

    private void OnCaseOpened()
    {
        if (!_config.ShowCaseOpenPopup)
        {
            return;
        }

        var state = _bleState.LastState;
        var model = _bleState.LastModel;
        if (state == null || model == null)
        {
            return;
        }

        _popup.ShowState(state, model, Cursor.Position);
    }

    private void RefreshTrayText(ProximityBeaconState? state = null, AirPodsModelDefinition? definition = null)
    {
        state ??= _bleState.LastState;
        definition ??= _bleState.LastModel;
        var device = _btAudio.FindByName(_deviceName);
        var conn = device?.IsConnected == true ? "已连接" : "未连接";

        if (state?.Parsed == true && !state.IsMono)
        {
            _tray.Text = $"{definition?.Name ?? _deviceName} {conn} L{state.Left?.LevelPercent}% R{state.Right?.LevelPercent}%";
        }
        else
        {
            _tray.Text = $"{_deviceName} {conn}";
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _bleState.Dispose();
            _media.Dispose();
            _policy.Dispose();
            _hotkeys.Dispose();
            _popup.Dispose();
            _messageForm.Dispose();
        }

        base.Dispose(disposing);
    }
}