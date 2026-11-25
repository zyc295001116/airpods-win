namespace AirPodsWinTray.Core.Parsing;

public static class BoundBeaconFilter
{
    public static bool Accepts(string? boundName, string modelName)
    {
        if (modelName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(boundName))
        {
            return true;
        }

        var boundPro = boundName.Contains("Pro", StringComparison.OrdinalIgnoreCase);
        var boundMax = boundName.Contains("Max", StringComparison.OrdinalIgnoreCase);
        var modelPro = modelName.Contains("Pro", StringComparison.OrdinalIgnoreCase);
        var modelMax = modelName.Contains("Max", StringComparison.OrdinalIgnoreCase);
        if (boundPro != modelPro || boundMax != modelMax)
        {
            return false;
        }

        return boundName.Contains("AirPods", StringComparison.OrdinalIgnoreCase)
            && modelName.Contains("AirPods", StringComparison.OrdinalIgnoreCase);
    }
}
