using AirPodsWinTray.Core.Configuration;
using AirPodsWinTray.Services;

namespace AirPodsWinTray;

internal static class Program
{
    private const string MutexName = "AirPodsWinTray.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            return RunCli(args);
        }

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        var config = AppConfig.Load();
        Application.Run(new TrayApplicationContext(config));
        return 0;
    }

  private static int RunCli(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        var bt = new BtAudioService();
        var name = args.Length > 1 ? args[1] : "AirPods";

        return command switch
        {
            "list" => ListDevices(bt),
            "connect" => bt.ConnectByName(name) ? 0 : 1,
            "disconnect" => bt.DisconnectByName(name) ? 0 : 1,
            _ => PrintCliHelp()
        };
    }

    private static int ListDevices(BtAudioService bt)
    {
        foreach (var d in bt.ListDevices())
        {
            Console.WriteLine($"{d.ContainerName}\t{d.ContainerId}\t{(d.IsConnected ? "connected" : "disconnected")}");
        }

        return 0;
    }

    private static int PrintCliHelp()
    {
        Console.WriteLine("AirPodsWinTray [list|connect|disconnect] [deviceName]");
        return 1;
    }
}