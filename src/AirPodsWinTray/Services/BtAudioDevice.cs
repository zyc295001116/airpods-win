namespace AirPodsWinTray.Services;

public sealed class BtAudioDevice
{
    public required string ContainerName { get; init; }
    public required Guid ContainerId { get; init; }
    public bool IsConnected { get; set; }
    public IReadOnlyList<string> EndpointIds { get; init; } = [];
    public IReadOnlyList<string> KsDeviceIds { get; init; } = [];
}