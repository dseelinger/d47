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

    /// <summary>Volcanism, as <c>/api/bodies/field_values/volcanism_type</c> returned it on 2026-10-06.</summary>
    public static IReadOnlyList<string> Volcanism { get; } =
    [
        "Carbon Dioxide Geysers", "Major Carbon Dioxide Geysers", "Major Metallic Magma", "Major Rocky Magma",
        "Major Silicate Vapour Geysers", "Major Water Geysers", "Major Water Magma", "Metallic Magma",
        "Minor Ammonia Magma", "Minor Carbon Dioxide Geysers", "Minor Metallic Magma", "Minor Methane Magma",
        "Minor Nitrogen Magma", "Minor Rocky Magma", "Minor Silicate Vapour Geysers", "Minor Water Geysers",
        "Minor Water Magma", "No volcanism", "Rocky Magma", "Silicate Vapour Geysers", "Water Geysers",
        "Water Magma",
    ];

    /// <summary>Atmospheres, as <c>/api/bodies/field_values/atmosphere</c> returned them on 2026-10-06.</summary>
    public static IReadOnlyList<string> Atmospheres { get; } =
    [
        "Ammonia", "Ammonia and Oxygen", "Ammonia-rich", "Argon", "Argon-rich", "Carbon dioxide",
        "Carbon dioxide-rich", "Helium", "Hot Argon", "Hot Argon-rich", "Hot Carbon dioxide",
        "Hot Carbon dioxide-rich", "Hot Metallic vapour", "Hot Silicate vapour", "Hot Sulphur dioxide",
        "Hot Water", "Hot Water-rich", "Hot thick Ammonia", "Hot thick Ammonia-rich", "Hot thick Argon",
        "Hot thick Argon-rich", "Hot thick Carbon dioxide", "Hot thick Carbon dioxide-rich",
        "Hot thick Metallic vapour", "Hot thick Methane", "Hot thick Methane-rich", "Hot thick Nitrogen",
        "Hot thick Silicate vapour", "Hot thick Sulphur dioxide", "Hot thick Water", "Hot thick Water-rich",
        "Hot thin Carbon dioxide", "Hot thin Metallic vapour", "Hot thin Silicate vapour",
        "Hot thin Sulphur dioxide", "Methane", "Methane-rich", "Neon-rich", "Nitrogen", "No atmosphere",
        "Oxygen", "Suitable for water-based life", "Sulphur dioxide", "Thick Ammonia",
        "Thick Ammonia and Oxygen", "Thick Ammonia-rich", "Thick Argon", "Thick Argon-rich",
        "Thick Carbon dioxide", "Thick Carbon dioxide-rich", "Thick Helium", "Thick Methane",
        "Thick Methane-rich", "Thick Nitrogen", "Thick No atmosphere", "Thick Suitable for water-based life",
        "Thick Sulphur dioxide", "Thick Water", "Thick Water-rich", "Thin Ammonia", "Thin Ammonia and Oxygen",
        "Thin Ammonia-rich", "Thin Argon", "Thin Argon-rich", "Thin Carbon dioxide",
        "Thin Carbon dioxide-rich", "Thin Helium", "Thin Methane", "Thin Methane-rich", "Thin Neon",
        "Thin Neon-rich", "Thin Nitrogen", "Thin Oxygen", "Thin Sulphur dioxide", "Thin Water",
        "Thin Water-rich", "Water", "Water-rich",
    ];

    public static string? MatchSurfaceMaterial(string spoken) => Catalogue.Match(SurfaceMaterials, spoken);

    public static string? MatchVolcanism(string spoken) => Catalogue.Match(Volcanism, spoken);

    public static string? MatchAtmosphere(string spoken) => Catalogue.Match(Atmospheres, spoken);

    public static string? MatchSubtype(string spoken) => Catalogue.Match(Subtypes, spoken);

    public static string? MatchSignal(string spoken) => Catalogue.Match(Signals, spoken);

    public static string? MatchRingSignal(string spoken) => Catalogue.Match(RingSignals, spoken);

    public static string? MatchRingType(string spoken) => Catalogue.Match(RingTypes, spoken);

    public static string? MatchReserveLevel(string spoken) => Catalogue.Match(ReserveLevels, spoken);
}
