using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>
/// d47's star and planet kinds from <c>CodexBodies.tsv</c>: the kind a Spansh body belongs to, and the kind tree. The
/// rules are those of <c>kind_of</c> in <c>tools/gen-codex-bodies.py</c>, which writes the table; both are checked
/// against <c>tests/fixtures/codex/kinds.tsv</c>.
/// </summary>
public static partial class CodexKinds
{
    public const string NoAtmosphere = "No atmosphere";

    private static readonly string[] RingOrder = ["Icy", "Rocky", "Metal Rich", "Metallic"];

    private static readonly string[] Unbanded = ["Neutron Star", "Black Hole", "Supermassive Black Hole"];

    /// <summary>The kind key of a Spansh star or planet in the shipped table's tree; null for a body with no kind.</summary>
    public static string? OfSpansh(BodyProfile body) => OfSpansh(body, CodexCatalogue.Table);

    /// <summary>
    /// The kind key of a Spansh star or planet; null for a body with no subType, or a star of a banded subType with no
    /// luminosity whose subType row in <paramref name="table"/> has no usual band.
    /// </summary>
    public static string? OfSpansh(BodyProfile body, CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(table);

        if (body.SubType is not { Length: > 0 } subType)
        {
            return null;
        }

        if (body.Type == "Star")
        {
            var star = "star." + Slug(subType);

            if (IsUnbanded(subType))
            {
                return star;
            }

            return (Band(body.Luminosity) ?? table.Entry(star)?.Kind?.UsualBand) is { } band ? star + "." + Slug(band) : null;
        }

        if (body.Type != "Planet")
        {
            return null;
        }

        if (IsGasGiant(subType))
        {
            var types = body.Rings.Select(ring => ring.Type).ToHashSet(StringComparer.Ordinal);
            var rings = RingOrder.Where(types.Contains).Select(Slug).ToList();

            return "gas." + Slug(subType) + "." + (rings.Count == 0 ? "no_rings" : string.Join('_', rings));
        }

        var terraformable = body.TerraformingState is not null && body.TerraformingState != "Not terraformable";
        var atmosphere = body.Atmosphere is { Length: > 0 } written ? written : NoAtmosphere;
        var level = "planet." + Slug(subType) + (terraformable ? ".terraformable" : ".not_terraformable");

        return atmosphere == NoAtmosphere ? level + ".no_atmosphere" : level + ".atmosphere." + Slug(atmosphere);
    }

    /// <summary>
    /// The luminosity band of a Spansh luminosity: hypergiant for 0, O or Ia0…, supergiant for I and II, giant for III,
    /// dwarf for IV, V and VI, each with any letters after it, white dwarf for VII; null for none or another value.
    /// </summary>
    public static string? Band(string? luminosity)
    {
        if (luminosity is null)
        {
            return null;
        }

        if (luminosity is "0" or "O" || luminosity.StartsWith("Ia0", StringComparison.Ordinal))
        {
            return "hypergiant";
        }

        var numeral = new string([.. luminosity.TakeWhile(character => character is 'I' or 'V')]);

        return numeral switch
        {
            "I" or "II" => "supergiant",
            "III" => "giant",
            "IV" or "V" or "VI" => "dwarf",
            "VII" => "white dwarf",
            _ => null,
        };
    }

    /// <summary>The key of the row <paramref name="key"/> names as its parent; null at the top of a family or for a key with no row.</summary>
    public static string? Parent(CodexTable table, string key)
    {
        ArgumentNullException.ThrowIfNull(table);

        return table.Entry(key)?.Kind?.ParentKey;
    }

    /// <summary>The row of <paramref name="key"/>, then each parent's, up to the top of its family; empty for a key that is not a kind.</summary>
    public static IReadOnlyList<CodexEntry> Lineage(CodexTable table, string key)
    {
        ArgumentNullException.ThrowIfNull(table);

        var rows = new List<CodexEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var at = key; at is not null && seen.Add(at) && table.Entry(at) is { Kind: { } kind } entry; at = kind.ParentKey)
        {
            rows.Add(entry);
        }

        return rows;
    }

    /// <summary>The family placeholder picture of a kind or group; null for a key that is not a kind.</summary>
    public static string? FamilyFile(CodexTable table, string key)
    {
        ArgumentNullException.ThrowIfNull(table);

        return table.Entry(key) is { Kind: not null } entry ? CodexFamilies.FileOfTableName(entry.SubCategory) : null;
    }

    private static bool IsUnbanded(string subType) =>
        subType.StartsWith("White Dwarf (", StringComparison.Ordinal) || Unbanded.Contains(subType, StringComparer.Ordinal);

    private static bool IsGasGiant(string subType) =>
        subType.Contains("gas giant", StringComparison.OrdinalIgnoreCase) || subType.Contains("water giant", StringComparison.OrdinalIgnoreCase);

    /// <summary>The field lowercased, each run of other characters made <c>_</c>, with none at either end.</summary>
    private static string Slug(string field) => OtherCharacters().Replace(field.ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex OtherCharacters();
}
