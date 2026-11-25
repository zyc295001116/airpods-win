using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AirPodsWinTray.Core.Models;

namespace AirPodsWinTray.Core.Registry;

public sealed class AirPodsModelRegistry
{
    private readonly Dictionary<ushort, AirPodsModelDefinition> _byCode;
    private readonly AirPodsModelDefinition _unknown;

    public IReadOnlyCollection<AirPodsModelDefinition> KnownModels => _byCode.Values.ToList();

    public AirPodsModelRegistry(IEnumerable<AirPodsModelDefinition> models, AirPodsModelDefinition unknown)
    {
        _byCode = models.ToDictionary(m => m.Code);
        _unknown = Clone(unknown, isKnown: false);
    }

    public static AirPodsModelRegistry LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "AirPodsWinTray.Core.Data.airpods-models.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return LoadFromJson(json);
    }

    public static AirPodsModelRegistry LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());

        var file = JsonSerializer.Deserialize<ModelsFile>(json, options)
            ?? throw new InvalidOperationException("Invalid airpods-models.json");

        var models = file.Models.Select(ParseEntry).ToList();
        var unknown = ParseUnknown(file.UnknownDefaults);
        return new AirPodsModelRegistry(models, unknown);
    }

    public AirPodsModelDefinition Resolve(ushort modelCode)
    {
        if (_byCode.TryGetValue(modelCode, out var def))
        {
            return def;
        }

        return Clone(_unknown, code: modelCode, name: $"{_unknown.Name} (0x{modelCode:X4})", isKnown: false);
    }

    public AirPodsModelDefinition Resolve(ProximityBeaconState state) =>
        state.Parsed ? Resolve(state.ModelCode) : _unknown;

    private static AirPodsModelDefinition ParseEntry(ModelEntry entry)
    {
        var code = ParseCode(entry.Code);
        return new AirPodsModelDefinition
        {
            Code = code,
            Name = entry.Name,
            FormFactor = Enum.Parse<FormFactor>(entry.FormFactor, ignoreCase: true),
            CapabilityTier = Enum.Parse<CapabilityTier>(entry.CapabilityTier, ignoreCase: true),
            SupportsAnc = entry.SupportsAnc,
            EarDetectReliability = Enum.Parse<EarDetectReliability>(entry.EarDetectReliability, ignoreCase: true),
            IsKnown = true
        };
    }

    private static AirPodsModelDefinition ParseUnknown(UnknownDefaults entry) =>
        new()
        {
            Name = entry.Name,
            FormFactor = Enum.Parse<FormFactor>(entry.FormFactor, ignoreCase: true),
            CapabilityTier = Enum.Parse<CapabilityTier>(entry.CapabilityTier, ignoreCase: true),
            SupportsAnc = entry.SupportsAnc,
            EarDetectReliability = Enum.Parse<EarDetectReliability>(entry.EarDetectReliability, ignoreCase: true),
            IsKnown = false
        };

    private static AirPodsModelDefinition Clone(
        AirPodsModelDefinition source,
        ushort? code = null,
        string? name = null,
        bool? isKnown = null) =>
        new()
        {
            Code = code ?? source.Code,
            Name = name ?? source.Name,
            FormFactor = source.FormFactor,
            CapabilityTier = source.CapabilityTier,
            SupportsAnc = source.SupportsAnc,
            EarDetectReliability = source.EarDetectReliability,
            IsKnown = isKnown ?? source.IsKnown
        };

    private static ushort ParseCode(string code)
    {
        var trimmed = code.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        return ushort.Parse(trimmed, System.Globalization.NumberStyles.HexNumber);
    }

    private sealed class ModelsFile
    {
        public List<ModelEntry> Models { get; set; } = [];
        public UnknownDefaults UnknownDefaults { get; set; } = new();
    }

    private sealed class ModelEntry
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string FormFactor { get; set; } = "Stereo";
        public string CapabilityTier { get; set; } = "A";
        public bool SupportsAnc { get; set; }
        public string EarDetectReliability { get; set; } = "Normal";
    }

    private sealed class UnknownDefaults
    {
        public string Name { get; set; } = "Unknown AirPods";
        public string FormFactor { get; set; } = "Stereo";
        public string CapabilityTier { get; set; } = "A";
        public bool SupportsAnc { get; set; }
        public string EarDetectReliability { get; set; } = "Normal";
    }
}