using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Policy;
using AirPodsWinTray.Core.Registry;
using Microsoft.UI.Dispatching;

namespace AirPodsWinTray.Services;

public sealed class HeadsetSession : IDisposable
{
    private readonly AppConfig _config;
    private readonly DispatcherQueue _queue;
    private readonly BtAudioService _btAudio = new();
    private readonly AirPodsModelRegistry _registry = AirPodsModelRegistry.LoadEmbedded();
    private readonly BleStateService _bleState;
    private readonly MediaControlBridge _media = new();
    private readonly AudioPolicyHostedService _policy;
    private readonly DispatcherQueueTimer _linkTimer;
    private readonly DispatcherQueueTimer _statusTimer;
    private readonly DispatcherQueueTimer _earTimer;
    private readonly EarReleaseTracker _ears = new();
    private string _deviceName;
    private string _boundDisplayName;
    private HeadsetLinkState _link = HeadsetLinkState.Unbound;
    private int _linkPolls;
    private long _commandAt;
    private bool _defaultPlaybackSet;
    private string _rosterSignature = "";

    public HeadsetSession(AppConfig config, DispatcherQueue queue)
    {
        _config = config;
        _queue = queue;
        _deviceName = config.BoundContainerName ?? "";
        _boundDisplayName = string.IsNullOrWhiteSpace(config.BoundContainerName) ? "未绑定设备" : config.BoundContainerName;

        _bleState = new BleStateService(_registry);
        _bleState.SetBoundHint(string.IsNullOrWhiteSpace(_deviceName) ? "" : _deviceName);
        _policy = new AudioPolicyHostedService(config, _btAudio, _media, queue);

        _linkTimer = queue.CreateTimer();
        _linkTimer.Interval = TimeSpan.FromMilliseconds(500);
        _linkTimer.IsRepeating = true;
        _linkTimer.Tick += (_, _) => PollLink();

        _statusTimer = queue.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(1);
        _statusTimer.IsRepeating = true;
        _statusTimer.Tick += (_, _) => RefreshRoster();

        _earTimer = queue.CreateTimer();
        _earTimer.Interval = EarReleaseTracker.Hold;
        _earTimer.Tick += (_, _) => OnEarDelay();

        _bleState.StateChanged += (state, definition) =>
            _queue.TryEnqueue(() => OnBeacon(state, definition));
    }

    public event Action? Changed;

    public HeadsetLinkState Link => _link;

    public ProximityBeaconState? Beacon => _bleState.LastState;

    public AirPodsPickerMode Picker { get; private set; } = AirPodsPickerMode.None;

    public IReadOnlyList<BtAudioDevice> AirPods { get; private set; } = [];

    public Guid? SelectedId => _config.BoundContainerId;

    public bool HasSelection => _config.BoundContainerId != null && Picker != AirPodsPickerMode.None;

    public string? ListenFault => _bleState.Fault;

    public bool ShowHfpHint => HasSelection && !_config.MusicModeDisableHfpHintShown;

    public string ModelTitle =>
        _bleState.LastModel is { IsKnown: true } model ? model.Name : _boundDisplayName;

    public void Select(Guid containerId)
    {
        var device = AirPods.FirstOrDefault(item => item.ContainerId == containerId) ?? _btAudio.FindById(containerId);
        if (device == null)
        {
            return;
        }

        EnsureBound(device);
        ApplyLink();
        Changed?.Invoke();
    }

    public void DismissHfpHint()
    {
        _config.MusicModeDisableHfpHintShown = true;
        _config.Save();
        Changed?.Invoke();
    }

    public void Start()
    {
        _ = StartAsync();
    }

    public void Connect()
    {
        // #region agent log
        AgentDebugLog.Write("A", "HeadsetSession.Connect", "connect clicked", new { link = _link.ToString(), can = HeadsetStateMachine.CanConnect(_link) });
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
        Changed?.Invoke();

        var id = _config.BoundContainerId;
        var ok = id is Guid containerId && _btAudio.Connect(containerId);
        // #region agent log
        AgentDebugLog.Write("E", "HeadsetSession.Connect", "connect returned", new { ok, link = _link.ToString() });
        // #endregion
        if (!ok)
        {
            _link = HeadsetStateMachine.Next(_link, HeadsetSignal.AttemptFinished);
            if (_btAudio.FindById(_config.BoundContainerId ?? Guid.Empty) == null && _config.BoundContainerId == null)
            {
                _link = HeadsetStateMachine.Next(_link, HeadsetSignal.Unbound);
            }

            Changed?.Invoke();
            return;
        }

        BeginWatch();
    }

    public void ReleaseToPhone()
    {
        // #region agent log
        AgentDebugLog.Write("A", "HeadsetSession.ReleaseToPhone", "disconnect clicked", new { link = _link.ToString(), can = HeadsetStateMachine.CanDisconnect(_link) });
        // #endregion
        if (!HeadsetStateMachine.CanDisconnect(_link))
        {
            return;
        }

        _link = HeadsetStateMachine.Next(_link, HeadsetSignal.DisconnectRequested);
        _commandAt = Environment.TickCount64;
        Changed?.Invoke();
        _policy.RequestRelease();
        BeginWatch();
    }

    public void RefreshDevices()
    {
        _btAudio.Discover();
        RefreshRoster();
    }

    public void Dispose()
    {
        _linkTimer.Stop();
        _statusTimer.Stop();
        _earTimer.Stop();
        _bleState.Dispose();
        _media.Dispose();
        _policy.Dispose();
    }

