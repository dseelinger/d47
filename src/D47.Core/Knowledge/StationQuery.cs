namespace D47.Core.Knowledge;

/// <summary>
/// One station, cut down to what answers "where do I buy this".
/// </summary>
public sealed record StationSummary
{
    public required string Name { get; init; }

    public required string SystemName { get; init; }

    /// <summary>What the journal writes as <c>MarketID</c> on docking. Null where the service did not say (list.md Phase 47).</summary>
    public long? MarketId { get; init; }

    /// <summary>The system's id64, for the same reason.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>Light years from the reference system.</summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Light seconds from the system's entry point. The second half of "how far", and the half a
    /// Commander feels: a station 4 light years away and 300,000 light seconds in-system is a
    /// worse trip than one twice as far with a pad at the star.
    /// </summary>
    public double? DistanceToArrival { get; init; }

    public string? Type { get; init; }

    public bool HasLargePad { get; init; }

    /// <summary>Raw, Manufactured or Encoded where the station has a material trader.
    /// <para>
    /// Null is a real state and not a parse failure: one station in fifty carries the service and
    /// no type, which matches the one genuinely unclassifiable trader in two hundred that the
    /// economy rule could not place either.
    /// </para>
    /// </summary>
    public string? TraderType { get; init; }

    /// <summary>
    /// When the service last saw this station's stock. Outfitting data comes from Commanders
    /// sharing it, so it is not "what is there" but "what somebody last reported was there" —
    /// and a report from two years ago should not be read out as though it were current.
    /// </summary>
    public DateTimeOffset? StockLastSeen { get; init; }
}

public sealed record StationSearchResult(
    string? Reference,
    int Total,
    IReadOnlyList<StationSummary> Stations);

/// <summary>
/// A validated search for somewhere to buy something. Like <see cref="GalaxyQuery"/>, existing is
/// the evidence that it is valid.
/// </summary>
public sealed record StationQuery
{
    private StationQuery()
    {
    }

    public string? ReferenceSystem { get; init; }

    /// <summary>Maximum light years from the reference. Always set — an unbounded station search is the galaxy.</summary>
    public double MaxDistance { get; init; } = 50;

    /// <summary>The catalogue name of a module to be sold there, if that is the question.</summary>
    public string? Module { get; init; }

    public string? ModuleClass { get; init; }

    public string? ModuleRating { get; init; }

    /// <summary>The catalogue name of a ship to be sold there, if that is the question.</summary>
    public string? Ship { get; init; }

    /// <summary>Whether to insist on a large landing pad.</summary>
    public bool LargePadOnly { get; init; }

    /// <summary>
    /// What kind of material trader, where that is the question — Raw, Manufactured or Encoded.
    /// <para>
    /// <b>The index answers this directly</b>, which retires an economy heuristic that was never
    /// going to behave (docs/spikes/engineering-data-sources.md §8). Every station carries a
    /// <c>material_trader</c> field, and filtering on it returns exactly the stations that also
    /// carry the service — 209 either way within 150 light years of Sol, measured 2026-08-15.
    /// </para>
    /// </summary>
    public string? TraderType { get; init; }

    public int Size { get; init; } = 5;

