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

    /// <summary>The percentage share of <paramref name="material"/>, or null when the body carries none.</summary>
    public double? Share(string material) =>
        Materials.Where(one => string.Equals(one.Name, material, StringComparison.OrdinalIgnoreCase))
            .Select(one => (double?)one.Share)
            .FirstOrDefault();
}

public sealed record BodySearchResult(string? Reference, int Total, IReadOnlyList<BodySummary> Bodies);

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

    /// <summary>"distance" or "material"; null is distance.</summary>
    public string? OrderBy { get; init; }

    /// <summary>Vocabulary filters by name, validated against <see cref="GalaxySearchKind.Bodies"/>.</summary>
    public IReadOnlyDictionary<string, string> Filters { get; init; } = new Dictionary<string, string>();

    public double? MaxDistance { get; init; }

    public int Size { get; init; } = 5;
}

/// <summary>A validated body search.</summary>
public sealed record BodyQuery
{
    private BodyQuery()
    {
    }

    public string? ReferenceSystem { get; init; }

    /// <summary>Maximum light years from the reference.</summary>
    public double MaxDistance { get; init; } = 50;

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

    /// <summary>How many bodies the request asks for.</summary>
    public int Size { get; init; } = 5;

    /// <summary>How many bodies the answer keeps.</summary>
    public int Limit { get; init; } = 5;

    /// <summary>How many of the nearest bodies an order by material chooses from.</summary>
    public const int RichestOf = 50;

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

        var orderBy = string.IsNullOrWhiteSpace(request.OrderBy) ? "distance" : request.OrderBy.Trim();
        var orderByMaterial = string.Equals(orderBy, "material", StringComparison.OrdinalIgnoreCase);

        if (!orderByMaterial && !string.Equals(orderBy, "distance", StringComparison.OrdinalIgnoreCase))
        {
            failure = $"Bodies can be ordered by distance or by material, not by '{orderBy}'.";
            return false;
        }

        if (orderByMaterial && matchedMaterial is null)
        {
            failure = "Say which surface material to order by — for example Polonium or Arsenic.";
            return false;
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
            && criteria.Count == 0)
        {
            failure =
                "That search has no filters, so it would match every body in the galaxy. Narrow it with a "
                + "body type, a surface signal, a hotspot material, a ring type, a surface material, volcanism, "
                + "an atmosphere, or whether it is landable.";
            return false;
        }

        var limit = Math.Clamp(request.Size <= 0 ? 5 : request.Size, 1, 20);

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
            Criteria = criteria,
            OrderByMaterial = orderByMaterial,

            // Wider than the station search allows, because a body search is often the one a Commander makes
            // from deep space where the nearest anything is a long way off, and still bounded because
            // unbounded is the galaxy.
            MaxDistance = Math.Clamp(request.MaxDistance is > 0 ? request.MaxDistance.Value : 50, 1, 1000),
            Size = orderByMaterial ? RichestOf : limit,
            Limit = limit,
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
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 1000),
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
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
            MaxDistance = Math.Clamp(maxDistance, 1, 1000),
            Size = Math.Clamp(size, 1, 20),
        };

    public static BodyQuery ForSystems(string referenceSystem, IReadOnlyList<string> names, double maxDistance) =>
        new()
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            SystemNames = names,
            MaxDistance = Math.Clamp(maxDistance, 1, 1000),
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
