using System.Collections.Frozen;
using System.Text.RegularExpressions;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// d47's star and planet kinds from <c>CodexBodies.tsv</c>: the kind a Spansh body or a journal scan belongs to, and the
/// kind tree. The rules are those of <c>kind_of</c> in <c>tools/gen-codex-bodies.py</c>, which writes the table; both
/// are checked against <c>tests/fixtures/codex/kinds.tsv</c>, and the scan's fields against
/// <c>tests/fixtures/codex/journal-kinds.tsv</c>.
/// </summary>
public static partial class CodexKinds
{
    public const string NoAtmosphere = "No atmosphere";

    private static readonly string[] RingOrder = ["Icy", "Rocky", "Metal Rich", "Metallic"];

    private static readonly string[] Unbanded = ["Neutron Star", "Black Hole", "Supermassive Black Hole"];

    /// <summary>The Spansh subType of each journal <c>StarType</c> other than a white dwarf's D code.</summary>
    internal static readonly FrozenDictionary<string, string> StarTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["O"] = "O (Blue-White) Star",
        ["B"] = "B (Blue-White) Star",
        ["A"] = "A (Blue-White) Star",
        ["F"] = "F (White) Star",
        ["G"] = "G (White-Yellow) Star",
        ["K"] = "K (Yellow-Orange) Star",
        ["M"] = "M (Red dwarf) Star",
        ["K_OrangeGiant"] = "K (Yellow-Orange giant) Star",
        ["M_RedGiant"] = "M (Red giant) Star",
        ["M_RedSuperGiant"] = "M (Red super giant) Star",
        ["A_BlueWhiteSuperGiant"] = "A (Blue-White super giant) Star",
        ["B_BlueWhiteSuperGiant"] = "B (Blue-White super giant) Star",
        ["F_WhiteSuperGiant"] = "F (White super giant) Star",
        ["G_WhiteSuperGiant"] = "G (White-Yellow super giant) Star",
        ["L"] = "L (Brown dwarf) Star",
        ["T"] = "T (Brown dwarf) Star",
        ["Y"] = "Y (Brown dwarf) Star",
        ["TTS"] = "T Tauri Star",
        ["AeBe"] = "Herbig Ae/Be Star",
        ["W"] = "Wolf-Rayet Star",
        ["WN"] = "Wolf-Rayet N Star",
        ["WNC"] = "Wolf-Rayet NC Star",
        ["WC"] = "Wolf-Rayet C Star",
        ["WO"] = "Wolf-Rayet O Star",
        ["C"] = "C Star",
        ["CN"] = "CN Star",
        ["CJ"] = "CJ Star",
        ["MS"] = "MS-type Star",
        ["S"] = "S-type Star",
        ["N"] = "Neutron Star",
        ["H"] = "Black Hole",
        ["SupermassiveBlackHole"] = "Supermassive Black Hole",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The Spansh subType of each journal <c>PlanetClass</c>.</summary>
    internal static readonly FrozenDictionary<string, string> PlanetClasses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Metal rich body"] = "Metal-rich body",
        ["High metal content body"] = "High metal content world",
        ["Rocky body"] = "Rocky body",
        ["Icy body"] = "Icy body",
        ["Rocky ice body"] = "Rocky Ice world",
        ["Earthlike body"] = "Earth-like world",
        ["Water world"] = "Water world",
        ["Ammonia world"] = "Ammonia world",
        ["Water giant"] = "Water giant",
        ["Gas giant with water based life"] = "Gas giant with water-based life",
        ["Gas giant with ammonia based life"] = "Gas giant with ammonia-based life",
        ["Sudarsky class I gas giant"] = "Class I gas giant",
        ["Sudarsky class II gas giant"] = "Class II gas giant",
        ["Sudarsky class III gas giant"] = "Class III gas giant",
        ["Sudarsky class IV gas giant"] = "Class IV gas giant",
        ["Sudarsky class V gas giant"] = "Class V gas giant",
        ["Helium rich gas giant"] = "Helium-rich gas giant",
        ["Helium gas giant"] = "Helium gas giant",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The gas in Spansh's atmosphere name for each journal <c>AtmosphereType</c>.</summary>
    internal static readonly FrozenDictionary<string, string> AtmosphereTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Ammonia"] = "Ammonia",
        ["AmmoniaRich"] = "Ammonia-rich",
        ["AmmoniaOxygen"] = "Ammonia and Oxygen",
        ["Argon"] = "Argon",
        ["ArgonRich"] = "Argon-rich",
        ["CarbonDioxide"] = "Carbon dioxide",
        ["CarbonDioxideRich"] = "Carbon dioxide-rich",
        ["EarthLike"] = "Suitable for water-based life",
        ["Helium"] = "Helium",
        ["MetallicVapour"] = "Metallic vapour",
        ["Methane"] = "Methane",
        ["MethaneRich"] = "Methane-rich",
        ["Neon"] = "Neon",
        ["NeonRich"] = "Neon-rich",
        ["Nitrogen"] = "Nitrogen",
        ["Oxygen"] = "Oxygen",
        ["SilicateVapour"] = "Silicate vapour",
        ["SulphurDioxide"] = "Sulphur dioxide",
        ["Water"] = "Water",
        ["WaterRich"] = "Water-rich",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Spansh's ring type for each journal <c>RingClass</c>.</summary>
    internal static readonly FrozenDictionary<string, string> RingClasses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["eRingClass_Icy"] = "Icy",
        ["eRingClass_Rocky"] = "Rocky",
        ["eRingClass_MetalRich"] = "Metal Rich",
        ["eRingClass_Metalic"] = "Metallic",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly string[] DensityWords = ["hot", "thin", "thick"];

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

