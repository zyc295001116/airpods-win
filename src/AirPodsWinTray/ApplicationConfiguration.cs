namespace AirPodsWinTray;

internal static class ApplicationConfiguration
{
    public static void Initialize()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    }
}