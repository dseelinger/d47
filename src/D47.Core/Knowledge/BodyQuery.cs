namespace D47.Core.Knowledge;

/// <summary>One ring around a body, cut down to what decides where to mine.</summary>
public sealed record RingSummary(string Name, string? Type)
{
    /// <summary>The hotspots in it, as material and how many overlap there.</summary>
    public IReadOnlyList<(string Material, int Count)> Hotspots { get; init; } = [];

    /// <summary>
    /// When the hotspots were last reported. Crowd-sourced like outfitting stock, so it carries
    /// the same warning: a hotspot list from two years ago is a claim about a ring nobody has
    /// scanned since.
    /// </summary>
    public DateTimeOffset? SignalsSeen { get; init; }
}

/// <summary>
/// One body, cut down to what can be said out loud.
/// <para>
/// The same projection <see cref="SystemSummary"/> is, and necessary for the same reason: a
/// single body record carries its atmosphere composition, its solid composition, its orbital
/// elements and every ring in full, none of which answers "where is the nearest one".
/// </para>
/// </summary>
public sealed record BodySummary
{
    public required string Name { get; init; }

    public required string SystemName { get; init; }

    /// <summary>What the journal writes as <c>BodyID</c> on a scan or a touchdown. Null where the service did not say (list.md Phase 47).</summary>
    public int? BodyId { get; init; }

    /// <summary>The system's id64, for the same reason.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>Light years from the query's reference system.</summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Light seconds from the system's entry point. The half of "how far" a Commander feels: a
    /// body four light years away and a quarter of a million light seconds in-system is the worse
    /// trip. Read from the result rather than filtered on — the service ignores a
    /// <c>distance_to_arrival</c> filter, measured on 2026-08-14 as an unchanged 1,315 results
    /// with and without it — so it is reported and never used to narrow.
    /// </summary>
    public double? DistanceToArrival { get; init; }

    public string? Subtype { get; init; }

    public bool IsLandable { get; init; }

    public string? TerraformingState { get; init; }

    /// <summary>How rich this body's rings are, when it has any.</summary>
    public string? ReserveLevel { get; init; }

    /// <summary>Surface signals, as kind and how many.</summary>
    public IReadOnlyList<(string Kind, int Count)> Signals { get; init; } = [];

    public IReadOnlyList<RingSummary> Rings { get; init; } = [];

    /// <summary>Surface materials and their percentage share, as every result carries them.</summary>
    public IReadOnlyList<(string Name, double Share)> Materials { get; init; } = [];

    /// <summary>What mapping it is worth, as the service estimates it.</summary>
    public long? MappingValue { get; init; }
}

public sealed record BodySearchResult(string? Reference, int Total, IReadOnlyList<BodySummary> Bodies);

/// <summary>
/// A validated body search. Like <see cref="GalaxyQuery"/> and <see cref="StationQuery"/>,
/// existing is the evidence that it is valid.
/// <para>
/// <b>The wire shapes here were measured, and two of them silently lie.</b> Signals are a
/// <em>group</em> filter whose members are <c>name</c> and <c>count</c> — the obvious spelling,
/// <c>{"signals":{"Biological":{"min":"1","max":"40"}}}</c>, is accepted and ignored, returning
/// the same 1,315 bodies as no filter at all. And <c>count</c> is a bare number matching
/// <em>exactly</em> that many rather than a range: asking for 1 returned 41 bodies, 2 returned
/// 14, 3 returned none and 4 returned 2, and each result carried exactly the number asked for.
/// That is why <see cref="SignalCount"/> is documented as exact rather than as a minimum — a
/// "three or more" that quietly meant "exactly three" would be a wrong answer that reads right.
/// </para>
/// </summary>
public sealed record BodyQuery
{
    private BodyQuery()
    {
    }

    public string? ReferenceSystem { get; init; }

    /// <summary>Maximum light years from the reference. Always bounded — the galaxy is not a search.</summary>
    public double MaxDistance { get; init; } = 50;

    /// <summary>The catalogue name of the body kind wanted, if that is the question.</summary>
    public string? Subtype { get; init; }

    /// <summary>A surface signal that must be present.</summary>
    public string? Signal { get; init; }

    /// <summary>Exactly how many of <see cref="Signal"/>. Exact, not a minimum — see the type remarks.</summary>
    public int? SignalCount { get; init; }

    /// <summary>A hotspot material that must be in one of the body's rings.</summary>
    public string? RingSignal { get; init; }

    /// <summary>Exactly how many overlapping hotspots of <see cref="RingSignal"/>.</summary>
    public int? RingSignalCount { get; init; }

    /// <summary>
    /// A surface material to insist on, for the raw half of engineering sourcing.
    /// <para>
    /// <b>Presence is filterable and share is not.</b> The index answers "which bodies have any
    /// Yttrium" and refuses to rank them — <c>percentage</c>, <c>value</c> and <c>count</c> beside
    /// the name are all silently ignored, and a sort on the material is dropped. So the filter is
    /// remote and the ranking is local, over what came back, and the answer says so
    /// (docs/spikes/engineering-data-sources.md §7).
    /// </para>
    /// </summary>
    public string? Material { get; init; }

    /// <summary>
    /// Named systems to confine the search to, as the index spells them.
    /// <para>
    /// <b>Matched exactly, and case-sensitively</b> — "sol" returns nothing where "Sol" returns
    /// Sol's forty bodies, measured 2026-08-16. So these are only ever names the index itself
    /// handed back, never a name somebody said out loud. The obvious spelling of the same idea,
    /// a zero-width distance range around the system, is the worst request in this file: it is
    /// not "this system only", it is every body in the galaxy, and it took 120 seconds before
    /// timing out rather than failing.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SystemNames { get; init; } = [];

