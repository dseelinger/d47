using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One ring around a body, cut down to what decides where to mine.</summary>
public sealed record RingSummary(string Name, string? Type)
{
    /// <summary>The hotspots in it, as material and how many overlap there.</summary>
    public IReadOnlyList<(string Material, int Count)> Hotspots { get; init; } = [];

    /// <summary>When the hotspots were last reported.</summary>
    public DateTimeOffset? SignalsSeen { get; init; }
}

/// <summary>One body, cut down to what can be said out loud.</summary>
public sealed record BodySummary
{
    public required string Name { get; init; }

    public required string SystemName { get; init; }

    /// <summary>What the journal writes as <c>BodyID</c> on a scan or a touchdown.</summary>
    public int? BodyId { get; init; }

    /// <summary>The system's id64, for the same reason.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>Light years from the query's reference system.</summary>
    public double? Distance { get; init; }

    /// <summary>The system's position; null where the search did not give all three coordinates.</summary>
    public StarPosition? Position { get; init; }

    /// <summary>Light seconds from the system's entry point.</summary>
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

    public string? Volcanism { get; init; }

    public string? Atmosphere { get; init; }

    /// <summary>Surface gravity in g.</summary>
    public double? Gravity { get; init; }

    /// <summary>Surface temperature in kelvin.</summary>
    public double? SurfaceTemperature { get; init; }

    public bool IsTidallyLocked { get; init; }

    /// <summary>The body's id64.</summary>
    public long? BodyAddress { get; init; }

    /// <summary>"Star" or "Planet".</summary>
    public string? Type { get; init; }

    public bool IsMainStar { get; init; }

    /// <summary>The whole <c>parents</c> chain, nearest first; kind is Star, Planet or Null as written.</summary>
    public IReadOnlyList<BodyParent> ParentBodies { get; init; } = [];

    /// <summary>A star's class and subclass, "K7".</summary>
    public string? SpectralClass { get; init; }

    /// <summary>A star's luminosity class, "V".</summary>
    public string? Luminosity { get; init; }

    public double? SolarMasses { get; init; }

    public double? SolarRadius { get; init; }

    public double? EarthMasses { get; init; }

    /// <summary>In kilometres.</summary>
    public double? Radius { get; init; }

    /// <summary>A star's age in millions of years.</summary>
    public double? Age { get; init; }

    /// <summary>In atmospheres.</summary>
    public double? SurfacePressure { get; init; }

    /// <summary>Atmosphere gases by percentage share.</summary>
    public IReadOnlyDictionary<string, double> AtmosphereComposition { get; init; } = new Dictionary<string, double>();

    /// <summary>Rock, metal and ice by percentage share.</summary>
    public IReadOnlyDictionary<string, double> SolidComposition { get; init; } = new Dictionary<string, double>();

    /// <summary>Biological genera reported on the body.</summary>
    public IReadOnlyList<string> Genera { get; init; } = [];

    /// <summary>In days.</summary>
    public double? OrbitalPeriod { get; init; }

    /// <summary>In astronomical units.</summary>
    public double? SemiMajorAxis { get; init; }

    public double? OrbitalEccentricity { get; init; }

    /// <summary>In degrees.</summary>
    public double? OrbitalInclination { get; init; }

    public double? OrbitalSynchronicity { get; init; }

    /// <summary>In degrees.</summary>
    public double? ArgOfPeriapsis { get; init; }

    /// <summary>In degrees.</summary>
    public double? AscendingNode { get; init; }

    /// <summary>In degrees.</summary>
    public double? MeanAnomaly { get; init; }

    /// <summary>In days.</summary>
    public double? RotationalPeriod { get; init; }

    /// <summary>In degrees.</summary>
    public double? AxialTilt { get; init; }

    /// <summary>What scanning it is worth, as the service estimates it.</summary>
    public long? ScanValue { get; init; }

    public int? SignalCount { get; init; }

    /// <summary>When the surface signals were last reported.</summary>
    public DateTimeOffset? SignalsSeen { get; init; }

    public long? LandmarkValue { get; init; }

    public IReadOnlyList<BodyLandmark> Landmarks { get; init; } = [];

