using AirPodsWinTray.Core.Models;
using AirPodsWinTray.Core.Parsing;
using AirPodsWinTray.Core.Registry;
using Windows.Devices.Bluetooth.Advertisement;

namespace AirPodsWinTray.Services;

public sealed class BleStateService : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly AirPodsModelRegistry _registry;
    private ProximityBeaconState? _lastState;
    private AirPodsModelDefinition? _lastDefinition;

    public event Action<ProximityBeaconState, AirPodsModelDefinition>? StateChanged;
    public event Action? CaseOpened;

    public BleStateService(AirPodsModelRegistry registry)
    {
        _registry = registry;
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += OnAdvertisementReceived;
    }

    public ProximityBeaconState? LastState => _lastState;
    public AirPodsModelDefinition? LastModel => _lastDefinition;

    public void Start() => _watcher.Start();
    public void Stop() => _watcher.Stop();

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var md in args.Advertisement.ManufacturerData)
        {
            if (md.CompanyId != AppleProximityParser.AppleCompanyId)
            {
                continue;
            }

            var buffer = md.Data;
            var bytes = new byte[buffer.Length];
            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
            reader.ReadBytes(bytes);

            var state = AppleProximityParser.ParseManufacturerData(bytes);
            if (!state.Parsed)
            {
                continue;
            }

            var definition = _registry.Resolve(state);
            var previousCase = _lastState?.Case?.LevelPercent;
            _lastState = state;
            _lastDefinition = definition;

            if (previousCase is null or PodBatteryInfo.DisconnectedLevel
                && state.Case?.LevelPercent >= 0)
            {
                CaseOpened?.Invoke();
            }

            StateChanged?.Invoke(state, definition);
        }
    }

    public void Dispose()
    {
        _watcher.Received -= OnAdvertisementReceived;
        Stop();
    }
}