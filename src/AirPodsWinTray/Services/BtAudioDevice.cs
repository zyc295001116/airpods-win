namespace AirPodsWinTray.Services;

public sealed class BtAudioDevice
{
    public required string ContainerName { get; init; }
    public required Guid ContainerId { get; init; }
    public bool IsConnected { get; set; }
}