    public IReadOnlyList<SynthesisRecipe> SynthesisRecipes { get; init; } = [];

    public IReadOnlyList<BeltProfile> Belts { get; init; } = [];

    /// <summary>The stations on or around the body, with the body and system named from this row.</summary>
    public IReadOnlyList<StationSummary> Stations { get; init; } = [];

    /// <summary>The Power controlling the body's system, in the service's spelling.</summary>
    public string? ControllingPower { get; init; }

    /// <summary>The system's Powerplay state, in the service's spelling.</summary>
    public string? PowerState { get; init; }

    public string? SystemRegion { get; init; }

    /// <summary>When the service last had a report of this body.</summary>
    public DateTimeOffset? ReportedAt { get; init; }

    /// <summary>The percentage share of <paramref name="material"/>, or null when the body carries none.</summary>
    public double? Share(string material) =>
        Materials.Where(one => string.Equals(one.Name, material, StringComparison.OrdinalIgnoreCase))
            .Select(one => (double?)one.Share)
            .FirstOrDefault();
}

/// <summary>One body in a body's <c>parents</c> chain.</summary>
/// <param name="Kind">Star, Planet or Null (a barycentre), as written.</param>
/// <param name="Address">The parent's id64.</param>
public sealed record BodyParent(string Kind, long Address, string? Subtype);

/// <summary>One surface feature of a body, with its position.</summary>
public sealed record BodyLandmark(long? Id, string? Type, string? Subtype, string? Variant, double? Latitude, double? Longitude, long? Value);

/// <summary>One material synthesis recipe a body's surface materials allow.</summary>
public sealed record SynthesisRecipe(string Name, string? Level);

public sealed record BodySearchResult(string? Reference, int Total, IReadOnlyList<BodySummary> Bodies)
{
    /// <summary>Whether the service has rows past the ones this answer used.</summary>
    public bool More { get; init; }
}

/// <summary>A body search as asked, before validation. A filter left null is not applied.</summary>
public sealed record BodyRequest
{
    public string? ReferenceSystem { get; init; }

    public string? Subtype { get; init; }

    public string? Signal { get; init; }

    public int? SignalCount { get; init; }

    public string? RingSignal { get; init; }

    public int? RingSignalCount { get; init; }

    public string? RingType { get; init; }

    public string? ReserveLevel { get; init; }

    public bool? Landable { get; init; }

    public bool? Terraformable { get; init; }

    public string? Volcanism { get; init; }

    public string? Atmosphere { get; init; }

    public bool? TidallyLocked { get; init; }

    /// <summary>A range in g, in <see cref="GalaxyCriteria.TryParseRange"/>'s syntax.</summary>
    public string? Gravity { get; init; }

    /// <summary>A range in kelvin, in <see cref="GalaxyCriteria.TryParseRange"/>'s syntax.</summary>
    public string? Temperature { get; init; }

    /// <summary>Light seconds from the system's entry point.</summary>
    public double? MaxArrivalDistance { get; init; }

    public string? Material { get; init; }

    /// <summary>A codex entry by name; a colour variant is searched as its species.</summary>
    public string? CodexEntry { get; init; }

    /// <summary>A codex region by name; see <see cref="GalacticRegions.Names"/>.</summary>
    public string? Region { get; init; }

    /// <summary>A name from <see cref="SearchSorts.For"/>; null is distance.</summary>
    public string? OrderBy { get; init; }

    /// <summary><c>asc</c> or <c>desc</c>; null is the field's first direction.</summary>
    public string? Order { get; init; }

    /// <summary>Vocabulary filters by name, validated against <see cref="GalaxySearchKind.Bodies"/>.</summary>
    public IReadOnlyDictionary<string, string> Filters { get; init; } = new Dictionary<string, string>();

    public double? MaxDistance { get; init; }

    public int Size { get; init; } = 5;

    /// <summary>Which page of <see cref="Size"/> results, from 1.</summary>
    public int Page { get; init; } = 1;
}

/// <summary>A validated body search.</summary>
public sealed record BodyQuery
{
    private BodyQuery()
    {
    }

    public string? ReferenceSystem { get; init; }

    /// <summary>Centres the search on a point instead of <see cref="ReferenceSystem"/>.</summary>
    public StarPosition? ReferencePosition { get; init; }

