using System.Runtime.InteropServices;

namespace AirPodsWinTray.Services;

/// <summary>
/// Sets default multimedia playback device (A2DP endpoint).
/// </summary>
public static class DefaultAudioDeviceService
{
    public static bool SetDefaultPlaybackDevice(string endpointId)
    {
        try
        {
            var policyConfig = (IPolicyConfig)new PolicyConfig();
            return policyConfig.SetDefaultEndpoint(endpointId, ERole.Multimedia) >= 0;
        }
        catch
        {
            return false;
        }
    }

    public static string? FindBestPlaybackEndpointId(string containerNameContains)
    {
        var policyConfig = (IPolicyConfig)new PolicyConfig();
        // Enumerate via MMDevice - delegated to BtAudioService caller with property store names.
        // Simple fallback: not implemented without full enumeration here.
        return null;
    }

    private enum ERole
    {
        Multimedia = 0,
        Console = 1
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfig;

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int Unused1();
        int Unused2();
        int Unused3();
        int Unused4();
        int Unused5();
        int Unused6();
        int Unused7();
        int Unused8();
        int Unused9();
        int Unused10();
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    }
}