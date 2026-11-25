using AirPodsWinTray.Core.Models;

namespace AirPodsWinTray.Core.Parsing;

/// <summary>
/// Keeps the last known battery when a later beacon omits one pod, and ignores a single contradictory in-ear flag.
/// </summary>
public sealed class ProximityDisplay
{
    private ProximityBeaconState? _current;
    private bool? _leftInEarPending;
    private bool? _rightInEarPending;

    public ProximityBeaconState Apply(ProximityBeaconState next)
    {
        if (_current is null || _current.ModelCode != next.ModelCode || _current.IsMono || next.IsMono)
        {
            _leftInEarPending = null;
            _rightInEarPending = null;
            _current = next;
            return next;
        }

        var merged = new ProximityBeaconState
        {
            Parsed = true,
            ModelCode = next.ModelCode,
            IsMono = false,
            Left = MergePod(_current.Left, next.Left, ref _leftInEarPending),
            Right = MergePod(_current.Right, next.Right, ref _rightInEarPending),
            Case = next.Case is null || next.Case.IsDisconnected ? _current.Case : next.Case,
            RawHex = next.RawHex
        };
        _current = merged;
        return merged;
    }

    private static PodBatteryInfo? MergePod(PodBatteryInfo? previous, PodBatteryInfo? next, ref bool? pendingInEar)
    {
        if (next is null || next.IsDisconnected)
        {
            return previous;
        }

        if (previous is null || previous.IsDisconnected)
        {
            pendingInEar = null;
            return next;
        }

        var inEar = previous.InEar;
        if (next.InEar == previous.InEar)
        {
            pendingInEar = null;
            inEar = next.InEar;
        }
        else if (pendingInEar == next.InEar)
        {
            pendingInEar = null;
            inEar = next.InEar;
        }
        else
        {
            pendingInEar = next.InEar;
        }

        return new PodBatteryInfo
        {
            LevelPercent = next.LevelPercent,
            IsCharging = next.IsCharging,
            InEar = inEar
        };
    }
}
