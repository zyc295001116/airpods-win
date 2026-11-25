using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Policy;
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
    private readonly System.Windows.Forms.Timer _linkTimer = new() { Interval = 500 };
    private readonly ToolStripLabel _titleItem = new();
    private readonly ToolStripLabel _linkItem = new();
    private readonly ToolStripLabel _battery0 = new();
    private readonly ToolStripLabel _battery1 = new();
    private readonly ToolStripLabel _battery2 = new();
    private readonly ToolStripMenuItem _connectItem = new("连接");
    private readonly ToolStripMenuItem _disconnectItem = new("断开（让路 iPhone）");
    private string _deviceName;
    private string _boundDisplayName;
    private HeadsetLinkState _link = HeadsetLinkState.Unbound;
    private int _linkPolls;
    private long _commandAt;
    private string _lastBatteryText = "";

    public TrayApplicationContext(AppConfig config)
    {
        _messageForm = new MessagePumpForm(_hotkeys);
        _config = config;
        _deviceName = string.IsNullOrWhiteSpace(config.BoundContainerName) ? "AirPods" : config.BoundContainerName;
        _boundDisplayName = string.IsNullOrWhiteSpace(config.BoundContainerName) ? "未绑定设备" : config.BoundContainerName;

        if (config.BoundContainerId is Guid containerId)
        {
            _btAudio.Seed(containerId, _deviceName, config.BoundEndpointIds, config.BoundKsDeviceIds);
            _link = HeadsetLinkState.BoundIdle;
        }

        _bleState = new BleStateService(_registry);
        _bleState.SetBoundHint(_deviceName);
        _policy = new AudioPolicyHostedService(config, _btAudio, _media);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "AirPods WinTray"
        };

        BuildMenu();
        _linkTimer.Tick += (_, _) => PollLink();
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

        _ = Task.Run(() =>
        {
            _btAudio.Discover();
            if (_messageForm.IsHandleCreated)
            {
                _messageForm.BeginInvoke(ApplyKnownDevice);
            }
        });
    }

    private void RegisterHotkeys()
    {
        var hwnd = _messageForm.Handle;
        _hotkeys.Register(hwnd, _config.ConnectHotkey, Connect);
        _hotkeys.Register(hwnd, _config.DisconnectHotkey, ReleaseToPhone);
        _hotkeys.Register(hwnd, _config.ReleaseHotkey, ReleaseToPhone);
    }

    private void BuildMenu()
    {
        _connectItem.Click += (_, _) => Connect();
        _disconnectItem.Click += (_, _) => ReleaseToPhone();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_titleItem);
        menu.Items.Add(_linkItem);
        menu.Items.Add(_battery0);
        menu.Items.Add(_battery1);
        menu.Items.Add(_battery2);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_connectItem);
        menu.Items.Add(_disconnectItem);
        menu.Items.Add("刷新设备", null, (_, _) => RefreshDevices());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => ApplyKnownDevice();
        _tray.ContextMenuStrip = menu;
        UpdateMenuItems();
    }

    private void Connect()
    {
        // #region agent log
        AgentDebugLog.Write("A", "TrayApplicationContext.Connect", "connect clicked", new { link = _link.ToString(), can = HeadsetStateMachine.CanConnect(_link) });
        // #endregion
        if (!HeadsetStateMachine.CanConnect(_link))
        {
            return;
        }

        if (_link == HeadsetLinkState.Unbound)
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.Bound);
        }

        _link = HeadsetStateMachine.Next(_link, HeadsetSignal.ConnectRequested);
        _commandAt = Environment.TickCount64;
        UpdateMenuItems();
        RefreshTrayText();

        var ok = _btAudio.ConnectByName(_deviceName);
        // #region agent log
        AgentDebugLog.Write("E", "TrayApplicationContext.Connect", "connect returned", new { ok, link = _link.ToString() });
        // #endregion
        if (!ok)
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.AttemptFinished);
            if (_btAudio.FindByName(_deviceName) == null && _config.BoundContainerId == null)
            {
                _link = HeadsetStateMachine.Next(_link, HeadsetSignal.Unbound);
            }

            UpdateMenuItems();
            RefreshTrayText();
            return;
        }

        BeginWatch();
    }

    private void ReleaseToPhone()
    {
        // #region agent log
        AgentDebugLog.Write("A", "TrayApplicationContext.ReleaseToPhone", "disconnect clicked", new { link = _link.ToString(), can = HeadsetStateMachine.CanDisconnect(_link) });
        // #endregion
        if (!HeadsetStateMachine.CanDisconnect(_link))
        {
            return;
        }

        _link = HeadsetStateMachine.Next(_link, HeadsetSignal.DisconnectRequested);
        _commandAt = Environment.TickCount64;
        UpdateMenuItems();
        RefreshTrayText();
        _policy.RequestRelease();
        BeginWatch();
    }

    private void BeginWatch()
    {
        _linkPolls = 0;
        _linkTimer.Start();
    }

    private void PollLink()
    {
        ApplyKnownDevice();
        if (_link is not (HeadsetLinkState.Connecting or HeadsetLinkState.Disconnecting))
        {
            _linkTimer.Stop();
            return;
        }

        _linkPolls++;
        if (_linkPolls < 16)
        {
            return;
        }

        var stillConnected = _link == HeadsetLinkState.Disconnecting && _btAudio.FindByName(_deviceName)?.IsConnected == true;
        // #region agent log
        AgentDebugLog.Write("E", "TrayApplicationContext.PollLink", "link watch timed out", new { link = _link.ToString(), polls = _linkPolls, stillConnected });
        // #endregion
        if (stillConnected)
        {
            _link = HeadsetLinkState.BoundActive;
        }
        else
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.AttemptFinished);
        }
        _linkTimer.Stop();
        UpdateMenuItems();
        RefreshTrayText();
    }

    private void ApplyKnownDevice()
    {
        var before = _link;
        var device = _btAudio.FindByName(_deviceName);
        var bound = device != null || _config.BoundContainerId != null;
        var audio = device?.IsConnected == true;
        if (device != null)
        {
            _boundDisplayName = device.ContainerName;
            _deviceName = device.ContainerName;
            _bleState.SetBoundHint(_deviceName);
            Persist(device);
        }

        if (!bound)
        {
            if (_link is not (HeadsetLinkState.Connecting or HeadsetLinkState.Disconnecting))
            {
                _link = HeadsetStateMachine.Next(_link, HeadsetSignal.Unbound);
            }
        }
        else if (_link == HeadsetLinkState.Unbound)
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.Bound);
            if (audio)
            {
                _link = HeadsetStateMachine.Next(_link, HeadsetSignal.AudioActive);
            }
        }
        else if (audio && _link != HeadsetLinkState.Disconnecting)
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.AudioActive);
        }
        else if (!audio && _link != HeadsetLinkState.Connecting)
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.AudioInactive);
        }

        if (_link != before)
        {
            // #region agent log
            AgentDebugLog.Write("F", "TrayApplicationContext.ApplyKnownDevice", "link changed", new
            {
                from = before.ToString(),
                to = _link.ToString(),
                audio,
                ms = _commandAt == 0 ? -1 : Environment.TickCount64 - _commandAt,
                defaultIsHeadset = _btAudio.DefaultPlaybackIsKnown(_deviceName)
            });
            // #endregion
        }

        UpdateMenuItems();
        RefreshTrayText();
    }

    private void Persist(BtAudioDevice device)
    {
        if (device.KsDeviceIds.Count == 0)
        {
            return;
        }

        if (_config.BoundContainerId == device.ContainerId
            && _config.BoundContainerName == device.ContainerName
            && Same(_config.BoundKsDeviceIds, device.KsDeviceIds)
            && Same(_config.BoundEndpointIds, device.EndpointIds))
        {
            return;
        }

        _config.BoundContainerId = device.ContainerId;
        _config.BoundContainerName = device.ContainerName;
        _config.BoundEndpointIds = device.EndpointIds.ToList();
        _config.BoundKsDeviceIds = device.KsDeviceIds.ToList();
        _config.Save();
    }

    private static bool Same(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right);

    private void UpdateMenuItems()
    {
        _titleItem.Text = _boundDisplayName;
        _linkItem.Text = HeadsetStatusText.LinkLabel(_link);

        var lines = HeadsetStatusText.BatteryLines(_bleState.LastState);
        var slots = new[] { _battery0, _battery1, _battery2 };
        for (var i = 0; i < slots.Length; i++)
        {
            if (i < lines.Count)
            {
                slots[i].Text = lines[i];
                slots[i].Visible = true;
            }
            else
            {
                slots[i].Visible = false;
            }
        }

        _connectItem.Enabled = HeadsetStateMachine.CanConnect(_link);
        _disconnectItem.Enabled = HeadsetStateMachine.CanDisconnect(_link);
    }

    private void RefreshDevices()
    {
        var devices = _btAudio.ListDevices();
        ApplyKnownDevice();
        var names = string.Join(", ", devices.Select(d => $"{d.ContainerName} ({(d.IsConnected ? "音频已连接" : "已绑定未连接")})"));
        _tray.BalloonTipTitle = "蓝牙音频设备";
        _tray.BalloonTipText = string.IsNullOrWhiteSpace(names) ? "未找到设备" : names;
        _tray.ShowBalloonTip(3000);
    }

    private void OnBleStateChanged(ProximityBeaconState state, AirPodsModelDefinition definition)
    {
        var lines = string.Join(" | ", HeadsetStatusText.BatteryLines(state));
        if (lines != _lastBatteryText)
        {
            _lastBatteryText = lines;
            // #region agent log
            AgentDebugLog.Write("I", "TrayApplicationContext.OnBleStateChanged", "menu battery", new
            {
                lines,
                uiThread = !_messageForm.InvokeRequired,
                model = definition.Code
            });
            // #endregion
        }

        if (_messageForm.IsHandleCreated && _messageForm.InvokeRequired)
        {
            _messageForm.BeginInvoke(() =>
            {
                UpdateMenuItems();
                RefreshTrayText();
            });
        }
        else
        {
            UpdateMenuItems();
            RefreshTrayText();
        }

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

    private void RefreshTrayText()
    {
        var text = $"{_boundDisplayName} {HeadsetStatusText.LinkLabel(_link)}";
        var state = _bleState.LastState;
        if (state is { Parsed: true, IsMono: false })
        {
            text += $" L{state.Left?.LevelPercent}% R{state.Right?.LevelPercent}%";
        }

        _tray.Text = text.Length <= 127 ? text : text[..127];
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _linkTimer.Stop();
            _linkTimer.Dispose();
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
