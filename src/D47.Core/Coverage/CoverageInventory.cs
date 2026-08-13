using D47.Core.Capabilities;

namespace D47.Core.Coverage;

/// <summary>
/// The list of things worth knowing you have exercised, derived from the capability registry.
/// <para>
/// The registry is already the app's own account of its major parts — it is what the docs gate
/// checks, what the prompt is built from and what the settings surface renders. Deriving the
/// inventory from it rather than maintaining a second list means a capability added in a later
/// phase shows up as "never exercised" the day it is written, with nobody having to remember.
/// </para>
/// <para>
/// Tools and settings rows, not capabilities: a capability is exercised by using one of its
/// tools or changing one of its rows, so counting it separately would be counting the same act
/// twice. Corner cases are deliberately out of scope — this answers "have I been here at all".
/// </para>
/// </summary>
public static class CoverageInventory
{
    public static IReadOnlyList<CoverageItem> Of(CapabilityRegistry registry) =>
    [
        .. registry.All.SelectMany(Tools),
        .. registry.All.SelectMany(Settings),
    ];

    /// <summary>
    /// The canonical schema is guaranteed byte-identical across runs by the prompt-caching
    /// invariant, which is most of what this wants — but not all of it. That writer emits only
    /// the parameters, so <em>every zero-argument tool serialises identically</em>: eleven of
    /// d47's seventeen tools hash the same, and rewording one of their descriptions would change
    /// nothing. The name and description go in alongside it, so a tool that reads differently to
    /// the model reads as worth trying again here. (The same blind spot is why a green
    /// documentation gate does not mean a page mentions every tool.)
    /// </summary>
    private static IEnumerable<CoverageItem> Tools(RegisteredCapability capability) =>
        capability.Descriptor.Tools.Select(tool => new CoverageItem(
            CoverageKind.Tool,
            tool.Name,
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
            $"{capability.Descriptor.Name} - {row.Label}",
            CoverageLedger.Fingerprint(Definition(row))));

    /// <summary>
    /// What makes a settings row the row it is, for fingerprinting. The wording is in here on
    /// purpose: a row whose help text changed is a row worth looking at again, because the help
    /// text is most of what a Commander judges it by.
    /// </summary>
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
