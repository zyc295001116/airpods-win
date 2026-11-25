using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Core.Policy;
using Microsoft.UI.Dispatching;

namespace AirPodsWinTray.Services;

public sealed class AudioPolicyHostedService : IDisposable
{
    private readonly AudioPolicyEngine _engine = new();
    private readonly AppConfig _config;
    private readonly BtAudioService _btAudio;
    private readonly MediaControlBridge _media;
    private readonly DispatcherQueueTimer _timer;
    private Guid? _deviceId;
    private string _deviceName;
    private bool _suppressAutoConnect;

    public AudioPolicyHostedService(AppConfig config, BtAudioService btAudio, MediaControlBridge media, DispatcherQueue queue)
    {
        _config = config;
        _btAudio = btAudio;
        _media = media;
        _deviceId = config.BoundContainerId;
        _deviceName = config.BoundContainerName ?? "";

        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(15);
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => Tick();

        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        _media.PlaybackChanged += playing => _engine.NotifyMediaPlayback(playing);
    }

    public void SetDevice(Guid? containerId, string? name)
    {
        _deviceId = containerId;
        _deviceName = name ?? "";
    }

    public void SetSuppressAutoConnect(bool suppress) => _suppressAutoConnect = suppress;

    public void Start()
    {
        _engine.NotifyMediaPlayback(_media.IsAnySessionPlaying());
        _timer.Start();
    }

    public void RequestRelease() =>
        Tick(manualRelease: true);

    private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        _engine.NotifySessionLock(e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock);
        Tick();
    }

    private void Tick(bool manualRelease = false)
    {
        var device = _deviceId is Guid id ? _btAudio.FindById(id) : _btAudio.FindByName(_deviceName);
        var connected = device?.IsConnected ?? false;
        var settings = new AudioPolicySettings
        {
            AutoConnectOnMediaPlay = _config.AutoConnectOnMediaPlay,
            AutoDisconnectOnLock = _config.AutoDisconnectOnLock,
            AutoDisconnectOnIdle = _config.AutoDisconnectOnIdle,
            IdleDisconnectMinutes = _config.IdleDisconnectMinutes,
            ManualReleaseRequested = manualRelease,
            SuppressAutoConnect = _suppressAutoConnect
        };

        var decision = _engine.Evaluate(settings, connected);
        if (manualRelease || decision.ShouldConnect || decision.ShouldDisconnect)
        {
            // #region agent log
            AgentDebugLog.Write("A", "AudioPolicyHostedService.Tick", "policy action", new { manualRelease, connected, found = device != null, connect = decision.ShouldConnect, disconnect = decision.ShouldDisconnect, reason = decision.Reason.ToString() });
            // #endregion
        }
        if (decision.ShouldConnect)
        {
            if (_deviceId is Guid connectId)
            {
                _btAudio.Connect(connectId);
            }
        }
        else if (decision.ShouldDisconnect)
        {
            if (_deviceId is Guid disconnectId)
            {
                _btAudio.Disconnect(disconnectId);
            }
        }

        if (_media.IsAnySessionPlaying())
        {
            _engine.NotifyAudioActivity();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
