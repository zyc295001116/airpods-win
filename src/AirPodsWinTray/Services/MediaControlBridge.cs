using Windows.Media.Control;

namespace AirPodsWinTray.Services;

public sealed class MediaControlBridge : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += OnSessionChanged;
    }

    public event Action<bool>? PlaybackChanged;

    public bool IsAnySessionPlaying()
    {
        if (_manager == null)
        {
            return false;
        }

        foreach (var session in _manager.GetSessions())
        {
            var info = session.GetPlaybackInfo();
            if (info.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                return true;
            }
        }

        return false;
    }

    public async Task TryPauseAsync()
    {
        if (_manager == null)
        {
            return;
        }

        foreach (var session in _manager.GetSessions())
        {
            var info = session.GetPlaybackInfo();
            if (info.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await session.TryPauseAsync();
            }
        }
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
    {
        PlaybackChanged?.Invoke(IsAnySessionPlaying());
    }

    public void Dispose()
    {
        if (_manager != null)
        {
            _manager.CurrentSessionChanged -= OnSessionChanged;
        }
    }
}