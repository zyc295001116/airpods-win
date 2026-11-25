namespace AirPodsWinTray.Core.Models;

public sealed class ProximityBeaconState
{
    public ushort ModelCode { get; init; }
    public string ModelCodeHex => $"0x{ModelCode:X4}";
    public PodBatteryInfo? Left { get; init; }
    public PodBatteryInfo? Right { get; init; }
    public PodBatteryInfo? Case { get; init; }
    public PodBatteryInfo? Single { get; init; }
    public bool IsMono { get; init; }
    public bool Parsed { get; init; }
    public string RawHex { get; init; } = "";
}