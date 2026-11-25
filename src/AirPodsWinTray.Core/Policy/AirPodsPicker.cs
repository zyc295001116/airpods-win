namespace AirPodsWinTray.Core.Policy;

public enum AirPodsPickerMode
{
    None,
    Single,
    Multiple
}

public static class AirPodsPicker
{
    public static AirPodsPickerMode ModeForCount(int count) => count switch
    {
        <= 0 => AirPodsPickerMode.None,
        1 => AirPodsPickerMode.Single,
        _ => AirPodsPickerMode.Multiple
    };
}
