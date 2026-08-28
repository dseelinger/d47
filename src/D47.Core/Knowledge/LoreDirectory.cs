using System.Globalization;
using System.Reflection;
using D47.Core.Lore;

namespace D47.Core.Knowledge;

/// <summary>
/// The shipped table of systems that mean something beyond their astrography (Phase 23,
/// "Know which systems carry lore").
/// <para>
/// <b>The table holds the fact, not the prose.</b> <em>This system contains this notable site</em>
/// is a fact about Elite Dangerous in the same category as which system an engineer sits in, and
/// <see cref="EngineerDirectory"/> already ships that way: derived by a generator with its
/// provenance recorded, Frontier attributed in their own words through <c>NOTICE</c>,
/// non-commercial, and keyed on system address rather than name.
/// </para>
/// <para>
/// <b>Where the engineer precedent stops carrying.</b> The set of engineers is closed and defined
/// by the game; <em>which systems are worth remarking on</em> is a judgement somebody made, so
/// copying a community list would copy their compilation even though every row in it is a fact.
/// There is no first-party set to key against instead — <c>CodexEntry</c> fires in 99 of the 913
/// corpus journals and is astronomy rather than lore, and <c>ApproachSettlement</c> names 229
/// settlements while saying nothing about which of them matter. So these twenty rows are the
/// maintainer's own compilation; see <c>tools/gen-lore.py</c>, which is where the sources and the
/// two dropped rows are written down.
/// </para>
/// <para>
/// It is a <b>seed rather than a corpus</b>. What grows it is <see cref="LoreBook"/>, on the
/// Commander's own machine, out of what they actually asked about.
/// </para>
/// </summary>
public static class LoreDirectory
{
    private const string ResourceName = "D47.Core.LoreTable";

    private static readonly Lazy<Dictionary<long, LoreEntry>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Every shipped row, in the table's order — which is alphabetical by name.</summary>
    public static IReadOnlyCollection<LoreEntry> All => Loaded.Value.Values;

    /// <summary>The row for a system, or null for the overwhelming majority of the galaxy.</summary>
    public static LoreEntry? ByAddress(long systemAddress) => Loaded.Value.GetValueOrDefault(systemAddress);

    /// <summary>
    /// The row for a system named rather than jumped into — what a Commander asking about
    /// somewhere reaches. Exact and case-insensitive rather than fuzzy: <see cref="Catalogue"/>'s
    /// matcher exists for spoken names out of a closed vocabulary, and system names are neither.
    /// </summary>
    public static LoreEntry? ByName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : Loaded.Value.Values.FirstOrDefault(
                entry => string.Equals(entry.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static Dictionary<long, LoreEntry> Load()
    {
        using var stream = typeof(LoreDirectory).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return new Dictionary<long, LoreEntry>();
        }

        using var reader = new StreamReader(stream);

        var rows = new Dictionary<long, LoreEntry>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#' || line.StartsWith("systemAddress\t", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells.Length < 3
                || !long.TryParse(cells[0], CultureInfo.InvariantCulture, out var address)
                || cells[1].Length == 0
                || cells[2].Length == 0)
            {
                continue;
            }

            rows[address] = new LoreEntry(address, cells[1], cells[2]) { Tier = LoreTier.Shipped };
        }

        return rows;
    }
}
