namespace D47.Core.Knowledge;

/// <summary>
/// What a body search can be asked about, by name. Read from the search service's own
/// <c>field_values</c> endpoints on 2026-08-14 rather than recalled.
/// <para>
/// <b>Validated locally, never advertised</b>, for the reason
/// <see cref="OutfittingCatalogue"/> gives at length: a tool parameter carrying sixty-one body
/// subtypes as an <c>enum</c> is kilobytes of schema in prompt position 1, where one byte's
/// change invalidates the entire cached prefix (architecture.md §6), paid for on every turn to
/// answer a question asked once a session. The parameter is free text, the name is matched here,
/// and a miss comes back with the near ones offered.
/// </para>
/// <para>
/// The signal lists are short enough that advertising them would be affordable, and they are
/// still matched locally — one rule rather than a threshold nobody can defend the exact value
/// of. "Low Temperature Diamond" and "low temperature diamonds" both land on the catalogue name
/// through <see cref="Catalogue.Match"/>, which is what the enum would have bought.
/// </para>
/// </summary>
public static class BodyCatalogue
{
    /// <summary>
    /// Every body subtype the search knows, stars and planets together. Sixty-one of them, which
    /// is why they are matched rather than listed to the model.
    /// </summary>
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

    /// <summary>
    /// Signals found on a body's surface. The two that matter for a Commander asking are
    /// <c>Biological</c> and <c>Geological</c>; the rest are what a surface deposit or a
    /// settlement reads as.
    /// </summary>
    public static IReadOnlyList<string> Signals { get; } =
    [
        "Alexandrite", "Benitoite", "Biological", "Bromellite", "Geological", "Grandidierite",
        "Guardian", "Human", "Low Temperature Diamonds", "Major Anomaly", "Monazite", "Musgravite",
        "Other", "Painite", "Platinum", "Rhodplumsite", "Serendibite", "Thargoid", "Tritium",
        "Void Opal",
    ];

    /// <summary>
    /// Hotspot materials in a ring. A longer list than the body signals and a different one — a
    /// Commander asking where to mine Painite means this, and asking where to find biology means
    /// the other. Keeping them apart is what stops "nearest Bertrandite" being answered from the
    /// wrong index and coming back empty.
    /// </summary>
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

    /// <summary>
    /// How rich a ring's reserves are. The multiplier on everything mined out of it, so a
    /// Pristine ring twenty light years further away is often the shorter trip in the end.
    /// </summary>
    public static IReadOnlyList<string> ReserveLevels { get; } =
        ["Common", "Depleted", "Low", "Major", "Pristine"];

    /// <summary>
    /// The surface materials the body index can be filtered on, in its own spelling.
    /// <para>
    /// <b>Twenty-five, and there are twenty-eight raw materials in the game.</b> Rhenium, Lead and
    /// Boron are simply not in this index, so a search for one comes back empty — and an empty
    /// search result reads as "there is none near you", which is a wrong answer wearing the shape
    /// of a right one. They are declined by name instead. Read from the service's own
    /// <c>field_values</c> on 2026-08-15 rather than recalled.
    /// </para>
    /// </summary>
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
