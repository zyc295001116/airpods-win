using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Services;

public sealed class AudioPolicyHostedService : IDisposable
{
    private readonly AudioPolicyEngine _engine = new();
    private readonly AppConfig _config;
    private readonly BtAudioService _btAudio;
    private readonly MediaControlBridge _media;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly string _deviceName;

    public AudioPolicyHostedService(AppConfig config, BtAudioService btAudio, MediaControlBridge media)
    {
        _config = config;
        _btAudio = btAudio;
        _media = media;
        _deviceName = string.IsNullOrWhiteSpace(config.BoundContainerName) ? "AirPods" : config.BoundContainerName;

        _timer = new System.Windows.Forms.Timer { Interval = 15_000 };
        _timer.Tick += (_, _) => Tick();

        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        _media.PlaybackChanged += playing => _engine.NotifyMediaPlayback(playing);
    }

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
        var device = _btAudio.FindByName(_deviceName);
        var connected = device?.IsConnected ?? false;
        var settings = new AudioPolicySettings
        {
            AutoConnectOnMediaPlay = _config.AutoConnectOnMediaPlay,
            AutoDisconnectOnLock = _config.AutoDisconnectOnLock,
            AutoDisconnectOnIdle = _config.AutoDisconnectOnIdle,
            IdleDisconnectMinutes = _config.IdleDisconnectMinutes,
            ManualReleaseRequested = manualRelease
        };

        var decision = _engine.Evaluate(settings, connected);
        if (decision.ShouldConnect)
        {
            _btAudio.ConnectByName(_deviceName);
        }
        else if (decision.ShouldDisconnect)
        {
            _btAudio.DisconnectByName(_deviceName);
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
        _timer.Dispose();
    }
}