    /// <summary>Maximum light years from the reference; unused where <see cref="NoDistanceLimit"/>.</summary>
    public double MaxDistance { get; init; } = 50;

    /// <summary>True for a codex entry search that gave no distance: the nearest anywhere, with no radius.</summary>
    public bool NoDistanceLimit { get; init; }

    /// <summary>The map's spelling of the codex region to search in.</summary>
    public string? Region { get; init; }

    /// <summary>The catalogue name of the body kind wanted, if that is the question.</summary>
    public string? Subtype { get; init; }

    /// <summary>A surface signal that must be present.</summary>
    public string? Signal { get; init; }

    /// <summary>Exactly how many of <see cref="Signal"/>.</summary>
    public int? SignalCount { get; init; }

    /// <summary>A hotspot material that must be in one of the body's rings.</summary>
    public string? RingSignal { get; init; }

    /// <summary>Exactly how many overlapping hotspots of <see cref="RingSignal"/>.</summary>
    public int? RingSignalCount { get; init; }

    /// <summary>A surface material to insist on, for the raw half of engineering sourcing.</summary>
    public string? Material { get; init; }

    /// <summary>The species of the codex entry whose landmark the body must carry.</summary>
    public string? CodexEntry { get; init; }

    /// <summary>Named systems to confine the search to, as the index spells them.</summary>
    public IReadOnlyList<string> SystemNames { get; init; } = [];

    public string? RingType { get; init; }

    public string? ReserveLevel { get; init; }

    /// <summary>True or null; the service reads any value of this filter as true.</summary>
    public bool? Landable { get; init; }

    /// <summary>Null means "either".</summary>
    public bool? Terraformable { get; init; }

    public string? Volcanism { get; init; }

    public string? Atmosphere { get; init; }

    /// <summary>True or null, as for <see cref="Landable"/>.</summary>
    public bool? TidallyLocked { get; init; }

    public double? GravityMin { get; init; }

    public double? GravityMax { get; init; }

    public double? TemperatureMin { get; init; }

    public double? TemperatureMax { get; init; }

    /// <summary>Light seconds from the system's entry point.</summary>
    public double? MaxArrivalDistance { get; init; }

    /// <summary>Vocabulary filters, sent under their <see cref="GalaxySearchKind.Bodies"/> keys.</summary>
    public IReadOnlyList<GalaxyCriterion> Criteria { get; init; } = [];

    /// <summary>
    /// Keep the richest in <see cref="Material"/> rather than the nearest. The service cannot sort by share, so
    /// the request asks for the <see cref="RichestOf"/> nearest and <see cref="Keep"/> chooses among them.
    /// </summary>
    public bool OrderByMaterial { get; init; }

    /// <summary>What the results are sorted by; null is nearest first, which an order by material also is.</summary>
    public SearchSort? Sort { get; init; }

    /// <summary>How many bodies the request asks for.</summary>
    public int Size { get; init; } = 5;

    /// <summary>How many bodies the answer keeps.</summary>
    public int Limit { get; init; } = 5;

    /// <summary>Which page of <see cref="Size"/> results, from 1. An order by material has only page 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>How many of the nearest bodies an order by material chooses from.</summary>
    public const int RichestOf = 50;

    /// <summary>The furthest, in light years, a body search goes.</summary>
    public const double MaxDistanceCeiling = 1000;

    /// <summary>Whether this asks about rings at all, which decides how the answer reads.</summary>
    public bool IsAboutRings => RingSignal is not null || RingType is not null || ReserveLevel is not null;

    /// <summary>The bodies the answer gives: as returned, or the <see cref="Limit"/> richest in the material.</summary>
    public BodySearchResult Keep(BodySearchResult result)
    {
        if (!OrderByMaterial || Material is not { } material)
        {
            return result;
        }

        return result with
        {
            Bodies = [.. result.Bodies.OrderByDescending(body => body.Share(material) ?? 0).Take(Limit)],
            More = false,
        };
    }

