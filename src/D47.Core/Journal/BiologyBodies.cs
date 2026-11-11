using System.Collections.Immutable;

namespace D47.Core.Journal;

/// <summary>A body Elite has counted biological signals on, and what its own scan said about it.</summary>
public sealed record BiologyBody(long SystemAddress, int BodyId, string BodyName)
{
    public int BiologicalCount { get; init; }

    /// <summary>The genera a surface scan named, as Elite localised them.</summary>
    public IReadOnlyList<string> Genera { get; init; } = [];

    /// <summary>True once a surface scan (<c>SAASignalsFound</c>) has been folded for this body.</summary>
    public bool Mapped { get; init; }

    public string? PlanetClass { get; init; }

    public string? Atmosphere { get; init; }

    public string? Volcanism { get; init; }

    /// <summary>m/s², as the journal writes it.</summary>
    public double? SurfaceGravity { get; init; }

    /// <summary>Kelvin.</summary>
    public double? SurfaceTemperature { get; init; }

    /// <summary>Pascals.</summary>
    public double? SurfacePressure { get; init; }

    public DateTimeOffset SeenAt { get; init; }

    internal bool HasConditions =>
        PlanetClass is not null || Atmosphere is not null || Volcanism is not null
        || SurfaceGravity is not null || SurfaceTemperature is not null || SurfacePressure is not null;

    internal BiologyBody WithConditionsOf(BiologyBody other) => this with
    {
        PlanetClass = other.PlanetClass,
        Atmosphere = other.Atmosphere,
        Volcanism = other.Volcanism,
        SurfaceGravity = other.SurfaceGravity,
        SurfaceTemperature = other.SurfaceTemperature,
        SurfacePressure = other.SurfacePressure,
    };
}

/// <summary>Every body the journals show biological signals on, across game sessions.</summary>
public sealed record BiologyBodies
{
    public static readonly BiologyBodies None = new();

    /// <summary>The bodies, most recently seen first.</summary>
    public IReadOnlyList<BiologyBody> All => [.. Held.Values.OrderByDescending(body => body.SeenAt)];

    /// <summary>True once the walk over earlier journals has been merged in.</summary>
    public bool IsWhole { get; init; }

    private ImmutableDictionary<(long SystemAddress, int BodyId), BiologyBody> Held { get; init; } =
        ImmutableDictionary<(long SystemAddress, int BodyId), BiologyBody>.Empty;

    private bool Folded { get; init; }

    /// <summary>The system whose scanned conditions are held.</summary>
    private long? HeldSystem { get; init; }

    /// <summary>Conditions from <c>Scan</c> events in <see cref="HeldSystem"/>, by body id.</summary>
    private ImmutableDictionary<int, BiologyBody> Scanned { get; init; } = ImmutableDictionary<int, BiologyBody>.Empty;

    public IReadOnlyList<BiologyBody> InSystem(long systemAddress) =>
        [.. Held.Values.Where(body => body.SystemAddress == systemAddress).OrderBy(body => body.BodyId)];

    public BiologyBody? For(long systemAddress, int bodyId) => Held.GetValueOrDefault((systemAddress, bodyId));

    internal bool SameBodies(BiologyBodies other) => ReferenceEquals(Held, other.Held);

    public BiologyBodies Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind == "NewCommander")
        {
            return None with { IsWhole = IsWhole };
        }

        var state = Folded ? this : this with { Folded = true };

        return journalEvent.Kind switch
        {
            "FSDJump" or "CarrierJump" or "Location" when journalEvent.Long("SystemAddress") is { } address => state.EnteredSystem(address),
            "Scan" => state.Scan(journalEvent),
            "FSSBodySignals" => state.Signals(journalEvent, mapped: false),
            "SAASignalsFound" => state.Signals(journalEvent, mapped: true),
            _ => state,
        };
    }

    /// <summary>
    /// This, recovered from older journals, merged with what the live journal has folded, marked whole. For a body
    /// in both, a surface scan beats an FSS row and otherwise the later row wins.
    /// </summary>
    public BiologyBodies With(BiologyBodies other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var bodies = Held;

        foreach (var (key, body) in other.Held)
        {
            if (!bodies.TryGetValue(key, out var held))
            {
                bodies = bodies.SetItem(key, body);
                continue;
            }

            var otherWins = body.Mapped != held.Mapped ? body.Mapped : body.SeenAt >= held.SeenAt;
            var (winner, loser) = otherWins ? (body, held) : (held, body);

            if (!winner.HasConditions && loser.HasConditions)
            {
                winner = winner.WithConditionsOf(loser);
            }

            bodies = bodies.SetItem(key, winner);
        }

        var tracking = other.Folded ? other : this;

        return tracking with { Held = bodies, IsWhole = true };
    }

    private BiologyBodies EnteredSystem(long address) =>
        HeldSystem == address ? this : this with { HeldSystem = address, Scanned = ImmutableDictionary<int, BiologyBody>.Empty };

    private BiologyBodies Scan(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SystemAddress") is not { } address
            || journalEvent.Int("BodyID") is not { } bodyId
            || journalEvent.String("BodyName") is not { } bodyName)
        {
            return this;
        }

        var conditions = new BiologyBody(address, bodyId, bodyName)
        {
            PlanetClass = journalEvent.String("PlanetClass"),
            Atmosphere = journalEvent.String("Atmosphere"),
            Volcanism = journalEvent.String("Volcanism"),
            SurfaceGravity = journalEvent.Double("SurfaceGravity"),
            SurfaceTemperature = journalEvent.Double("SurfaceTemperature"),
            SurfacePressure = journalEvent.Double("SurfacePressure"),
        };

        var state = EnteredSystem(address);
        state = state with { Scanned = state.Scanned.SetItem(bodyId, conditions) };

        return state.Held.TryGetValue((address, bodyId), out var kept)
            ? state with { Held = state.Held.SetItem((address, bodyId), kept.WithConditionsOf(conditions)) }
            : state;
    }

    private BiologyBodies Signals(JournalEvent journalEvent, bool mapped)
    {
        if (journalEvent.Long("SystemAddress") is not { } address
            || journalEvent.Int("BodyID") is not { } bodyId
            || journalEvent.String("BodyName") is not { } bodyName)
        {
            return this;
        }

        var key = (address, bodyId);
        Held.TryGetValue(key, out var kept);

        if (!mapped && kept is { Mapped: true })
        {
            return this;
        }

        var count = journalEvent
            .Items("Signals")
            .Where(row => row.Spoken("Type") is { } type && type.Contains("Biological", StringComparison.OrdinalIgnoreCase))
            .Sum(row => row.Int("Count") ?? 0);

        IReadOnlyList<string> genera = [.. journalEvent.Items("Genuses").Select(row => row.Named("Genus")).OfType<string>()];

        if (count == 0 && genera.Count == 0)
        {
            return this;
        }

        var body = new BiologyBody(address, bodyId, bodyName)
        {
            BiologicalCount = count,
            Genera = genera,
            Mapped = mapped,
            SeenAt = journalEvent.Timestamp,
        };

        if (HeldSystem == address && Scanned.TryGetValue(bodyId, out var scanned))
        {
            body = body.WithConditionsOf(scanned);
        }
        else if (kept is not null)
        {
            body = body.WithConditionsOf(kept);
        }

        return this with { Held = Held.SetItem(key, body) };
    }
}
