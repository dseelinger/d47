using System.Globalization;

namespace D47.Core.Journal;

/// <summary>How far a delivery has got, from <c>CargoDepot</c>.</summary>
public sealed record MissionCargo(int Collected, int Delivered, int Total);

/// <summary>One live mission. Everything but the id and name comes from its <c>MissionAccepted</c>.</summary>
/// <param name="Id">The journal's <c>MissionID</c>.</param>
/// <param name="Name">The internal name as written, such as <c>Mission_Courier</c>.</param>
public sealed record Mission(long Id, string Name)
{
    public string? LocalisedName { get; init; }

    public string? Faction { get; init; }

    /// <summary>The faction the mission is against, where it names one.</summary>
    public string? TargetFaction { get; init; }

    public string? DestinationSystem { get; init; }

    /// <summary>The station, or the settlement for an on-foot mission.</summary>
    public string? DestinationStation { get; init; }

    /// <summary>The commodity's folded symbol, as <see cref="JournalJson.Symbol(string?)"/> gives it.</summary>
    public string? Commodity { get; init; }

    public string? CommodityLocalised { get; init; }

    public int? Count { get; init; }

    public int? PassengerCount { get; init; }

    public bool PassengerMission { get; init; }

    public long? Reward { get; init; }

    /// <summary>From the latest <c>Missions</c> snapshot where one has listed it, else from the accept.</summary>
    public DateTimeOffset? Expiry { get; init; }

    /// <summary>Whether a <c>MissionRedirected</c> has replaced the destination.</summary>
    public bool Redirected { get; init; }

    public MissionCargo? Cargo { get; init; }

    /// <summary>When it was accepted, or null where no accept has been read.</summary>
    public DateTimeOffset? AcceptedAt { get; init; }

    /// <summary>Whether its <c>MissionAccepted</c> was read; without it only the name is known.</summary>
    public bool HasDetail => AcceptedAt is not null;

    public string Title => LocalisedName ?? Name;

    public string? Destination => (DestinationStation, DestinationSystem) switch
    {
        ({ } station, { } system) => $"{station}, {system}",
        (null, { } system) => system,
        ({ } station, null) => station,
        _ => null,
    };

