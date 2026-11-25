namespace AirPodsWinTray.Core.Policy;

/// <summary>
/// Platform-agnostic policy decisions for auto connect / disconnect (iPhone handoff friendly).
/// </summary>
public sealed class AudioPolicyEngine
{
    private DateTime _lastAudioActivityUtc = DateTime.UtcNow;
    private bool _mediaPlaying;
    private bool _isLocked;

    public void NotifyMediaPlayback(bool isPlaying)
    {
        _mediaPlaying = isPlaying;
        if (isPlaying)
        {
            _lastAudioActivityUtc = DateTime.UtcNow;
        }
    }

    public void NotifyAudioActivity()
    {
        _lastAudioActivityUtc = DateTime.UtcNow;
    }

    public void NotifySessionLock(bool locked) => _isLocked = locked;

    public PolicyDecision Evaluate(in AudioPolicySettings settings, bool deviceConnected)
    {
        if (settings.ManualReleaseRequested)
        {
            return PolicyDecision.Disconnect(PolicyReason.ManualRelease);
        }

        if (!deviceConnected)
        {
            if (settings.AutoConnectOnMediaPlay && _mediaPlaying && !settings.SuppressAutoConnect)
            {
                return PolicyDecision.Connect(PolicyReason.MediaPlay);
            }

            return PolicyDecision.None;
        }

        if (settings.AutoDisconnectOnLock && _isLocked)
        {
            return PolicyDecision.Disconnect(PolicyReason.SessionLocked);
        }

        if (settings.AutoDisconnectOnIdle && !_mediaPlaying)
        {
            var idle = DateTime.UtcNow - _lastAudioActivityUtc;
            if (idle >= TimeSpan.FromMinutes(settings.IdleDisconnectMinutes))
            {
                return PolicyDecision.Disconnect(PolicyReason.IdleTimeout);
            }
        }

        return PolicyDecision.None;
    }
}

public sealed class AudioPolicySettings
{
    public bool AutoConnectOnMediaPlay { get; init; } = true;
    public bool AutoDisconnectOnLock { get; init; } = true;
    public bool AutoDisconnectOnIdle { get; init; } = true;
    public int IdleDisconnectMinutes { get; init; } = 10;
    public bool ManualReleaseRequested { get; set; }
    public bool SuppressAutoConnect { get; init; }
}

public enum PolicyReason
{
    None,
    MediaPlay,
    SessionLocked,
    IdleTimeout,
    ManualRelease
}

public readonly struct PolicyDecision
{
    public bool ShouldConnect { get; init; }
    public bool ShouldDisconnect { get; init; }
    public PolicyReason Reason { get; init; }

    public static PolicyDecision None => default;

    public static PolicyDecision Connect(PolicyReason reason) =>
        new() { ShouldConnect = true, Reason = reason };

    public static PolicyDecision Disconnect(PolicyReason reason) =>
        new() { ShouldDisconnect = true, Reason = reason };
}