using AirPodsWinTray.Core.Policy;

namespace AirPodsWinTray.Tests;

public class DockPlacementTests
{
    [Fact]
    public void Expanded_RightEdge_ShowsTheFullWindow()
    {
        var point = DockPlacement.Expanded("Right", 0, 0, 1920, 1000, 320, 300, 1908, 400);

        Assert.Equal(new DockPlacement.Point(1600, 400), point);
    }

    [Fact]
    public void Expanded_LeftEdge_StaysInsideTheWorkArea()
    {
        var point = DockPlacement.Expanded("Left", 0, 0, 1920, 1000, 320, 300, -300, 900);

        Assert.Equal(new DockPlacement.Point(0, 700), point);
    }

    [Fact]
    public void Expanded_TopAndBottom_UseTheStripAlongTheEdge()
    {
        var top = DockPlacement.Expanded("Top", 10, 20, 1000, 800, 200, 100, 50, -80);
        var bottom = DockPlacement.Expanded("Bottom", 10, 20, 1000, 800, 200, 100, 900, 900);

        Assert.Equal(new DockPlacement.Point(50, 20), top);
        Assert.Equal(new DockPlacement.Point(810, 720), bottom);
    }

    [Fact]
    public void Beside_OpensOutsideTheWindow()
    {
        var right = DockPlacement.Beside(400, 200, 320, 300, 240, 180, 0, 0, 1920, 1080);
        var left = DockPlacement.Beside(1600, 200, 320, 300, 240, 180, 0, 0, 1920, 1080);

        Assert.Equal(new DockPlacement.Point(728, 200), right);
        Assert.Equal(new DockPlacement.Point(1352, 200), left);
        Assert.True(right.X >= 400 + 320);
        Assert.True(left.X + 240 <= 1600);
    }
}