    /// <summary>
    /// Builds a station search, or says what was wrong in words the model can act on.
    /// <para>
    /// The names are matched against <see cref="OutfittingCatalogue"/> rather than passed
    /// through. The service does honour a name it does not know — by returning nothing — so this
    /// is not the silent-ignore problem the system filters have; it is the quieter one of
    /// answering "nowhere sells that" about a module that does not exist under that name.
    /// </para>
    /// </summary>
    public static bool TryParse(
        string? referenceSystem,
        string? module,
        string? moduleClass,
        string? moduleRating,
        string? ship,
        bool largePadOnly,
        double? maxDistance,
        int size,
        out StationQuery query,
        out string failure)
    {
        query = new StationQuery();
        failure = string.Empty;

        string? matchedModule = null;
        string? matchedShip = null;

        if (!string.IsNullOrWhiteSpace(module))
        {
            matchedModule = OutfittingCatalogue.MatchModule(module);

            if (matchedModule is null)
            {
                failure = Unknown("module", module, OutfittingCatalogue.Near(OutfittingCatalogue.Modules, module));
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(ship))
        {
            matchedShip = OutfittingCatalogue.MatchShip(ship);

            if (matchedShip is null)
            {
                failure = Unknown("ship", ship, OutfittingCatalogue.Near(OutfittingCatalogue.Ships, ship));
                return false;
            }
        }

        if (matchedModule is null && matchedShip is null)
        {
            failure = "Say what to look for — a module or a ship.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(moduleClass)
            && !OutfittingCatalogue.Classes.Contains(moduleClass.Trim()))
        {
            failure = $"'{moduleClass}' is not a module size. They run 0 to 8.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(moduleRating)
            && !OutfittingCatalogue.Ratings.Contains(moduleRating.Trim().ToUpperInvariant()))
        {
            failure = $"'{moduleRating}' is not a module rating. They run A to I.";
            return false;
        }

        query = new StationQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            Module = matchedModule,
            ModuleClass = string.IsNullOrWhiteSpace(moduleClass) ? null : moduleClass.Trim(),
            ModuleRating = string.IsNullOrWhiteSpace(moduleRating) ? null : moduleRating.Trim().ToUpperInvariant(),
            Ship = matchedShip,
            LargePadOnly = largePadOnly,

            // Bounded on both ends. Unbounded is the whole galaxy, and a Commander asking for the
            // nearest anything does not want a result 40,000 light years away.
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 500),
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };

        return true;
    }

    /// <summary>
    /// Every station in one named system, for resolving a station a story names to its market id
    /// (list.md Phase 47). Not through <see cref="TryParse"/>, which insists on something being
    /// sold: the question here is what is there, not who sells what. A light year is the narrowest
    /// radius the index honours, so a neighbour inside it can appear and the caller matches on name.
    /// </summary>
    public static StationQuery Within(string system) => new()
    {
        ReferenceSystem = system.Trim(),
        MaxDistance = 1,
        Size = 20,
    };

    /// <summary>
    /// The stations nearest a system, for proposing real places a story may dock at (list.md
    /// Phase 47). Nothing sold, no pad — what is within reach, nearest first.
    /// </summary>
    public static StationQuery Near(string referenceSystem, double maxDistance, int size) => new()
    {
        ReferenceSystem = referenceSystem.Trim(),
        MaxDistance = Math.Clamp(maxDistance, 1, 500),
        Size = Math.Clamp(size, 1, 20),
    };

    /// <summary>The three kinds of material trader, in the index's own spelling.</summary>
    public static IReadOnlyList<string> TraderTypes { get; } = ["Raw", "Manufactured", "Encoded"];

    /// <summary>
    /// A search for material traders, which asks a different question from the module and ship
    /// search above and so does not go through it — <see cref="TryParse"/> insists on something
    /// being sold, and a trader sells nothing.
    /// <para>
    /// No economy rule and no security or population bound, because the index knows the answer
    /// outright. The published location rule — Refinery and Extraction for Raw, Extraction and
    /// Industrial for Manufactured, High Tech and Military for Encoded, medium-or-high security,
    /// a population between one and twenty-two million — describes where traders <em>tend</em> to
    /// be, and it was the best available while the type had to be inferred. It no longer does:
    /// asking for the type returns exactly the stations that carry the service. Two of those
    /// bounds could not have been applied here anyway, since the station index silently ignores
    /// <c>population</c> and <c>security</c> (measured 2026-08-15: a population of 0 to 1 returns
    /// the same count as no filter, identically to a bogus key).
    /// </para>
    /// </summary>
    public static StationQuery ForTrader(string? referenceSystem, string? traderType, double? maxDistance, int size)
    {
        var matched = traderType is null
            ? null
            : TraderTypes.FirstOrDefault(type => string.Equals(type, traderType.Trim(), StringComparison.OrdinalIgnoreCase));

        return new StationQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            TraderType = matched,
            IsAboutTraders = true,
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 500),
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };
    }

    /// <summary>Whether this search is about traders rather than about something being sold.</summary>
    public bool IsAboutTraders { get; init; }

    private static string Unknown(string kind, string spoken, IReadOnlyList<string> near) =>
        near.Count > 0
            ? $"I don't know a {kind} called '{spoken}'. Did you mean {string.Join(", ", near)}?"
            : $"I don't know a {kind} called '{spoken}'.";
}
