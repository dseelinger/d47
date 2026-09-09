using D47.Core.Capabilities;

namespace D47.Core.Coverage;

/// <summary>The list of things worth knowing you have exercised, derived from the capability registry.</summary>
public static class CoverageInventory
{
    public static IReadOnlyList<CoverageItem> Of(CapabilityRegistry registry) =>
    [
        .. registry.All.SelectMany(Tools),
        .. registry.All.SelectMany(Settings),
    ];

    /// <summary>
    /// The canonical schema is guaranteed byte-identical across runs by the prompt-caching invariant,
    /// which is most of what this wants — but not all of it.
    /// </summary>
    private static IEnumerable<CoverageItem> Tools(RegisteredCapability capability) =>
        capability.Descriptor.Tools.Select(tool => new CoverageItem(
            CoverageKind.Tool,
            tool.Name,
            capability.Descriptor.Id,
            $"{capability.Descriptor.Name} - {tool.Name}",
            CoverageLedger.Fingerprint(string.Join(
                "",
                tool.Name,
                tool.Description,
                capability.ToolSchemas[tool.Name]))));

    private static IEnumerable<CoverageItem> Settings(RegisteredCapability capability) =>
        capability.Descriptor.Settings.Select(row => new CoverageItem(
            CoverageKind.Setting,
            row.Key,
            capability.Descriptor.Id,
            $"{capability.Descriptor.Name} - {row.Label}",
            CoverageLedger.Fingerprint(Definition(row))));

    /// <summary>What makes a settings row the row it is, for fingerprinting.</summary>
    private static string Definition(SettingRow row) =>
        string.Join(
            '',
            row.Key,
            row.Label,
            row.Help,
            row.Kind.ToString(),
            row.Protected ? "protected" : "open",
            row.DefaultDisplay ?? string.Empty,
            string.Join(',', row.Commands.Select(c => c.Phrase)));
}