    public string? RingType { get; init; }

    public string? ReserveLevel { get; init; }

    /// <summary>Null means "either", which is not the same as false.</summary>
    public bool? Landable { get; init; }

    /// <summary>Null means "either". True is the terraforming candidate a Commander is hunting.</summary>
    public bool? Terraformable { get; init; }

    public int Size { get; init; } = 5;

    /// <summary>Whether this asks about rings at all, which decides how the answer reads.</summary>
    public bool IsAboutRings => RingSignal is not null || RingType is not null || ReserveLevel is not null;

    public static bool TryParse(
        string? referenceSystem,
        string? subtype,
        string? signal,
        int? signalCount,
        string? ringSignal,
        int? ringSignalCount,
        string? ringType,
        string? reserveLevel,
        bool? landable,
        bool? terraformable,
        double? maxDistance,
        int size,
        out BodyQuery query,
        out string failure)
    {
        query = new BodyQuery();
        failure = string.Empty;

        if (!TryMatch("body type", subtype, BodyCatalogue.Subtypes, BodyCatalogue.MatchSubtype, out var matchedSubtype, out failure)
            || !TryMatch("surface signal", signal, BodyCatalogue.Signals, BodyCatalogue.MatchSignal, out var matchedSignal, out failure)
            || !TryMatch("hotspot material", ringSignal, BodyCatalogue.RingSignals, BodyCatalogue.MatchRingSignal, out var matchedRingSignal, out failure)
            || !TryMatch("ring type", ringType, BodyCatalogue.RingTypes, BodyCatalogue.MatchRingType, out var matchedRingType, out failure)
            || !TryMatch("reserve level", reserveLevel, BodyCatalogue.ReserveLevels, BodyCatalogue.MatchReserveLevel, out var matchedReserve, out failure))
        {
            return false;
        }

        // A count without the thing it counts is not a filter the service can express: its
        // `count` member only means anything beside a `name`, and sending it alone returned zero
        // results rather than being ignored. Refused here so the Commander is told, rather than
        // being told nothing in the galaxy matches.
        if (signalCount is not null && matchedSignal is null)
        {
            failure = "Say which surface signal to count — for example biological or geological.";
            return false;
        }

        if (ringSignalCount is not null && matchedRingSignal is null)
        {
            failure = "Say which hotspot material to count — for example Painite or Low Temperature Diamonds.";
            return false;
        }

        if (matchedSubtype is null
            && matchedSignal is null
            && matchedRingSignal is null
            && matchedRingType is null
            && matchedReserve is null
            && landable is null
            && terraformable is null)
        {
            failure =
                "That search has no filters, so it would match every body in the galaxy. Narrow it with a "
                + "body type, a surface signal, a hotspot material, a ring type, or whether it is landable.";
            return false;
        }

        query = new BodyQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            Subtype = matchedSubtype,
            Signal = matchedSignal,
            SignalCount = Bounded(signalCount),
            RingSignal = matchedRingSignal,
            RingSignalCount = Bounded(ringSignalCount),
            RingType = matchedRingType,
            ReserveLevel = matchedReserve,
            Landable = landable,
            Terraformable = terraformable,

            // Wider than the station search allows, because a body search is often the one a
            // Commander makes from deep space where the nearest anything is a long way off, and
            // still bounded because unbounded is the galaxy.
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 1000),
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };

        return true;
    }

    /// <summary>
    /// A search for bodies carrying a surface material, which is the raw half of engineering
    /// sourcing and a different question from the one <see cref="TryParse"/> serves.
    /// <para>
    /// Landable only, because a material on a body nobody can land on is not somewhere to go.
    /// </para>
    /// </summary>
    public static BodyQuery ForMaterial(string? referenceSystem, string material, double? maxDistance, int size) =>
        new()
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            Material = material,
            Landable = true,
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 1000),
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };

    /// <summary>
    /// Every body in a handful of named systems, for the second half of a colonisation candidate
    /// search — landability and rings, which the systems search's embedded bodies do not carry.
    /// <para>
    /// Unfiltered otherwise, and that is the point: the shortlist is already small, and the
    /// question being asked of it is "what is in there", not "which of these matches". The page is
    /// the service's maximum, since a rich system runs to forty bodies and five of them would
    /// overrun any smaller number without saying so.
    /// </para>
    /// </summary>
    /// <summary>Every body in one named system, for resolving a body a story names to its id (list.md Phase 47).</summary>
    public static BodyQuery Within(string system) => ForSystems(system, [system.Trim()], 1);

    public static BodyQuery ForSystems(string referenceSystem, IReadOnlyList<string> names, double maxDistance) =>
        new()
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            SystemNames = names,
            MaxDistance = Math.Clamp(maxDistance, 1, 1000),
            Size = 500,
        };

    /// <summary>
    /// Counts the service will answer at all. Its own <c>field_values</c> reported no signal
    /// above 63 of anything, so a number outside this is a question with no possible answer and
    /// clamping it is kinder than a search that returns nothing for a reason nobody can see.
    /// </summary>
    private static int? Bounded(int? count) => count is null ? null : Math.Clamp(count.Value, 1, 63);

    private static bool TryMatch(
        string kind,
        string? spoken,
        IReadOnlyList<string> catalogue,
        Func<string, string?> match,
        out string? matched,
        out string failure)
    {
        matched = null;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(spoken))
        {
            return true;
        }

        matched = match(spoken);

        if (matched is not null)
        {
            return true;
        }

        failure = Catalogue.Unknown(kind, spoken.Trim(), Catalogue.Near(catalogue, spoken));
        return false;
    }
}
