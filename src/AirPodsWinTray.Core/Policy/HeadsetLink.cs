using AirPodsWinTray.Core.Models;

namespace AirPodsWinTray.Core.Policy;

/// <summary>
/// Bound means this headset is the one the tray controls.
/// Audio connection is separate: it only means Windows is routing sound to it.
/// </summary>
public enum HeadsetLinkState
{
    Unbound,
    BoundIdle,
    Connecting,
    BoundActive,
    Disconnecting
}

public enum HeadsetSignal
{
    Bound,
    Unbound,
    ConnectRequested,
    DisconnectRequested,
    AudioActive,
    AudioInactive,
    AttemptFinished
}

public static class HeadsetStateMachine
{
    public static HeadsetLinkState Next(HeadsetLinkState current, HeadsetSignal signal)
    {
        if (signal == HeadsetSignal.Unbound)
        {
            return HeadsetLinkState.Unbound;
        }

        return (current, signal) switch
        {
            (HeadsetLinkState.Unbound, HeadsetSignal.Bound) => HeadsetLinkState.BoundIdle,
            (HeadsetLinkState.Unbound, HeadsetSignal.AudioActive) => HeadsetLinkState.BoundActive,

            (HeadsetLinkState.BoundIdle, HeadsetSignal.ConnectRequested) => HeadsetLinkState.Connecting,
            (HeadsetLinkState.BoundIdle, HeadsetSignal.AudioActive) => HeadsetLinkState.BoundActive,

            (HeadsetLinkState.Connecting, HeadsetSignal.AudioActive) => HeadsetLinkState.BoundActive,
            (HeadsetLinkState.Connecting, HeadsetSignal.AttemptFinished) => HeadsetLinkState.BoundIdle,
            (HeadsetLinkState.Connecting, HeadsetSignal.DisconnectRequested) => HeadsetLinkState.Disconnecting,

            (HeadsetLinkState.BoundActive, HeadsetSignal.DisconnectRequested) => HeadsetLinkState.Disconnecting,
            (HeadsetLinkState.BoundActive, HeadsetSignal.AudioInactive) => HeadsetLinkState.BoundIdle,

            (HeadsetLinkState.Disconnecting, HeadsetSignal.AudioInactive) => HeadsetLinkState.BoundIdle,
            (HeadsetLinkState.Disconnecting, HeadsetSignal.AttemptFinished) => HeadsetLinkState.BoundIdle,
            (HeadsetLinkState.Disconnecting, HeadsetSignal.ConnectRequested) => HeadsetLinkState.Connecting,

            _ => current
        };
    }

    public static bool CanConnect(HeadsetLinkState state) =>
        state is HeadsetLinkState.Unbound or HeadsetLinkState.BoundIdle;

    public static bool CanDisconnect(HeadsetLinkState state) =>
        state is HeadsetLinkState.BoundActive or HeadsetLinkState.Connecting;
}

public static class HeadsetStatusText
{
    public static string LinkLabel(HeadsetLinkState state) => state switch
    {
        HeadsetLinkState.Unbound => "未绑定",
        HeadsetLinkState.BoundIdle => "已绑定 · 音频未连接",
        HeadsetLinkState.Connecting => "正在连接…",
        HeadsetLinkState.BoundActive => "已绑定 · 音频已连接",
        HeadsetLinkState.Disconnecting => "正在断开…",
        _ => "未知"
    };

    public static IReadOnlyList<string> BatteryLines(ProximityBeaconState? state)
    {
        if (state is not { Parsed: true })
        {
            return ["电量未知（打开盒盖后更新）"];
        }

        if (state.IsMono)
        {
            return [FormatPod("耳机", state.Single, showEar: false)];
        }

        return
        [
            FormatPod("左耳", state.Left, showEar: true),
            FormatPod("右耳", state.Right, showEar: true),
            FormatPod("充电盒", state.Case, showEar: false)
        ];
    }

    private static string FormatPod(string label, PodBatteryInfo? pod, bool showEar)
    {
        if (pod == null || pod.IsDisconnected)
        {
            return $"{label}  —";
        }

        var ear = showEar ? (pod.InEar ? " · 在耳" : " · 未入耳") : "";
        var charging = pod.IsCharging ? " · 充电中" : "";
        return $"{label}  {pod.LevelPercent}%{ear}{charging}";
    }
}
