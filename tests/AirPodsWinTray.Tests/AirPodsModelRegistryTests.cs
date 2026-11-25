using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Registry;

namespace AirPodsWinTray.Tests;

public class AirPodsModelRegistryTests
{
    [Theory]
    [InlineData(0x0220, "AirPods", FormFactor.Stereo)]
    [InlineData(0x1320, "AirPods (3rd generation)", FormFactor.Stereo)]
    [InlineData(0x2420, "AirPods Pro 2 (USB-C)", FormFactor.Stereo)]
    [InlineData(0x1F20, "AirPods Max (USB-C)", FormFactor.Mono)]
    public void Resolve_KnownAirPodsCodes(ushort code, string name, FormFactor form)
    {
        var registry = AirPodsModelRegistry.LoadEmbedded();
        var model = registry.Resolve(code);
        Assert.True(model.IsKnown);
        Assert.Equal(name, model.Name);
        Assert.Equal(form, model.FormFactor);
    }
}