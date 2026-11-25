using AirPodsWinTray.Core.Models;

namespace AirPodsWinTray.Core.Policy;

/// <summary>
/// In-ear decisions for stereo buds. Mono beacons do not carry a trustworthy in-ear bit.
/// </summary>
public static class EarPresence
{
    public static bool AllOutOfEar(ProximityBeaconState? state)
    {
        if (state is not { Parsed: true, IsMono: false })
        {
            return false;
        }

        if (state.Left is null && state.Right is null)
        {
            return false;
        }

        return !IsWorn(state.Left) && !IsWorn(state.Right);
    }

    public static bool ShouldReleaseAudio(ProximityBeaconState? state, EarDetectReliability reliability) =>
        reliability != EarDetectReliability.Degraded && AllOutOfEar(state);

    private static bool IsWorn(PodBatteryInfo? pod) =>
        pod is { IsDisconnected: false, InEar: true };
}
