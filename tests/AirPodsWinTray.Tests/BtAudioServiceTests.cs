using AirPodsWinTray.Services;

namespace AirPodsWinTray.Tests;

public class BtAudioServiceTests
{
    [Fact]
    public void ListDevices_EnumeratesEndpointsWithoutInvalidCast()
    {
        var devices = new BtAudioService().ListDevices();

        Assert.NotNull(devices);
        Assert.All(devices, device => Assert.NotEqual(Guid.Empty, device.ContainerId));
    }
}
