namespace AirPodsWinTray.Core.Policy;

/// <summary>
/// Fires once after all buds have stayed out of the ear for <see cref="Hold"/>.
/// Auto-connect stays suppressed for the whole out-of-ear stretch.
/// </summary>
public sealed class EarReleaseTracker
{
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(1);

    private long _outSinceTicks;
    private bool _fired;

    public bool SuppressAutoConnect { get; private set; }

    public bool Waiting => _outSinceTicks != 0 && !_fired;

    public bool Observe(bool allOut, long nowTicks)
    {
        if (!allOut)
        {
            _outSinceTicks = 0;
            _fired = false;
            SuppressAutoConnect = false;
            return false;
        }

        SuppressAutoConnect = true;
        if (_outSinceTicks == 0)
        {
            _outSinceTicks = nowTicks;
        }

        if (_fired || nowTicks - _outSinceTicks < Hold.Ticks)
        {
            return false;
        }

        _fired = true;
        return true;
    }
}
