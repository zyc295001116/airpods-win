using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Tests;

public class HeadsetLinkTests
{
    [Fact]
    public void Connect_ThenAudioUp_BecomesActive()
    {
        var state = HeadsetLinkState.Unbound;
        state = HeadsetStateMachine.Next(state, HeadsetSignal.Bound);
        state = HeadsetStateMachine.Next(state, HeadsetSignal.ConnectRequested);

        Assert.Equal(HeadsetLinkState.Connecting, state);
        Assert.False(HeadsetStateMachine.CanConnect(state));

        state = HeadsetStateMachine.Next(state, HeadsetSignal.AudioActive);

        Assert.Equal(HeadsetLinkState.BoundActive, state);
        Assert.True(HeadsetStateMachine.CanDisconnect(state));
        Assert.False(HeadsetStateMachine.CanConnect(state));
    }

    [Fact]
    public void Disconnect_ThenAudioDown_ReturnsToBoundIdle()
    {
        var state = HeadsetLinkState.BoundActive;
        state = HeadsetStateMachine.Next(state, HeadsetSignal.DisconnectRequested);
        Assert.Equal(HeadsetLinkState.Disconnecting, state);

        state = HeadsetStateMachine.Next(state, HeadsetSignal.AudioInactive);
        Assert.Equal(HeadsetLinkState.BoundIdle, state);
        Assert.True(HeadsetStateMachine.CanConnect(state));
        Assert.Equal("已绑定 · 音频未连接", HeadsetStatusText.LinkLabel(state));
    }

    [Fact]
    public void ConnectAttempt_Timeout_StaysBoundButIdle()
    {
        var state = HeadsetStateMachine.Next(HeadsetLinkState.BoundIdle, HeadsetSignal.ConnectRequested);
        state = HeadsetStateMachine.Next(state, HeadsetSignal.AttemptFinished);

        Assert.Equal(HeadsetLinkState.BoundIdle, state);
    }

    [Fact]
    public void AudioDrop_WhileActive_DoesNotForgetBinding()
    {
        var state = HeadsetStateMachine.Next(HeadsetLinkState.BoundActive, HeadsetSignal.AudioInactive);

        Assert.Equal(HeadsetLinkState.BoundIdle, state);
        Assert.NotEqual(HeadsetLinkState.Unbound, state);
    }

    [Fact]
    public void BatteryLines_ShowLeftRightEarAndCase()
    {
        var state = new ProximityBeaconState
        {
            Parsed = true,
            Left = new PodBatteryInfo { LevelPercent = 80, InEar = true },
            Right = new PodBatteryInfo { LevelPercent = 70, InEar = false, IsCharging = true },
            Case = new PodBatteryInfo { LevelPercent = 50 }
        };

        var lines = HeadsetStatusText.BatteryLines(state);

        Assert.Equal(["左耳  80% · 在耳", "右耳  70% · 未入耳 · 充电中", "充电盒  50%"], lines);
    }

    [Fact]
    public void BatteryLines_WhenBeaconMissing_SaysUnknown()
    {
        var lines = HeadsetStatusText.BatteryLines(null);
        Assert.Equal(["电量未知（打开盒盖后更新）"], lines);
    }
}
