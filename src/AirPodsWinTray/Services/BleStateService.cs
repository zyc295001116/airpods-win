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
    private int _sameBatteryCount;
    private string _boundName = "";
    private ushort? _ignoredModel;
    private int? _skippedPrefix;
    private readonly ProximityDisplay _display = new();

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

    public void SetBoundHint(string? containerName) => _boundName = containerName ?? "";

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

            if (bytes.Length == 0 || bytes[0] != 0x07)
            {
                var prefix = bytes.Length == 0 ? -1 : bytes[0];
                if (_skippedPrefix != prefix)
                {
                    _skippedPrefix = prefix;
                    // #region agent log
                    AgentDebugLog.Write("J", "BleStateService.OnAdvertisementReceived", "battery skipped", new { prefix });
                    // #endregion
                }

                continue;
            }

            var state = AppleProximityParser.ParseManufacturerData(bytes);
            if (!state.Parsed)
            {
                continue;
            }

            var definition = _registry.Resolve(state);
            if (!definition.IsKnown || !BoundBeaconFilter.Accepts(_boundName, definition.Name))
            {
                if (_ignoredModel != state.ModelCode)
                {
                    _ignoredModel = state.ModelCode;
                    // #region agent log
                    AgentDebugLog.Write("H", "BleStateService.OnAdvertisementReceived", "battery ignored", new { model = state.ModelCode });
                    // #endregion
                }

                continue;
            }

            _ignoredModel = null;

            var batteryChanged = _lastState?.Left?.LevelPercent != state.Left?.LevelPercent
                || _lastState?.Right?.LevelPercent != state.Right?.LevelPercent
                || _lastState?.Case?.LevelPercent != state.Case?.LevelPercent
                || _lastState?.Left?.InEar != state.Left?.InEar
                || _lastState?.Right?.InEar != state.Right?.InEar
                || _lastState?.ModelCode != state.ModelCode;
            if (batteryChanged)
            {
                var nibbles = state.RawHex is { Length: > 15 } ? state.RawHex.Substring(10, 6) : state.RawHex;
                // #region agent log
                AgentDebugLog.Write("H", "BleStateService.OnAdvertisementReceived", "battery status", new
                {
                    len = bytes.Length,
                    prefix = bytes.Length > 0 ? bytes[0] : -1,
                    model = state.ModelCode,
                    left = state.Left?.LevelPercent,
                    right = state.Right?.LevelPercent,
                    @case = state.Case?.LevelPercent,
                    inEarL = state.Left?.InEar,
                    inEarR = state.Right?.InEar,
                    chargeL = state.Left?.IsCharging,
                    chargeR = state.Right?.IsCharging,
                    mono = state.IsMono,
                    nibbles,
                    repeats = _sameBatteryCount
                });
                // #endregion
                _sameBatteryCount = 0;
            }
            else
            {
                _sameBatteryCount++;
            }

            var previousCase = _lastState?.Case?.LevelPercent;
            var shown = _display.Apply(state);
            _lastState = shown;
            _lastDefinition = definition;

            if (previousCase is null or PodBatteryInfo.DisconnectedLevel
                && state.Case?.LevelPercent >= 0)
            {
                CaseOpened?.Invoke();
            }

            StateChanged?.Invoke(shown, definition);
        }
    }

    public void Dispose()
    {
        _watcher.Received -= OnAdvertisementReceived;
        Stop();
    }
}