    /// <summary>The kind key of a star or planet in a journal <c>Scan</c>, in the shipped table's tree; null for a body with no kind.</summary>
    public static string? OfScan(JournalEvent scan) => OfScan(scan, CodexCatalogue.Table);

    /// <summary>
    /// The kind key of a star or planet in a journal <c>Scan</c>: its fields in Spansh's words, classified as
    /// <see cref="OfSpansh(BodyProfile, CodexTable)"/> does. Null for a scan with neither <c>StarType</c> nor
    /// <c>PlanetClass</c>, a star type with no kind, a planet with no <c>TerraformState</c>, or a value not mapped here.
    /// </summary>
    public static string? OfScan(JournalEvent scan, CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(table);

        var name = scan.String("BodyName") ?? string.Empty;

        if (scan.String("StarType") is { Length: > 0 } starType)
        {
            return SubTypeOfStar(starType) is { } star
                ? OfSpansh(new BodyProfile { BodyId = 0, Name = name, Type = "Star", SubType = star, Luminosity = scan.String("Luminosity") }, table)
                : null;
        }

        if (scan.String("PlanetClass") is not { Length: > 0 } planetClass
            || !PlanetClasses.TryGetValue(planetClass, out var subType)
            || scan.String("TerraformState") is not { } terraformState)
        {
            return null;
        }

        if (IsGasGiant(subType))
        {
            var rings = new List<RingProfile>();

            foreach (var ring in scan.Items("Rings"))
            {
                if (ring.String("RingClass") is not { } ringClass || !RingClasses.TryGetValue(ringClass, out var type))
                {
                    return null;
                }

                rings.Add(new RingProfile(ring.String("Name") ?? string.Empty, type, new Dictionary<string, int>()));
            }

            return OfSpansh(new BodyProfile { BodyId = 0, Name = name, Type = "Planet", SubType = subType, Rings = rings }, table);
        }

        if (AtmosphereOf(scan.String("Atmosphere"), scan.String("AtmosphereType")) is not { } atmosphere)
        {
            return null;
        }

        var planet = new BodyProfile
        {
            BodyId = 0,
            Name = name,
            Type = "Planet",
            SubType = subType,
            TerraformingState = terraformState.Length == 0 ? "Not terraformable" : terraformState,
            Atmosphere = atmosphere,
        };

        return OfSpansh(planet, table);
    }

    /// <summary>
    /// The Spansh atmosphere of a journal <c>Atmosphere</c> text and <c>AtmosphereType</c>: the text's density words, the
    /// first capitalised, then the type's gas. "No atmosphere" for type None or a text with no gas word; null for a gas
    /// whose type is missing or not mapped here.
    /// </summary>
    internal static string? AtmosphereOf(string? text, string? type)
    {
        var words = (text ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (type == "None" || words.All(word => word == "atmosphere" || DensityWords.Contains(word)))
        {
            return NoAtmosphere;
        }

        if (type is null || !AtmosphereTypes.TryGetValue(type, out var gas))
        {
            return null;
        }

        var density = string.Join(' ', words.Where(DensityWords.Contains));

        return density.Length == 0 ? gas : char.ToUpperInvariant(density[0]) + density[1..] + " " + gas;
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

    private static string? SubTypeOfStar(string starType) =>
        starType[0] == 'D' && starType.All(char.IsAsciiLetterUpper)
            ? "White Dwarf (" + starType + ") Star"
            : StarTypes.GetValueOrDefault(starType);

    private static bool IsUnbanded(string subType) =>
        subType.StartsWith("White Dwarf (", StringComparison.Ordinal) || Unbanded.Contains(subType, StringComparer.Ordinal);

    private static bool IsGasGiant(string subType) =>
        subType.Contains("gas giant", StringComparison.OrdinalIgnoreCase) || subType.Contains("water giant", StringComparison.OrdinalIgnoreCase);

    /// <summary>The field lowercased, each run of other characters made <c>_</c>, with none at either end.</summary>
    internal static string Slug(string field) => OtherCharacters().Replace(field.ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex OtherCharacters();
}
