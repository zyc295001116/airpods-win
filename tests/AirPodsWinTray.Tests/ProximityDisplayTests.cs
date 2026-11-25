using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Parsing;

namespace AirPodsWinTray.Tests;

public class ProximityDisplayTests
{
    [Fact]
    public void Apply_PartialBeacon_KeepsLastKnownPodAndCase()
    {
        var display = new ProximityDisplay();
        display.Apply(Beacon(Pod(50, inEar: true), Pod(100, charging: true), Pod(70)));

        var shown = display.Apply(Beacon(Pod(50, inEar: true), Pod(-1), Pod(-1)));

        Assert.Equal(50, shown.Left!.LevelPercent);
        Assert.Equal(100, shown.Right!.LevelPercent);
        Assert.Equal(70, shown.Case!.LevelPercent);
    }

    [Fact]
    public void Apply_SingleContradictoryInEar_DoesNotFlip()
    {
        var display = new ProximityDisplay();
        display.Apply(Beacon(Pod(50, inEar: true), Pod(100, inEar: false), Pod(70)));

        var shown = display.Apply(Beacon(Pod(50, inEar: false), Pod(100, inEar: true), Pod(70)));

        Assert.True(shown.Left!.InEar);
        Assert.False(shown.Right!.InEar);
    }

    [Fact]
    public void Apply_RepeatedInEar_UpdatesAfterSecondPacket()
    {
        var display = new ProximityDisplay();
        display.Apply(Beacon(Pod(50, inEar: true), Pod(100, inEar: false), Pod(70)));
        display.Apply(Beacon(Pod(50, inEar: false), Pod(100, inEar: true), Pod(70)));

        var shown = display.Apply(Beacon(Pod(50, inEar: false), Pod(100, inEar: true), Pod(70)));

        Assert.False(shown.Left!.InEar);
        Assert.True(shown.Right!.InEar);
    }

    private static ProximityBeaconState Beacon(PodBatteryInfo left, PodBatteryInfo right, PodBatteryInfo box) =>
        new()
        {
            Parsed = true,
            ModelCode = 0x1320,
            Left = left,
            Right = right,
            Case = box
        };

    private static PodBatteryInfo Pod(int level, bool inEar = false, bool charging = false) =>
        new()
        {
            LevelPercent = level,
            InEar = inEar,
            IsCharging = charging
        };
}
