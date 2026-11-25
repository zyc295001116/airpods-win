using System.Runtime.InteropServices;

namespace AirPodsWinTray.Interop;

internal enum DataFlow
{
    Render = 0,
    Capture = 1,
    All = 2
}

[Flags]
internal enum DeviceState : uint
{
    Active = 0x1,
    Disabled = 0x2,
    NotPresent = 0x4,
    Unplugged = 0x8,
    All = 0xF
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FmtId;
    public uint Pid;
}

internal static class PropertyKeys
{
    public static readonly PropertyKey PKEY_Device_FriendlyName = new()
    {
        FmtId = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        Pid = 14
    };

    public static readonly PropertyKey PKEY_Device_ContainerId = new()
    {
        FmtId = new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),
        Pid = 2
    };
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort vt;
    public ushort wReserved1;
    public ushort wReserved2;
    public ushort wReserved3;
    public IntPtr pointer;
    public IntPtr pointer2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KsProperty
{
    public Guid Set;
    public uint Id;
    public uint Flags;
}

internal static class KsBtAudio
{
    public static readonly Guid PropertySet = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    public const uint OneshotReconnect = 0;
    public const uint OneshotDisconnect = 1;
    public const uint KsPropertyTypeGet = 1;
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
    [PreserveSig] int Commit();
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);
    [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, uint role, out IMMDevice device);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore properties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out DeviceState state);
}

[ComImport]
[Guid("2A07407E-6497-4A18-9787-32F79BD0D98F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceTopology
{
    [PreserveSig] int GetConnectorCount(out uint count);
    [PreserveSig] int GetConnector(uint index, out IConnector connector);
    [PreserveSig] int GetSubunitCount(out uint count);
    [PreserveSig] int GetSubunit(uint index, out IntPtr subunit);
    [PreserveSig] int GetPartById(uint id, out IPart part);
    [PreserveSig] int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetSignalPath(IPart from, IPart to, [MarshalAs(UnmanagedType.Bool)] bool rejectMixedPaths, out IntPtr parts);
}

[ComImport]
[Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IConnector
{
    [PreserveSig] int GetType(out int type);
    [PreserveSig] int GetDataFlow(out DataFlow flow);
    [PreserveSig] int ConnectTo(IConnector connectTo);
    [PreserveSig] int Disconnect();
    [PreserveSig] int IsConnected(out int connected);
    [PreserveSig] int GetConnectedTo(out IConnector connectedTo);
    [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
}

[ComImport]
[Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPart
{
    [PreserveSig] int GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    [PreserveSig] int GetLocalId(out uint id);
    [PreserveSig] int GetGlobalId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetPartType(out int type);
    [PreserveSig] int GetSubType(out Guid subType);
    [PreserveSig] int GetControlInterfaceCount(out uint count);
    [PreserveSig] int GetControlInterface(uint index, out IntPtr control);
    [PreserveSig] int EnumPartsIncoming(out IntPtr parts);
    [PreserveSig] int EnumPartsOutgoing(out IntPtr parts);
    [PreserveSig] int GetTopologyObject(out IDeviceTopology topology);
    [PreserveSig] int Activate(uint clsCtx, ref Guid iid, out IntPtr interfacePointer);
    [PreserveSig] int RegisterControlChangeCallback(ref Guid iid, IntPtr notify);
    [PreserveSig] int UnregisterControlChangeCallback(IntPtr notify);
}

[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig]
    int KsProperty([In] ref KsProperty property, [In] uint propertyLength,
        IntPtr propertyData, [In] uint dataLength, out uint bytesReturned);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator
{
}