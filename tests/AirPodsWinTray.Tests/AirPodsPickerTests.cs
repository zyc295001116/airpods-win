using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Tests;

public class AirPodsPickerTests
{
    [Theory]
    [InlineData(0, AirPodsPickerMode.None)]
    [InlineData(-1, AirPodsPickerMode.None)]
    [InlineData(1, AirPodsPickerMode.Single)]
    [InlineData(2, AirPodsPickerMode.Multiple)]
    public void ModeForCount_MatchesPairedDeviceCount(int count, AirPodsPickerMode expected)
    {
        Assert.Equal(expected, AirPodsPicker.ModeForCount(count));
    }
}
