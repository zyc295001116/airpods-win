namespace AirPodsWinTray.Core.Policy;

public static class DockPlacement
{
    public readonly record struct Point(int X, int Y);

    public static Point Expanded(
        string? edge,
        int areaX,
        int areaY,
        int areaWidth,
        int areaHeight,
        int width,
        int height,
        int stripX,
        int stripY)
    {
        var maxX = Math.Max(areaX, areaX + areaWidth - width);
        var maxY = Math.Max(areaY, areaY + areaHeight - height);
        var x = Math.Clamp(stripX, areaX, maxX);
        var y = Math.Clamp(stripY, areaY, maxY);
        return edge switch
        {
            "Left" => new Point(areaX, y),
            "Right" => new Point(maxX, y),
            "Top" => new Point(x, areaY),
            "Bottom" => new Point(x, maxY),
            _ => new Point(x, y)
        };
    }

    public static Point Beside(
        int windowX,
        int windowY,
        int windowWidth,
        int windowHeight,
        int menuWidth,
        int menuHeight,
        int areaX,
        int areaY,
        int areaWidth,
        int areaHeight)
    {
        const int gap = 8;
        var maxX = Math.Max(areaX, areaX + areaWidth - menuWidth);
        var maxY = Math.Max(areaY, areaY + areaHeight - menuHeight);
        var right = windowX + windowWidth + gap;
        var left = windowX - gap - menuWidth;
        var x = right + menuWidth <= areaX + areaWidth ? right
            : left >= areaX ? left
            : Math.Clamp(right, areaX, maxX);
        var y = Math.Clamp(windowY, areaY, maxY);
        return new Point(x, y);
    }
}