    private async Task StartAsync()
    {
        await _media.InitializeAsync();
        _policy.Start();
        _bleState.Start();
        _statusTimer.Start();

        _ = Task.Run(() =>
        {
            _btAudio.Discover();
            _queue.TryEnqueue(RefreshRoster);
        });
    }

    private void OnBeacon(ProximityBeaconState state, AirPodsModelDefinition definition)
    {
        var detected = EarPresence.ShouldReleaseAudio(state, definition.EarDetectReliability);
        var release = detected && _config.DisconnectWhenAllOutOfEar;
        var pause = detected && _config.PauseOnEarRemoval;
        var fired = _ears.Observe(release || pause, Environment.TickCount64);
        _policy.SetSuppressAutoConnect(release && _ears.SuppressAutoConnect);

        if (fired)
        {
            _ = PauseThenReleaseAsync(pause, release);
        }

        if (_ears.Waiting)
        {
            if (!_earTimer.IsRunning)
            {
                _earTimer.Start();
            }
        }
        else
        {
            _earTimer.Stop();
        }

        Changed?.Invoke();
    }

    private void OnEarDelay()
    {
        _earTimer.Stop();
        var state = _bleState.LastState;
        var model = _bleState.LastModel;
        if (state != null && model != null)
        {
            OnBeacon(state, model);
        }
    }

    private async Task PauseThenReleaseAsync(bool pause, bool release)
    {
        if (pause)
        {
            await _media.TryPauseAsync();
        }

        if (release)
        {
            _queue.TryEnqueue(ReleaseToPhone);
        }
    }

    private void BeginWatch()
    {
        _linkPolls = 0;
        if (!_linkTimer.IsRunning)
        {
            _linkTimer.Start();
        }
    }

    private void PollLink()
    {
        ApplyLink();
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

        var stillConnected = _link == HeadsetLinkState.Disconnecting
            && _config.BoundContainerId is Guid connectedId
            && _btAudio.FindById(connectedId)?.IsConnected == true;
        // #region agent log
        AgentDebugLog.Write("E", "HeadsetSession.PollLink", "link watch timed out", new { link = _link.ToString(), polls = _linkPolls, stillConnected });
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
        Changed?.Invoke();
    }

    private void RefreshRoster()
    {
        var pods = _btAudio.ListAirPods();
        var signature = string.Join("|", pods.Select(device => device.ContainerId));
        AirPods = pods;
        Picker = AirPodsPicker.ModeForCount(pods.Count);

        var selectionChanged = Picker switch
        {
            AirPodsPickerMode.None => ClearBindingIfNeeded(),
            AirPodsPickerMode.Single => EnsureBound(pods[0]),
            _ => _config.BoundContainerId is Guid saved && pods.Any(device => device.ContainerId == saved)
                ? EnsureBound(pods.First(device => device.ContainerId == saved))
                : ClearBindingIfNeeded()
        };

        var linkChanged = ApplyLink();
        if (selectionChanged || linkChanged || signature != _rosterSignature)
        {
            _rosterSignature = signature;
            Changed?.Invoke();
        }
    }

    private bool EnsureBound(BtAudioDevice device)
    {
        var same = _config.BoundContainerId == device.ContainerId
            && _config.BoundContainerName == device.ContainerName
            && Same(_config.BoundKsDeviceIds, device.KsDeviceIds)
            && Same(_config.BoundEndpointIds, device.EndpointIds);
        _deviceName = device.ContainerName;
        _boundDisplayName = device.ContainerName;
        _bleState.SetBoundHint(device.ContainerName);
        _policy.SetDevice(device.ContainerId, device.ContainerName);
        if (same || device.KsDeviceIds.Count == 0)
        {
            return false;
        }

        _config.BoundContainerId = device.ContainerId;
        _config.BoundContainerName = device.ContainerName;
        _config.BoundEndpointIds = device.EndpointIds.ToList();
        _config.BoundKsDeviceIds = device.KsDeviceIds.ToList();
        _config.Save();
        return true;
    }

    private bool ClearBindingIfNeeded()
    {
        _deviceName = "";
        _boundDisplayName = "未绑定设备";
        _bleState.SetBoundHint("");
        _policy.SetDevice(null, "");
        if (_config.BoundContainerId == null
            && string.IsNullOrWhiteSpace(_config.BoundContainerName)
            && _config.BoundKsDeviceIds.Count == 0)
        {
            return false;
        }

        _config.BoundContainerId = null;
        _config.BoundContainerName = null;
        _config.BoundEndpointIds = [];
        _config.BoundKsDeviceIds = [];
        _config.Save();
        if (_link is not (HeadsetLinkState.Connecting or HeadsetLinkState.Disconnecting))
        {
            _link = HeadsetLinkState.Unbound;
        }

        return true;
    }

    private bool ApplyLink()
    {
        var before = _link;
        var device = _config.BoundContainerId is Guid id ? _btAudio.FindById(id) : null;
        var bound = device != null;
        var audio = device?.IsConnected == true;
        if (!bound)
        {
            if (_link is not (HeadsetLinkState.Connecting or HeadsetLinkState.Disconnecting))
            {
                _link = HeadsetLinkState.Unbound;
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

        if (audio && !_defaultPlaybackSet && device != null)
        {
            var endpoint = _btAudio.FindStereoPlaybackEndpoint(device.ContainerId);
            if (endpoint != null && DefaultAudioDeviceService.SetDefaultPlaybackDevice(endpoint))
            {
                _defaultPlaybackSet = true;
            }
        }
        else if (!audio)
        {
            _defaultPlaybackSet = false;
        }

        return _link != before;
    }

    private static bool Same(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right);
}