    /// <summary>
    /// The mission a <c>MissionAccepted</c> or <c>MissionCompleted</c> describes, or null without a
    /// <c>MissionID</c>. <see cref="AcceptedAt"/> is the event's time.
    /// </summary>
    internal static Mission? Of(JournalEvent journalEvent)
    {
        if (journalEvent.Long("MissionID") is not { } id)
        {
            return null;
        }

        return new Mission(id, journalEvent.String("Name") ?? $"Mission {id}")
        {
            LocalisedName = Text(journalEvent.String("LocalisedName")),
            Faction = Text(journalEvent.String("Faction")),
            TargetFaction = Text(journalEvent.String("TargetFaction")),
            DestinationSystem = Text(journalEvent.String("DestinationSystem")),
            DestinationStation = Text(journalEvent.String("DestinationStation"))
                ?? Text(journalEvent.String("DestinationSettlement")),
            Commodity = JournalJson.Symbol(journalEvent.String("Commodity")),
            CommodityLocalised = Text(journalEvent.String("Commodity_Localised")),
            Count = journalEvent.Int("Count"),
            PassengerCount = journalEvent.Int("PassengerCount"),
            PassengerMission = journalEvent.Int("PassengerCount") is > 0,
            Reward = journalEvent.Long("Reward"),
            Expiry = ParseExpiry(journalEvent.String("Expiry")),
            AcceptedAt = journalEvent.Timestamp,
        };
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateTimeOffset? ParseExpiry(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// This detail under what a live entry with no accept has seen since: the live expiry, redirect and
    /// delivery progress are newer.
    /// </summary>
    internal Mission Under(Mission live) => this with
    {
        Expiry = live.Expiry ?? Expiry,
        PassengerMission = PassengerMission || live.PassengerMission,
        DestinationSystem = live.Redirected ? live.DestinationSystem : DestinationSystem,
        DestinationStation = live.Redirected ? live.DestinationStation : DestinationStation,
        Redirected = Redirected || live.Redirected,
        Cargo = live.Cargo ?? Cargo,
    };
}

/// <summary>
/// The Commander's live missions, keyed on <c>MissionID</c>. The <c>Missions</c> snapshot written
/// at login decides what is live; the accept, redirect and depot events supply the detail.
/// </summary>
public sealed record MissionBoard
{
    public static readonly MissionBoard Empty = new();

    public IReadOnlyList<Mission> Missions { get; init; } = [];

    /// <summary>
    /// Ids completed, failed or abandoned since the last snapshot, so a merge onto a board with no snapshot
    /// does not bring them back.
    /// </summary>
    public IReadOnlySet<long> Ended { get; init; } = new HashSet<long>();

    /// <summary>When a <c>Missions</c> snapshot was last folded, or null where none has been.</summary>
    public DateTimeOffset? SnapshotAt { get; init; }

    /// <summary>When any mission event was last folded.</summary>
    public DateTimeOffset? SeenAt { get; init; }

    public bool IsKnown => SeenAt is not null;

    public Mission? For(long id) => Missions.FirstOrDefault(mission => mission.Id == id);

    /// <summary>The missions with an expiry, soonest first, then those without.</summary>
    public IReadOnlyList<Mission> BySoonest() =>
        [.. Missions.OrderBy(mission => mission.Expiry ?? DateTimeOffset.MaxValue).ThenBy(mission => mission.Id)];

    /// <summary>The live missions whose destination is the settlement, or whose target is its faction.</summary>
    public IReadOnlyList<Mission> Concerning(string? settlement, string? faction) =>
        [.. Missions.Where(mission =>
            settlement is { Length: > 0 } && string.Equals(mission.DestinationStation, settlement, StringComparison.OrdinalIgnoreCase)
            || faction is { Length: > 0 } && string.Equals(mission.TargetFaction, faction, StringComparison.OrdinalIgnoreCase))];

    public MissionBoard Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "MissionAccepted" => Accept(journalEvent),
        "MissionRedirected" => Change(journalEvent, mission => mission with
        {
            DestinationSystem = Text(journalEvent.String("NewDestinationSystem")),
            DestinationStation = Text(journalEvent.String("NewDestinationStation")),
            Redirected = true,
        }),
        "CargoDepot" => Change(journalEvent, mission => mission with
        {
            Cargo = new MissionCargo(
                journalEvent.Int("ItemsCollected") ?? mission.Cargo?.Collected ?? 0,
                journalEvent.Int("ItemsDelivered") ?? mission.Cargo?.Delivered ?? 0,
                journalEvent.Int("TotalItemsToDeliver") ?? mission.Cargo?.Total ?? 0),
            Commodity = mission.Commodity ?? JournalJson.Symbol(journalEvent.String("CargoType")),
        }),
        "MissionCompleted" or "MissionFailed" or "MissionAbandoned" => End(journalEvent),
        "Missions" => Snapshot(journalEvent),
        _ => this,
    };

    /// <summary>
    /// This board, recovered from older journals, under what the live journal has folded. Where the live
    /// board has a snapshot it decides what is live and this only fills in detail.
    /// </summary>
    public MissionBoard With(MissionBoard live)
    {
        ArgumentNullException.ThrowIfNull(live);

        if (!live.IsKnown)
        {
            return this;
        }

        var merged = live.Missions
            .Select(mission => !mission.HasDetail && For(mission.Id) is { HasDetail: true } recovered
                ? recovered.Under(mission)
                : mission)
            .ToList();

        if (live.SnapshotAt is null)
        {
            merged.AddRange(Missions.Where(mission => live.For(mission.Id) is null && !live.Ended.Contains(mission.Id)));
        }

        return live with { Missions = merged };
    }

    private MissionBoard Accept(JournalEvent journalEvent)
    {
        if (Mission.Of(journalEvent) is not { } accepted)
        {
            return this;
        }

        var id = accepted.Id;

        return this with
        {
            Missions = [.. Missions.Where(mission => mission.Id != id), accepted],
            SeenAt = Later(journalEvent.Timestamp),
        };
    }

    /// <summary>Changes an entry already on the board; an event for a mission not on it adds nothing.</summary>
    private MissionBoard Change(JournalEvent journalEvent, Func<Mission, Mission> change)
    {
        if (journalEvent.Long("MissionID") is not { } id || For(id) is null)
        {
            return this;
        }

        return this with
        {
            Missions = [.. Missions.Select(mission => mission.Id == id ? change(mission) : mission)],
            SeenAt = Later(journalEvent.Timestamp),
        };
    }

    private MissionBoard End(JournalEvent journalEvent)
    {
        if (journalEvent.Long("MissionID") is not { } id)
        {
            return this;
        }

        return this with
        {
            Missions = [.. Missions.Where(mission => mission.Id != id)],
            Ended = new HashSet<long>(Ended) { id },
            SeenAt = Later(journalEvent.Timestamp),
        };
    }

    private MissionBoard Snapshot(JournalEvent journalEvent)
    {
        var live = new List<Mission>();

        foreach (var entry in journalEvent.Items("Active"))
        {
            if (entry.Long("MissionID") is not { } id)
            {
                continue;
            }

            var mission = For(id) ?? new Mission(id, entry.String("Name") ?? $"Mission {id}");

            live.Add(mission with
            {
                PassengerMission = mission.PassengerMission || entry.Bool("PassengerMission"),
                Expiry = entry.Long("Expires") is { } seconds
                    ? journalEvent.Timestamp.AddSeconds(seconds)
                    : mission.Expiry,
            });
        }

        return this with
        {
            Missions = live,
            Ended = new HashSet<long>(),
            SnapshotAt = journalEvent.Timestamp,
            SeenAt = Later(journalEvent.Timestamp),
        };
    }

    private DateTimeOffset Later(DateTimeOffset arrived) =>
        SeenAt is { } seen && seen > arrived ? seen : arrived;

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
