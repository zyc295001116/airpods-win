using System.Runtime.InteropServices;
using AirPodsWinTray.Interop;
using Windows.Devices.Enumeration;

namespace AirPodsWinTray.Services;

/// <summary>
/// Connect/disconnect Bluetooth audio via KSPROPERTY_ONESHOT (ToothTray approach).
/// </summary>
public sealed class BtAudioService
{
    private readonly Dictionary<Guid, ConnectorGroup> _groups = new();

    public IReadOnlyList<BtAudioDevice> ListDevices()
    {
        _groups.Clear();
        EnumerateIntoGroups();

        return _groups.Values
            .Select(g => new BtAudioDevice
            {
                ContainerName = g.ContainerName,
                ContainerId = g.ContainerId,
                IsConnected = g.IsConnected
            })
            .OrderBy(d => d.ContainerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public BtAudioDevice? FindByName(string nameContains)
    {
        return ListDevices()
            .FirstOrDefault(d => d.ContainerName.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
    }

    public bool Connect(Guid containerId) => SendProperty(containerId, KsBtAudio.OneshotReconnect);

    public bool Disconnect(Guid containerId) => SendProperty(containerId, KsBtAudio.OneshotDisconnect);

    public bool ConnectByName(string nameContains)
    {
        var device = FindByName(nameContains);
        return device != null && Connect(device.ContainerId);
    }

    public bool DisconnectByName(string nameContains)
    {
        var device = FindByName(nameContains);
        return device != null && Disconnect(device.ContainerId);
    }

    private bool SendProperty(Guid containerId, uint propertyId)
    {
        if (!_groups.TryGetValue(containerId, out var group) && !TryLoadGroup(containerId, out group))
        {
            return false;
        }

        var ksProperty = new KsProperty
        {
            Set = KsBtAudio.PropertySet,
            Id = propertyId,
            Flags = KsBtAudio.KsPropertyTypeGet
        };

        var ok = true;
        foreach (var ks in group.KsControls)
        {
            var hr = ks.KsProperty(ref ksProperty, (uint)Marshal.SizeOf<KsProperty>(), IntPtr.Zero, 0, out _);
            if (hr < 0)
            {
                ok = false;
            }
        }

        return ok && group.KsControls.Count > 0;
    }

    private void EnumerateIntoGroups()
    {
        var containers = EnumerateContainers();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        enumerator.EnumAudioEndpoints(DataFlow.All, DeviceState.Active | DeviceState.Unplugged, out var collection);
        collection.GetCount(out var count);

        for (uint i = 0; i < count; i++)
        {
            collection.Item(i, out var device);
            device.GetState(out var state);

            device.OpenPropertyStore(0, out var propertyStore);
            var name = GetStringProperty(propertyStore, PropertyKeys.PKEY_Device_FriendlyName);
            var containerId = GetGuidProperty(propertyStore, PropertyKeys.PKEY_Device_ContainerId);
            if (containerId == Guid.Empty)
            {
                continue;
            }

            if (!TryGetKsControl(device, out var ksControl))
            {
                continue;
            }

            if (!_groups.TryGetValue(containerId, out var group))
            {
                var containerName = containers.TryGetValue(containerId, out var cname) ? cname : name;
                group = new ConnectorGroup(containerId, containerName);
                _groups[containerId] = group;
            }

            group.KsControls.Add(ksControl);
            if (state == DeviceState.Active)
            {
                group.IsConnected = true;
            }
        }
    }

    private bool TryLoadGroup(Guid containerId, out ConnectorGroup group)
    {
        EnumerateIntoGroups();
        return _groups.TryGetValue(containerId, out group!);
    }

    private static bool TryGetKsControl(IMMDevice endpointDevice, out IKsControl ksControl)
    {
        ksControl = null!;
        var iidTopology = typeof(IDeviceTopology).GUID;
        endpointDevice.Activate(ref iidTopology, 1, IntPtr.Zero, out var topologyObj);
        var topology = (IDeviceTopology)topologyObj;
        topology.GetConnectorCount(out var connectorCount);

        var iidEnumerator = typeof(IMMDeviceEnumerator).GUID;
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();

        for (uint i = 0; i < connectorCount; i++)
        {
            topology.GetConnector(i, out var connector);
            connector.GetConnectedTo(out var otherConnector);
            if (otherConnector == null)
            {
                continue;
            }

            var part = (IPart)otherConnector;
            part.GetTopologyObject(out var otherTopology);
            otherTopology.GetDeviceId(out var otherDeviceId);
            if (otherDeviceId is null || !(otherDeviceId.Contains("bth", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            enumerator.GetDevice(otherDeviceId, out var otherDevice);
            var iidKs = typeof(IKsControl).GUID;
            otherDevice.Activate(ref iidKs, 1, IntPtr.Zero, out var ksObj);
            ksControl = (IKsControl)ksObj;
            return true;
        }

        return false;
    }

    private static Dictionary<Guid, string> EnumerateContainers()
    {
        var map = new Dictionary<Guid, string>();
        var containers = DeviceInformation.FindAllAsync(string.Empty, null, DeviceInformationKind.DeviceContainer)
            .AsTask().GetAwaiter().GetResult();
        foreach (var info in containers)
        {
            if (Guid.TryParse(info.Id, out var id))
            {
                map[id] = info.Name;
            }
        }

        return map;
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

    private sealed class ConnectorGroup(Guid containerId, string containerName)
    {
        public Guid ContainerId { get; } = containerId;
        public string ContainerName { get; } = containerName;
        public List<IKsControl> KsControls { get; } = [];
        public bool IsConnected { get; set; }
    }
}