    public static bool TryParse(BodyRequest request, out BodyQuery query, out string failure)
    {
        query = new BodyQuery();
        failure = string.Empty;
        var landable = request.Landable is true ? true : (bool?)null;
        var tidallyLocked = request.TidallyLocked is true ? true : (bool?)null;

        if (!TryMatch("body type", request.Subtype, BodyCatalogue.Subtypes, BodyCatalogue.MatchSubtype, out var matchedSubtype, out failure)
            || !TryMatch("surface signal", request.Signal, BodyCatalogue.Signals, BodyCatalogue.MatchSignal, out var matchedSignal, out failure)
            || !TryMatch("hotspot material", request.RingSignal, BodyCatalogue.RingSignals, BodyCatalogue.MatchRingSignal, out var matchedRingSignal, out failure)
            || !TryMatch("ring type", request.RingType, BodyCatalogue.RingTypes, BodyCatalogue.MatchRingType, out var matchedRingType, out failure)
            || !TryMatch("reserve level", request.ReserveLevel, BodyCatalogue.ReserveLevels, BodyCatalogue.MatchReserveLevel, out var matchedReserve, out failure)
            || !TryMatch("volcanism", request.Volcanism, BodyCatalogue.Volcanism, BodyCatalogue.MatchVolcanism, out var matchedVolcanism, out failure)
            || !TryMatch("atmosphere", request.Atmosphere, BodyCatalogue.Atmospheres, BodyCatalogue.MatchAtmosphere, out var matchedAtmosphere, out failure)
            || !TryMatch("surface material", request.Material, BodyCatalogue.SurfaceMaterials, BodyCatalogue.MatchSurfaceMaterial, out var matchedMaterial, out failure)
            || !TryRange("gravity", request.Gravity, out var gravityMin, out var gravityMax, out failure)
            || !TryRange("temperature", request.Temperature, out var temperatureMin, out var temperatureMax, out failure)
            || !GalaxyCriteria.TryParse(GalaxySearchKind.Bodies, request.Filters, out var criteria, out failure))
        {
            return false;
        }

        // A count without the thing it counts is not a filter the service can express: its `count` member
        // only means anything beside a `name`, and sending it alone returned zero results rather than being
        // ignored.
        if (request.SignalCount is not null && matchedSignal is null)
        {
            failure = "Say which surface signal to count — for example biological or geological.";
            return false;
        }

        if (request.RingSignalCount is not null && matchedRingSignal is null)
        {
            failure = "Say which hotspot material to count — for example Painite or Low Temperature Diamonds.";
            return false;
        }

        if (!SearchSorts.TryParse(GalaxySearchKind.Bodies, request.OrderBy, request.Order, out var sort, out failure))
        {
            return false;
        }

        var orderByMaterial = sort?.Field.Name == SearchSorts.Material;

        if (orderByMaterial && matchedMaterial is null)
        {
            failure = "Say which surface material to order by — for example Polonium or Arsenic.";
            return false;
        }

        var page = GalaxyQuery.PageOf(request.Page);

        if (orderByMaterial && page > 1)
        {
            failure =
                $"An order by material is one answer, chosen from the nearest {RichestOf} bodies, so it has no page {page}.";
            return false;
        }

        var codexEntry = CodexSpecies.Of(request.CodexEntry);
        string? region = null;

        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            region = GalacticRegions.Match(request.Region);

            if (region is null)
            {
                failure = $"'{request.Region.Trim()}' is not one of the 42 codex regions.";
                return false;
            }
        }

        var arrival = request.MaxArrivalDistance is > 0 ? request.MaxArrivalDistance : null;

        if (matchedSubtype is null
            && matchedSignal is null
            && matchedRingSignal is null
            && matchedRingType is null
            && matchedReserve is null
            && landable is null
            && request.Terraformable is null
            && matchedVolcanism is null
            && matchedAtmosphere is null
            && tidallyLocked is null
            && gravityMin is null && gravityMax is null
            && temperatureMin is null && temperatureMax is null
            && arrival is null
            && matchedMaterial is null
            && codexEntry is null
            && criteria.Count == 0)
        {
            failure =
                "That search has no filters, so it would match every body in the galaxy. Narrow it with a "
                + "body type, a surface signal, a hotspot material, a ring type, a surface material, volcanism, "
                + "an atmosphere, a codex entry, or whether it is landable.";
            return false;
        }

