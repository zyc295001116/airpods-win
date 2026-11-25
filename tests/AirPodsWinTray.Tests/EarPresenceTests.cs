using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Tests;

public class EarPresenceTests
{
    [Fact]
    public void AllOutOfEar_BothBudsOut_IsTrue()
    {
        Assert.True(EarPresence.AllOutOfEar(Stereo(Pod(80, inEar: false), Pod(70, inEar: false))));
    }

    [Fact]
    public void AllOutOfEar_OneBudStillWorn_IsFalse()
    {
        Assert.False(EarPresence.AllOutOfEar(Stereo(Pod(80, inEar: true), Pod(70, inEar: false))));
    }

    [Fact]
    public void AllOutOfEar_BothInCase_IsTrue()
    {
        Assert.True(EarPresence.AllOutOfEar(Stereo(Pod(-1, inEar: false), Pod(-1, inEar: true))));
    }

    [Fact]
    public void AllOutOfEar_Mono_IsFalse()
    {
        var state = new ProximityBeaconState
        {
            Parsed = true,
            IsMono = true,
            Single = Pod(50, inEar: false)
        };

        Assert.False(EarPresence.AllOutOfEar(state));
    }

    [Fact]
    public void ShouldReleaseAudio_Degraded_IsFalse()
    {
        var state = Stereo(Pod(80, inEar: false), Pod(70, inEar: false));

        Assert.False(EarPresence.ShouldReleaseAudio(state, EarDetectReliability.Degraded));
        Assert.True(EarPresence.ShouldReleaseAudio(state, EarDetectReliability.Normal));
    }

    private static ProximityBeaconState Stereo(PodBatteryInfo left, PodBatteryInfo right) =>
        new()
        {
            Parsed = true,
            ModelCode = 0x1320,
            Left = left,
            Right = right,
            Case = Pod(90, inEar: false)
        };

    private static PodBatteryInfo Pod(int level, bool inEar) =>
        new() { LevelPercent = level, InEar = inEar };
}
