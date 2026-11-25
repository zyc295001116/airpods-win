using AirPodsWinTray.Core.Parsing;
using AirPodsWinTray.Core.Registry;

namespace AirPodsWinTray.Tests;

public class AppleProximityParserTests
{
    [Fact]
    public void Parse_AirPods3_IdentifiesModel1320()
    {
        var payload = BuildStereoBeacon(modelByte0: 0x13, modelByte1: 0x20, left: 8, right: 7, caseBattery: 9);
        var state = AppleProximityParser.ParseManufacturerData(payload);

        Assert.True(state.Parsed);
        Assert.Equal(0x1320, state.ModelCode);
        Assert.False(state.IsMono);
        Assert.Equal(70, state.Left!.LevelPercent);
        Assert.Equal(80, state.Right!.LevelPercent);
        Assert.Equal(90, state.Case!.LevelPercent);
        Assert.Contains("1320", state.RawHex, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithCompanyPrefix_Normalizes()
    {
        var inner = BuildStereoBeacon(0x13, 0x20, 5, 5, 10);
        var withPrefix = new byte[inner.Length + 2];
        withPrefix[0] = 0x4C;
        withPrefix[1] = 0x00;
        inner.CopyTo(withPrefix, 2);

        var state = AppleProximityParser.ParseManufacturerData(withPrefix);
        Assert.Equal(0x1320, state.ModelCode);
    }

    [Fact]
    public void Registry_AllEmbeddedModels_RoundTrip()
    {
        var registry = AirPodsModelRegistry.LoadEmbedded();
        Assert.Equal(11, registry.KnownModels.Count);
        var airPods3 = registry.Resolve(0x1320);
        Assert.Equal("AirPods (3rd generation)", airPods3.Name);
        Assert.False(airPods3.SupportsAnc);

        var unknown = registry.Resolve(0xBEEF);
        Assert.False(unknown.IsKnown);
    }

    private static byte[] BuildStereoBeacon(byte modelByte0, byte modelByte1, int left, int right, int caseBattery)
    {
        var payload = new byte[AppleProximityParser.ExpectedPayloadLength];
        payload[3] = modelByte0;
        payload[4] = modelByte1;
        payload[5] = 0x00; // clear isFlipped nibble at hex char 10
        // OpenPods indexes a 54-char hex string: one hex digit per battery nibble.
        payload[6] = (byte)((left << 4) | (right & 0x0F)); // chars 12,13
        payload[7] = (byte)((caseBattery & 0x0F) | 0x90); // char 15 = low nibble of byte 7
        payload[11] = 0x0A;
        return payload;
    }
}