        var limit = Math.Clamp(request.Size <= 0 ? GalaxyQuery.DefaultSize : request.Size, 1, GalaxyQuery.MaxSize);

        query = new BodyQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(request.ReferenceSystem) ? null : request.ReferenceSystem.Trim(),
            Subtype = matchedSubtype,
            Signal = matchedSignal,
            SignalCount = Bounded(request.SignalCount),
            RingSignal = matchedRingSignal,
            RingSignalCount = Bounded(request.RingSignalCount),
            RingType = matchedRingType,
            ReserveLevel = matchedReserve,
            Landable = landable,
            Terraformable = request.Terraformable,
            Volcanism = matchedVolcanism,
            Atmosphere = matchedAtmosphere,
            TidallyLocked = tidallyLocked,
            GravityMin = gravityMin,
            GravityMax = gravityMax,
            TemperatureMin = temperatureMin,
            TemperatureMax = temperatureMax,
            MaxArrivalDistance = arrival,
            Material = matchedMaterial,
            CodexEntry = codexEntry,
            Region = region,
            NoDistanceLimit = codexEntry is not null && request.MaxDistance is not > 0,
            Criteria = criteria,
            OrderByMaterial = orderByMaterial,
            Sort = orderByMaterial ? null : sort,

            // Wider than the station search allows, because a body search is often the one a Commander makes
            // from deep space where the nearest anything is a long way off, and still bounded because
            // unbounded is the galaxy.
            MaxDistance = Math.Clamp(request.MaxDistance is > 0 ? request.MaxDistance.Value : 50, 1, MaxDistanceCeiling),
            Size = orderByMaterial ? RichestOf : limit,
            Limit = limit,
            Page = page,
        };

        return true;
    }

    private static bool TryRange(string name, string? spoken, out double? min, out double? max, out string failure)
    {
        min = null;
        max = null;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(spoken) || GalaxyCriteria.TryParseRange(spoken, out min, out max))
        {
            return true;
        }

        failure =
            $"I couldn't read '{spoken}' as a range for {name}. "
            + "Give it as a number, or as two numbers separated by a dash.";
        return false;
    }

    /// <summary>
    /// A search for bodies carrying a surface material, which is the raw half of engineering sourcing
    /// and a different question from the one <see cref="TryParse"/> serves.
    /// </summary>
    public static BodyQuery ForMaterial(string? referenceSystem, string material, double? maxDistance, int size) =>
        new()
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            Material = material,
            Landable = true,
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, MaxDistanceCeiling),
            Size = Math.Clamp(size <= 0 ? GalaxyQuery.DefaultSize : size, 1, GalaxyQuery.MaxSize),
        };

    /// <summary>
    /// Every body in a handful of named systems, for the second half of a colonisation candidate search
    /// — landability and rings, which the systems search's embedded bodies do not carry.
    /// </summary>
    public static BodyQuery Within(string system) => ForSystems(system, [system.Trim()], 1);

    /// <summary>
    /// The landable bodies nearest a system, for proposing real places a story may land on or scan
    /// (Phase 47).
    /// </summary>
    public static BodyQuery LandableNear(string referenceSystem, double maxDistance, int size) =>
        new()
        {
            ReferenceSystem = referenceSystem.Trim(),
            Landable = true,
            MaxDistance = Math.Clamp(maxDistance, 1, MaxDistanceCeiling),
            Size = Math.Clamp(size, 1, 20),
        };

    /// <summary>The landable bodies nearest a point, for proposing real places a story may land on or scan.</summary>
    public static BodyQuery LandableNear(StarPosition referencePosition, double maxDistance, int size) =>
        new()
        {
            ReferencePosition = referencePosition,
            Landable = true,
            MaxDistance = Math.Clamp(maxDistance, 1, MaxDistanceCeiling),
            Size = Math.Clamp(size, 1, 20),
        };

    public static BodyQuery ForSystems(string referenceSystem, IReadOnlyList<string> names, double maxDistance) =>
        new()
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            SystemNames = names,
            MaxDistance = Math.Clamp(maxDistance, 1, MaxDistanceCeiling),
            Size = 500,
        };

    /// <summary>Counts the service will answer at all.</summary>
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
