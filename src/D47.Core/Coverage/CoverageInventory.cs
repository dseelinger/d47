using D47.Core.Capabilities;

namespace D47.Core.Coverage;

/// <summary>The list of things worth knowing you have exercised, derived from the capability registry.</summary>
public static class CoverageInventory
{
    public static IReadOnlyList<CoverageItem> Of(CapabilityRegistry registry)
    {
        var settings = registry.All.SelectMany(Settings).ToList();
        var shared = settings
            .GroupBy(s => s.Item.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. registry.All.SelectMany(Tools),
            .. settings.Select(s => shared.Contains(s.Item.Name) && !string.IsNullOrEmpty(s.Group)
                ? s.Item with { Name = $"{s.Group} - {s.Item.Name}" }
                : s.Item),
        ];
    }

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

    private static IEnumerable<(CoverageItem Item, string? Group)> Settings(RegisteredCapability capability) =>
        capability.Descriptor.Settings.Where(Drivable).Select(row => (
            new CoverageItem(
                CoverageKind.Setting,
                row.Key,
                capability.Descriptor.Id,
                $"{capability.Descriptor.Name} - {row.Label}",
                CoverageLedger.Fingerprint(Definition(row))),
            row.Group));

    /// <summary>False for a row that only shows a value: no control and no button.</summary>
    private static bool Drivable(SettingRow row) =>
        row.Kind != SettingKind.Info || row.Press is not null || row.PressAsync is not null;

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
