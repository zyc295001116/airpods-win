using System.Runtime.InteropServices;
using AirPodsWinTray.Interop;

namespace AirPodsWinTray.Services;

/// <summary>
/// Connect/disconnect Bluetooth audio via KSPROPERTY_ONESHOT (ToothTray approach).
/// KS filter ids are remembered so reconnect still works after the endpoint unplugs.
/// </summary>
public sealed class BtAudioService
{
    private const uint ClsCtxAll = 23;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, KnownGroup> _known = new();

    public void Seed(Guid containerId, string name, IEnumerable<string> endpointIds, IEnumerable<string> ksDeviceIds)
    {
        lock (_gate)
        {
            if (_known.ContainsKey(containerId))
            {
                return;
            }

            var group = new KnownGroup(containerId, name);
            group.EndpointIds.AddRange(endpointIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct());
            group.KsDeviceIds.AddRange(ksDeviceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct());
            _known[containerId] = group;
        }
    }

    public IReadOnlyList<BtAudioDevice> ListAirPods()
    {
        lock (_gate)
        {
            DiscoverUnlocked();
            return _known.Values
                .Where(group => group.KsDeviceIds.Count > 0
                    && group.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
                .Select(ToDevice)
                .OrderBy(device => device.ContainerName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public BtAudioDevice? FindById(Guid containerId)
    {
        lock (_gate)
        {
            if (!_known.TryGetValue(containerId, out var group))
            {
                return null;
            }

            RefreshGroupStateUnlocked(group);
            return ToDevice(group);
        }
    }

    public string? FindStereoPlaybackEndpoint(Guid containerId)
    {
        lock (_gate)
        {
            if (!_known.TryGetValue(containerId, out var group))
            {
                return null;
            }

            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            string? fallback = null;
            foreach (var endpointId in group.EndpointIds)
            {
                if (!TryGetDevice(enumerator, endpointId, out var device, out _)
                    || device.OpenPropertyStore(0, out var store) < 0
                    || store is null)
                {
                    continue;
                }

                var name = GetStringProperty(store, PropertyKeys.PKEY_Device_FriendlyName);
                if (name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Microphone", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (name.Contains("Stereo", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Headphone", StringComparison.OrdinalIgnoreCase))
                {
                    return endpointId;
                }

                fallback ??= endpointId;
            }

            return fallback;
        }
    }

    public IReadOnlyList<BtAudioDevice> ListDevices()
    {
        lock (_gate)
        {
            DiscoverUnlocked();
            return SnapshotUnlocked();
        }
    }

    public void Discover()
    {
        lock (_gate)
        {
            DiscoverUnlocked();
        }
    }

    public bool? DefaultPlaybackIsKnown(string nameContains)
    {
        lock (_gate)
        {
            if (!TryFindUnlocked(nameContains, out var group) || group.EndpointIds.Count == 0)
            {
                return null;
            }

            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (!TryGetDefaultPlayback(enumerator, out var device) || device.GetId(out var id) < 0)
            {
                return null;
            }

            return group.EndpointIds.Contains(id);
        }
    }

    public BtAudioDevice? FindByName(string nameContains)
    {
        lock (_gate)
        {
            if (!TryFindUnlocked(nameContains, out var group))
            {
                return null;
            }

            RefreshGroupStateUnlocked(group);
            return ToDevice(group);
        }
    }

    public bool Connect(Guid containerId) => SendProperty(containerId, KsBtAudio.OneshotReconnect);

    public bool Disconnect(Guid containerId) => SendProperty(containerId, KsBtAudio.OneshotDisconnect);

    public bool ConnectByName(string nameContains) =>
        SendByName(nameContains, KsBtAudio.OneshotReconnect);

    public bool DisconnectByName(string nameContains) =>
        SendByName(nameContains, KsBtAudio.OneshotDisconnect);

    private bool SendByName(string nameContains, uint propertyId)
    {
        var started = Environment.TickCount64;
        lock (_gate)
        {
            var foundBeforeDiscover = TryFindUnlocked(nameContains, out var group);
            if (!foundBeforeDiscover)
            {
                DiscoverUnlocked();
                foundBeforeDiscover = TryFindUnlocked(nameContains, out group);
            }

            var sent = foundBeforeDiscover && SendUnlocked(group, propertyId);
            if (!sent)
            {
                DiscoverUnlocked();
                sent = TryFindUnlocked(nameContains, out group) && SendUnlocked(group, propertyId);
            }

            // #region agent log
            AgentDebugLog.Write("B", "BtAudioService.SendByName", "send finished", new
            {
                propertyId,
                found = foundBeforeDiscover,
                sent,
                ms = Environment.TickCount64 - started,
                known = _known.Count,
                ks = group?.KsDeviceIds.Count ?? 0,
                connected = group?.IsConnected ?? false
            });
            // #endregion
            return sent;
        }
    }

    private bool SendProperty(Guid containerId, uint propertyId)
    {
        lock (_gate)
        {
            if (!_known.TryGetValue(containerId, out var group))
            {
                DiscoverUnlocked();
                if (!_known.TryGetValue(containerId, out group))
                {
                    return false;
                }
            }

            if (SendUnlocked(group, propertyId))
            {
                return true;
            }

            DiscoverUnlocked();
            return _known.TryGetValue(containerId, out group) && SendUnlocked(group, propertyId);
        }
    }

    private bool SendUnlocked(KnownGroup group, uint propertyId)
    {
        if (group.KsDeviceIds.Count == 0)
        {
            return false;
        }

        var ksProperty = new KsProperty
        {
            Set = KsBtAudio.PropertySet,
            Id = propertyId,
            Flags = KsBtAudio.KsPropertyTypeGet
        };
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        var any = false;
        var steps = new List<string>(group.KsDeviceIds.Count);
        foreach (var ksId in group.KsDeviceIds)
        {
            if (!TryGetDevice(enumerator, ksId, out var device, out var getHr))
            {
                steps.Add($"get:{getHr}");
                continue;
            }

            var iidKs = typeof(IKsControl).GUID;
            var actHr = device.Activate(ref iidKs, ClsCtxAll, IntPtr.Zero, out var ksObj);
            if (actHr < 0 || ksObj is not IKsControl ks)
            {
                steps.Add($"act:{actHr}");
                continue;
            }

            var ksHr = ks.KsProperty(ref ksProperty, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _);
            steps.Add($"ks:{ksHr}");
            if (ksHr >= 0)
            {
                any = true;
            }
        }

        // #region agent log
        AgentDebugLog.Write("D", "BtAudioService.SendUnlocked", "ks property results", new { propertyId, any, steps });
        // #endregion
        return any;
    }

    private void DiscoverUnlocked()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        var hr = enumerator.EnumAudioEndpoints(DataFlow.All, DeviceState.Active | DeviceState.Unplugged, out var collection);
        uint count = 0;
        var countHr = collection?.GetCount(out count) ?? -1;
        if (hr < 0 || collection is null || countHr < 0)
        {
            // #region agent log
            AgentDebugLog.Write("B", "BtAudioService.DiscoverUnlocked", "enum endpoints failed", new { hr, countHr });
            // #endregion
            return;
        }

        var seen = new HashSet<Guid>();
        var active = new Dictionary<Guid, bool>();
        var ksHits = 0;

        for (uint i = 0; i < count; i++)
        {
            if (!TryItem(collection, i, out var device) || device.GetState(out var state) < 0)
            {
                continue;
            }

            if (device.OpenPropertyStore(0, out var propertyStore) < 0 || propertyStore is null)
            {
                continue;
            }

            var name = GetStringProperty(propertyStore, PropertyKeys.PKEY_Device_FriendlyName);
            var containerId = GetGuidProperty(propertyStore, PropertyKeys.PKEY_Device_ContainerId);
            if (containerId == Guid.Empty)
            {
                continue;
            }

            var foundKs = TryGetKsControl(device, out var ksDeviceId);
            if (foundKs)
            {
                ksHits++;
            }
            if (!_known.TryGetValue(containerId, out var group))
            {
                if (!foundKs)
                {
                    continue;
                }

                group = new KnownGroup(containerId, PreferName(name));
                _known[containerId] = group;
            }
            else if (IsGenericName(group.Name) && !string.IsNullOrWhiteSpace(name))
            {
                group.Name = name;
            }
            else if (IsPreferredName(name) && !IsPreferredName(group.Name))
            {
                group.Name = name;
            }

            seen.Add(containerId);
            active[containerId] = active.GetValueOrDefault(containerId) || state == DeviceState.Active;

            if (device.GetId(out var endpointId) >= 0 && !string.IsNullOrWhiteSpace(endpointId) && !group.EndpointIds.Contains(endpointId))
            {
                group.EndpointIds.Add(endpointId);
            }

            if (foundKs && !group.KsDeviceIds.Contains(ksDeviceId))
            {
                group.KsDeviceIds.Add(ksDeviceId);
            }
        }

        foreach (var (id, isActive) in active)
        {
            _known[id].IsConnected = isActive;
        }

        foreach (var (id, group) in _known.ToList())
        {
            if (seen.Contains(id))
            {
                continue;
            }

            if (EndpointAlive(group))
            {
                RefreshGroupStateUnlocked(group);
            }
            else
            {
                _known.Remove(id);
            }
        }

        // #region agent log
        AgentDebugLog.Write("B", "BtAudioService.DiscoverUnlocked", "discover finished", new
        {
            endpoints = count,
            ksHits,
            known = _known.Count,
            withKs = _known.Values.Count(g => g.KsDeviceIds.Count > 0),
            active = _known.Values.Count(g => g.IsConnected)
        });
        // #endregion
    }

    private void RefreshGroupStateUnlocked(KnownGroup group)
    {
        if (group.EndpointIds.Count == 0)
        {
            return;
        }

        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        var saw = false;
        var anyActive = false;
        foreach (var endpointId in group.EndpointIds)
        {
            if (!TryGetDevice(enumerator, endpointId, out var device, out _))
            {
                continue;
            }

            saw = true;
            if (device.GetState(out var state) >= 0 && state == DeviceState.Active)
            {
                anyActive = true;
            }
        }

        if (saw)
        {
            group.IsConnected = anyActive;
        }
    }

    private static bool EndpointAlive(KnownGroup group)
    {
        if (group.EndpointIds.Count == 0)
        {
            return false;
        }

        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        foreach (var endpointId in group.EndpointIds)
        {
            if (TryGetDevice(enumerator, endpointId, out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindUnlocked(string nameContains, out KnownGroup group)
    {
        group = null!;
        foreach (var candidate in _known.Values)
        {
            if (candidate.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
                || nameContains.Contains(candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                group = candidate;
                return true;
            }
        }

        return false;
    }

    private List<BtAudioDevice> SnapshotUnlocked() =>
        _known.Values
            .Select(ToDevice)
            .OrderBy(d => d.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static BtAudioDevice ToDevice(KnownGroup group) => new()
    {
        ContainerName = group.Name,
        ContainerId = group.ContainerId,
        IsConnected = group.IsConnected,
        EndpointIds = group.EndpointIds.ToArray(),
        KsDeviceIds = group.KsDeviceIds.ToArray()
    };

    private static bool TryGetDevice(IMMDeviceEnumerator enumerator, string id, out IMMDevice device, out int hr)
    {
        try
        {
            hr = enumerator.GetDevice(id, out device!);
            if (hr < 0 || device is null)
            {
                device = null!;
                return false;
            }

            return true;
        }
        catch (InvalidCastException)
        {
            device = null!;
            hr = unchecked((int)0x80004002);
            return false;
        }
    }

    private static bool TryGetDefaultPlayback(IMMDeviceEnumerator enumerator, out IMMDevice device)
    {
        try
        {
            if (enumerator.GetDefaultAudioEndpoint(DataFlow.Render, 1, out device!) < 0 || device is null)
            {
                device = null!;
                return false;
            }

            return true;
        }
        catch (InvalidCastException)
        {
            device = null!;
            return false;
        }
    }

    private static bool TryItem(IMMDeviceCollection collection, uint index, out IMMDevice device)
    {
        try
        {
            if (collection.Item(index, out device!) < 0 || device is null)
            {
                device = null!;
                return false;
            }

            return true;
        }
        catch (InvalidCastException)
        {
            device = null!;
            return false;
        }
    }

    private static string PreferName(string name) => string.IsNullOrWhiteSpace(name) ? "AirPods" : name;

    private static bool IsGenericName(string name) =>
        string.IsNullOrWhiteSpace(name) || name.Equals("AirPods", StringComparison.OrdinalIgnoreCase);

    private static bool IsPreferredName(string name) =>
        !name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("Headset", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetKsControl(IMMDevice endpointDevice, out string ksDeviceId)
    {
        ksDeviceId = "";
        var iidTopology = typeof(IDeviceTopology).GUID;
        var hr = endpointDevice.Activate(ref iidTopology, ClsCtxAll, IntPtr.Zero, out var topologyObj);
        if (hr < 0 || topologyObj is not IDeviceTopology topology)
        {
            return false;
        }

        hr = topology.GetConnectorCount(out var connectorCount);
        if (hr < 0)
        {
            return false;
        }

        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();

        for (uint i = 0; i < connectorCount; i++)
        {
            hr = topology.GetConnector(i, out var connector);
            if (hr < 0 || connector is null)
            {
                continue;
            }

            hr = connector.GetConnectedTo(out var otherConnector);
            if (hr < 0 || otherConnector is not IPart part)
            {
                continue;
            }

            hr = part.GetTopologyObject(out var otherTopology);
            if (hr < 0 || otherTopology is null)
            {
                continue;
            }

            hr = otherTopology.GetDeviceId(out var otherDeviceId);
            if (hr < 0 || otherDeviceId is null || !otherDeviceId.Contains("bth", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetDevice(enumerator, otherDeviceId, out var otherDevice, out _))
            {
                continue;
            }

            var iidKs = typeof(IKsControl).GUID;
            if (otherDevice.Activate(ref iidKs, ClsCtxAll, IntPtr.Zero, out var ksObj) < 0 || ksObj is not IKsControl)
            {
                continue;
            }

            ksDeviceId = otherDeviceId;
            return true;
        }

        return false;
    }

    private static string GetStringProperty(IPropertyStore store, PropertyKey key)
    {
        store.GetValue(ref key, out var value);
        try
        {
            if (value.vt == 31 && value.pointer != IntPtr.Zero) // VT_LPWSTR
            {
                return Marshal.PtrToStringUni(value.pointer) ?? string.Empty;
            }
        }
        finally
        {
            PropVariantClear(ref value);
        }

        return string.Empty;
    }

    private static Guid GetGuidProperty(IPropertyStore store, PropertyKey key)
    {
        store.GetValue(ref key, out var value);
        try
        {
            if (value.vt == 72 && value.pointer != IntPtr.Zero) // VT_CLSID
            {
                var bytes = new byte[16];
                Marshal.Copy(value.pointer, bytes, 0, 16);
                return new Guid(bytes);
            }
        }
        finally
        {
            PropVariantClear(ref value);
        }

        return Guid.Empty;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    private sealed class KnownGroup(Guid containerId, string name)
    {
        public Guid ContainerId { get; } = containerId;
        public string Name { get; set; } = name;
        public List<string> EndpointIds { get; } = [];
        public List<string> KsDeviceIds { get; } = [];
        public bool IsConnected { get; set; }
    }
}
