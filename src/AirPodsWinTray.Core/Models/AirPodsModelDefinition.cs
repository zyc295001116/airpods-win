namespace AirPodsWinTray.Core.Models;

public sealed class AirPodsModelDefinition
{
    public ushort Code { get; init; }
    public string Name { get; init; } = "Unknown";
    public FormFactor FormFactor { get; init; } = FormFactor.Stereo;
    public CapabilityTier CapabilityTier { get; init; } = CapabilityTier.A;
    public bool SupportsAnc { get; init; }
    public EarDetectReliability EarDetectReliability { get; init; } = EarDetectReliability.Normal;
    public bool IsKnown { get; init; } = true;
}