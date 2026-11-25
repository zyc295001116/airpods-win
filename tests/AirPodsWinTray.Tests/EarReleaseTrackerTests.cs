using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Tests;

public class EarReleaseTrackerTests
{
    [Fact]
    public void Observe_FiresOnceAfterHold_AndSuppressesUntilWorn()
    {
        var tracker = new EarReleaseTracker();
        var start = 1_000_000L;

        Assert.False(tracker.Observe(allOut: true, start));
        Assert.True(tracker.SuppressAutoConnect);
        Assert.False(tracker.Observe(allOut: true, start + EarReleaseTracker.Hold.Ticks - 1));
        Assert.True(tracker.Observe(allOut: true, start + EarReleaseTracker.Hold.Ticks));
        Assert.False(tracker.Observe(allOut: true, start + EarReleaseTracker.Hold.Ticks * 2));

        Assert.False(tracker.Observe(allOut: false, start + EarReleaseTracker.Hold.Ticks * 3));
        Assert.False(tracker.SuppressAutoConnect);
    }
}
