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
        FmtId = new Guid("8c7ed206-3f8a-4826-b3b4-f0bca5f89e47"),
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
    public static readonly Guid PropertySet = new("C2F568A2-8BEA-4120-9F78-43881FC10D13");
    public const uint OneshotReconnect = 0;
    public const uint OneshotDisconnect = 1;
    public const uint KsPropertyTypeGet = 1;
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out uint count);
    int GetAt(uint iProp, out PropertyKey pkey);
    int GetValue(ref PropertyKey key, out PropVariant pv);
    int SetValue(ref PropertyKey key, ref PropVariant pv);
    int Commit();
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(DataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);
    int GetDefaultAudioEndpoint(DataFlow dataFlow, uint role, out IMMDevice device);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
}

[ComImport]
[Guid("0BD7A1BE-652A-4D11-A7BD-37CF2F714EB6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    int GetCount(out uint count);
    int Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    int OpenPropertyStore(uint stgmAccess, out IPropertyStore properties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetState(out DeviceState state);
}

[ComImport]
[Guid("62BEA501-4008-4E56-82F8-6093C5FFC668")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDeviceTopology
{
    int GetConnectorCount(out uint count);
    int GetConnector(uint index, out IConnector connector);
    int GetSubunitCount(out uint count);
    int GetSubunit(uint index, out IPart part);
    int GetPartById(uint id, out IPart part);
    int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetSignalPathCount(out uint count);
    int GetSignalPath(uint index, out uint signalPath);
}

[ComImport]
[Guid("9C87313D-CF22-4430-B448-97C72218866A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IConnector
{
    int GetType(out int type);
    int GetDataFlow(out DataFlow flow);
    int ConnectTo(IConnector connectTo);
    int Disconnect();
    int IsConnected(out int connected);
    int GetConnectedTo(out IConnector connectedTo);
    int GetConnectorIdConnectedTo(out uint id);
    int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
}

[ComImport]
[Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDBBDA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPart
{
    int GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    int GetLocalId(out uint id);
    int GetGlobalId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetPartType(out int type);
    int GetSubType(out Guid subType);
    int GetControlInterfaceCount(out uint count);
    int GetControlInterface(uint index, out IntPtr control);
    int RegisterControlChangeCallback(ref Guid eventId, IntPtr callback);
    int UnregisterControlChangeCallback(IntPtr callback);
    int GetPartById(uint id, out IPart part);
    int GetControlChangeRegister(out int register);
    int GetInterfaceByName([MarshalAs(UnmanagedType.LPWStr)] string name, out IntPtr interfacePointer);
    int GetTopologyObject(out IDeviceTopology topology);
    int Activate(uint clsCtx, ref Guid iid, out IntPtr interfacePointer);
    int RegisterControlChangeCallback2(uint flags, IntPtr context);
}

[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    int KsProperty([In] ref KsProperty property, [In] uint propertyLength,
        IntPtr propertyData, [In] uint dataLength, out uint bytesReturned);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator
{
}