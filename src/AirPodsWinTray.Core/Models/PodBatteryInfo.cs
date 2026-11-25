namespace AirPodsWinTray.Core.Models;

public sealed class PodBatteryInfo
{
    public const int DisconnectedLevel = 15;

    public int LevelPercent { get; init; }
    public bool IsCharging { get; init; }
    public bool InEar { get; init; }
    public bool IsDisconnected => LevelPercent < 0;

    public static int NibbleToPercent(int nibble) =>
        nibble is >= 0 and <= 10 ? nibble * 10 : -1;
}