namespace D47.Core.Adventures;

/// <summary>How an adventure arrived, which decides how much a Commander should trust its prose.</summary>
public enum AdventureSource
{
    /// <summary>The Commander's own words, written on the panel or in the file.</summary>
    Commander,

    /// <summary>Written by the ship's AI once, and accepted by a person before it could begin.</summary>
    Generated,
}

/// <summary>
/// The five things a beat can wait for (Phase 47, "The trigger vocabulary is closed and the prose is
/// free").
/// </summary>
public enum TriggerKind
{
    /// <summary><c>FSDJump</c>, <c>Location</c> or <c>CarrierJump</c> into a <c>SystemAddress</c>.</summary>
    Arrive,

    /// <summary><c>Docked</c> at a <c>MarketID</c>.</summary>
    Dock,

    /// <summary><c>Touchdown</c> on a <c>SystemAddress</c> and <c>BodyID</c>.</summary>
    Land,

    /// <summary><c>Scan</c> of a <c>SystemAddress</c> and <c>BodyID</c>.</summary>
    Scan,

    /// <summary><c>Promotion</c> in a career to at least a rank.</summary>
    Rank,
}

/// <summary>Where a beat lands on the galaxy.</summary>
public sealed record AdventureTrigger
{
    public required TriggerKind Kind { get; init; }

    public long? SystemAddress { get; init; }

    public long? MarketId { get; init; }

    public int? BodyId { get; init; }

    /// <summary>One of <see cref="Journal.RankState.Careers"/>, in the journal's own spelling.</summary>
    public string? Career { get; init; }

    /// <summary>The rank to reach, 1 to 8.</summary>
    public int? Rank { get; init; }

    public string? System { get; init; }

    public string? Station { get; init; }

    public string? Body { get; init; }

    /// <summary>Whether the ids this kind matches on are all present.</summary>
    public bool IsResolved => Kind switch
    {
        TriggerKind.Arrive => SystemAddress is not null,
        TriggerKind.Dock => MarketId is not null,
        TriggerKind.Land or TriggerKind.Scan => SystemAddress is not null && BodyId is not null,
        TriggerKind.Rank => Career is not null && Rank is not null,
        _ => false,
    };

    /// <summary>The trigger in words — "arrive at Ossen's Lantern", "reach Exploration rank 6".</summary>
    public string Describe() => Kind switch
    {
        TriggerKind.Arrive => $"arrive at {System ?? Address(SystemAddress)}",
        TriggerKind.Dock => $"dock at {Station ?? Market(MarketId)}{In()}",
        TriggerKind.Land => $"land on {Body ?? Address(SystemAddress, BodyId)}{In()}",
        TriggerKind.Scan => $"scan {Body ?? Address(SystemAddress, BodyId)}{In()}",
        TriggerKind.Rank => $"reach {Careers.Word(Career)} rank {Rank}",
        _ => Kind.ToString(),
    };

    /// <summary>
    /// The trigger as a hand-off — "Next: dock at Maren Anchorage in Dyson's Hollow." — said with the
    /// beat before it.
    /// </summary>
    public string HandOff() => Kind == TriggerKind.Scan
        ? $"Next: {Describe()} — the ship's own scanner from supercruise does it, or a close pass; no surface scanner is needed, and simply going there counts if you have scanned it before."
        : $"Next: {Describe()}.";

    private string In() => System is { Length: > 0 } system && !string.Equals(system, Body, StringComparison.Ordinal)
        ? $" in {system}"
        : string.Empty;

    private static string Address(long? address, int? body = null) =>
        address is null
            ? "an unresolved place"
            : body is null ? $"system {address}" : $"body {body} of system {address}";

    private static string Market(long? market) => market is null ? "an unresolved station" : $"market {market}";
}

/// <summary>One dramatic function, anchored to a place (Phase 47, "Story, not a checklist").</summary>
public sealed record AdventureBeat
{
    /// <summary>The chapter's name, which is what the card shows.</summary>
    public required string Title { get; init; }

    /// <summary>Its place in the structure — setup, catalyst, midpoint, all is lost, finale.</summary>
    public string? Function { get; init; }

    public required AdventureTrigger Trigger { get; init; }

    /// <summary>What the ship's AI says when this beat is reached.</summary>
    public required string Line { get; init; }
}

/// <summary>
/// The story before the scenes — the blueprint a generated adventure writes in its own turn, and the
/// questions an authored one is offered in the craft's order.
/// </summary>
public sealed record AdventureSpine
{
    public string? Premise { get; init; }

    /// <summary>The outer goal — what the Commander is after in this story.</summary>
    public string? Want { get; init; }

    /// <summary>The inner one — the belief the story tests, and what it would cost to be wrong.</summary>
    public string? Stake { get; init; }

    /// <summary>Where it stops being what it looked like.</summary>
    public string? Turn { get; init; }

    /// <summary>What the last beat means.</summary>
    public string? Ending { get; init; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Premise) && string.IsNullOrWhiteSpace(Want) && string.IsNullOrWhiteSpace(Stake)
        && string.IsNullOrWhiteSpace(Turn) && string.IsNullOrWhiteSpace(Ending);
}

/// <summary>A story the Commander progresses through, tracked from their own journal (Phase 47).</summary>
public sealed record Adventure
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public AdventureSource Source { get; init; } = AdventureSource.Commander;

    public DateTimeOffset? Written { get; init; }

    /// <summary>The persona id that wrote a generated one, or null for the Commander or no persona.</summary>
    public string? WrittenBy { get; init; }

    public AdventureSpine? Spine { get; init; }

    /// <summary>The line spoken when it begins — the beat before the first beat.</summary>
    public string? Opening { get; init; }

    public IReadOnlyList<AdventureBeat> Beats { get; init; } = [];

    /// <summary>When the Commander pressed Begin.</summary>
    public DateTimeOffset? AcceptedAt { get; init; }

    /// <summary>Null unless abandoned.</summary>
    public DateTimeOffset? AbandonedAt { get; init; }

    /// <summary>
    /// The draft before the last revision, kept on a generated adventure that has not begun so Put it
    /// back costs a press and not a model call.
    /// </summary>
    public Adventure? Previous { get; init; }

    /// <summary>
    /// What was actually said about this story, oldest first (asked for 2026-08-22) — the beats as the
    /// Commander heard them and the asides between them.
    /// </summary>
    public IReadOnlyList<AdventureTold> Told { get; init; } = [];

    public bool IsBegun => AcceptedAt is not null;

    public bool IsAbandoned => AbandonedAt is not null;

    /// <summary>A generated adventure nobody has agreed to yet.</summary>
    public bool IsDraft => Source == AdventureSource.Generated && AcceptedAt is null;

    /// <summary>Running: begun and not abandoned.</summary>
    public bool IsActive => IsBegun && !IsAbandoned;
}

/// <summary>Bounds, so a hand-edited file cannot be a novel and a model cannot be asked for one.</summary>
public static class AdventureLimits
{
    public const int MaxAdventures = 40;

    public const int MaxBeats = 12;

    public const int MaxNameLength = 80;

    public const int MaxTitleLength = 80;

    /// <summary>A beat's line or the opening.</summary>
    public const int MaxLineLength = 900;

    public const int MaxSpineLength = 700;

    /// <summary>How much of what was said is kept per story.</summary>
    public const int MaxTold = 60;
}
