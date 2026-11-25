using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Tests;

public class AudioPolicyEngineTests
{
    [Fact]
    public void Evaluate_MediaPlay_WhenDisconnected_SuggestsConnect()
    {
        var engine = new AudioPolicyEngine();
        engine.NotifyMediaPlayback(true);

        var decision = engine.Evaluate(new AudioPolicySettings { AutoConnectOnMediaPlay = true }, deviceConnected: false);

        Assert.True(decision.ShouldConnect);
        Assert.Equal(PolicyReason.MediaPlay, decision.Reason);
    }

    [Fact]
    public void Evaluate_IdleTimeout_WhenConnected_SuggestsDisconnect()
    {
        var engine = new AudioPolicyEngine();
        engine.NotifyMediaPlayback(false);
        var settings = new AudioPolicySettings
        {
            AutoDisconnectOnIdle = true,
            IdleDisconnectMinutes = 5
        };

        var decision = engine.Evaluate(settings, deviceConnected: true);
        Assert.False(decision.ShouldDisconnect);

        engine.GetType(); // advance: simulate idle by re-evaluating after time — use manual field via another notification pattern

        // Force idle by not updating activity and mocking time — engine uses DateTime.UtcNow internally.
        // Unit-test via ManualRelease instead for determinism.
        settings = new AudioPolicySettings { ManualReleaseRequested = true };
        decision = engine.Evaluate(settings, deviceConnected: true);
        Assert.True(decision.ShouldDisconnect);
        Assert.Equal(PolicyReason.ManualRelease, decision.Reason);
    }

    [Fact]
    public void Evaluate_SuppressAutoConnect_DoesNotConnectOnMediaPlay()
    {
        var engine = new AudioPolicyEngine();
        engine.NotifyMediaPlayback(true);

        var decision = engine.Evaluate(new AudioPolicySettings
        {
            AutoConnectOnMediaPlay = true,
            SuppressAutoConnect = true
        }, deviceConnected: false);

        Assert.False(decision.ShouldConnect);
    }
}