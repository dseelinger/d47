namespace D47.Core.Knowledge;

/// <summary>What a body search can be asked about, by name.</summary>
public static class BodyCatalogue
{
    /// <summary>Every body subtype the search knows, stars and planets together.</summary>
    public static IReadOnlyList<string> Subtypes { get; } =
    [
        "A (Blue-White super giant) Star", "A (Blue-White) Star", "Ammonia world",
        "B (Blue-White super giant) Star", "B (Blue-White) Star", "Black Hole", "C Star", "CJ Star",
        "CN Star", "Class I gas giant", "Class II gas giant", "Class III gas giant",
        "Class IV gas giant", "Class V gas giant", "Earth-like world", "F (White super giant) Star",
        "F (White) Star", "G (White-Yellow super giant) Star", "G (White-Yellow) Star",
        "Gas giant with ammonia-based life", "Gas giant with water-based life", "Helium gas giant",
        "Helium-rich gas giant", "Herbig Ae/Be Star", "High metal content world", "Icy body",
        "K (Yellow-Orange giant) Star", "K (Yellow-Orange) Star", "L (Brown dwarf) Star",
        "M (Red dwarf) Star", "M (Red giant) Star", "M (Red super giant) Star", "MS-type Star",
        "Metal-rich body", "Neutron Star", "O (Blue-White) Star", "Rocky Ice world", "Rocky body",
        "S-type Star", "Supermassive Black Hole", "T (Brown dwarf) Star", "T Tauri Star",
        "Water giant", "Water world", "White Dwarf (D) Star", "White Dwarf (DA) Star",
        "White Dwarf (DAB) Star", "White Dwarf (DAV) Star", "White Dwarf (DAZ) Star",
        "White Dwarf (DB) Star", "White Dwarf (DBV) Star", "White Dwarf (DBZ) Star",
        "White Dwarf (DC) Star", "White Dwarf (DCV) Star", "White Dwarf (DQ) Star",
        "Wolf-Rayet C Star", "Wolf-Rayet N Star", "Wolf-Rayet NC Star", "Wolf-Rayet O Star",
        "Wolf-Rayet Star", "Y (Brown dwarf) Star",
    ];

    /// <summary>Signals found on a body's surface.</summary>
    public static IReadOnlyList<string> Signals { get; } =
    [
        "Alexandrite", "Benitoite", "Biological", "Bromellite", "Geological", "Grandidierite",
        "Guardian", "Human", "Low Temperature Diamonds", "Major Anomaly", "Monazite", "Musgravite",
        "Other", "Painite", "Platinum", "Rhodplumsite", "Serendibite", "Thargoid", "Tritium",
        "Void Opal",
    ];

    /// <summary>Hotspot materials in a ring.</summary>
    public static IReadOnlyList<string> RingSignals { get; } =
    [
        "Alexandrite", "Bauxite", "Benitoite", "Bertrandite", "Bromellite", "Cobalt", "Coltan",
        "Gallite", "Grandidierite", "Hydrogen Peroxide", "Indite", "Lepidolite", "Liquid oxygen",
        "Lithium Hydroxide", "Low Temperature Diamonds", "Methane Clathrate",
        "Methanol Monohydrate Crystals", "Monazite", "Musgravite", "Painite", "Platinum",
        "Praseodymium", "Rhodplumsite", "Rutile", "Samarium", "Serendibite", "Tritium", "Uraninite",
        "Void Opal", "Water",
    ];

    /// <summary>Ring compositions.</summary>
    public static IReadOnlyList<string> RingTypes { get; } = ["Icy", "Metal Rich", "Metallic", "Rocky"];

    /// <summary>How rich a ring's reserves are.</summary>
    public static IReadOnlyList<string> ReserveLevels { get; } =
        ["Common", "Depleted", "Low", "Major", "Pristine"];

    /// <summary>The surface materials the body index can be filtered on, in its own spelling.</summary>
    public static IReadOnlyList<string> SurfaceMaterials { get; } =
    [
        "Antimony", "Arsenic", "Cadmium", "Carbon", "Chromium", "Germanium", "Iron", "Manganese",
        "Mercury", "Molybdenum", "Nickel", "Niobium", "Phosphorus", "Polonium", "Ruthenium",
        "Selenium", "Sulphur", "Technetium", "Tellurium", "Tin", "Tungsten", "Vanadium", "Yttrium",
        "Zinc", "Zirconium",
    ];

    public static string? MatchSurfaceMaterial(string spoken) => Catalogue.Match(SurfaceMaterials, spoken);

    public static string? MatchSubtype(string spoken) => Catalogue.Match(Subtypes, spoken);

    public static string? MatchSignal(string spoken) => Catalogue.Match(Signals, spoken);

    public static string? MatchRingSignal(string spoken) => Catalogue.Match(RingSignals, spoken);

    public static string? MatchRingType(string spoken) => Catalogue.Match(RingTypes, spoken);

    public static string? MatchReserveLevel(string spoken) => Catalogue.Match(ReserveLevels, spoken);
}
