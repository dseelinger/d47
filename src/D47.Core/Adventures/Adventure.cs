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
/// The five things a beat can wait for (list.md Phase 47, "The trigger vocabulary is closed and the
/// prose is free").
/// <para>
/// <b>The vocabulary is the event catalogue.</b> Core has no list of journal event kinds — every
/// consumer switches on strings — and a catalogue of Frontier's schema written by hand would be a
/// table of theirs. So what a beat may wait for is exactly this, and every member is an integer
/// comparison on a structured field: a system address, a market id, a body id, a rank. Nothing a
/// hostile in-game message can write — a ship name, a chat line, a mission title — can be a trigger,
/// by construction rather than by policy.
/// </para>
/// </summary>
public enum TriggerKind
{
    /// <summary><c>FSDJump</c>, <c>Location</c> or <c>CarrierJump</c> into a <c>SystemAddress</c>.</summary>
    Arrive,

    /// <summary><c>Docked</c> at a <c>MarketID</c>. Never the station name: a carrier's is player-chosen.</summary>
    Dock,

    /// <summary><c>Touchdown</c> on a <c>SystemAddress</c> and <c>BodyID</c>.</summary>
    Land,

    /// <summary><c>Scan</c> of a <c>SystemAddress</c> and <c>BodyID</c>.</summary>
    Scan,

    /// <summary><c>Promotion</c> in a career to at least a rank. Counted, never named — Phase 34's rule.</summary>
    Rank,
}

/// <summary>
/// Where a beat lands on the galaxy. The ids are what match; the names ride beside them so a
/// Commander reading the file, or the panel, knows what the numbers mean.
/// </summary>
public sealed record AdventureTrigger
{
    public required TriggerKind Kind { get; init; }

    public long? SystemAddress { get; init; }

    public long? MarketId { get; init; }

    public int? BodyId { get; init; }

    /// <summary>One of <see cref="Journal.RankState.Careers"/>, in the journal's own spelling.</summary>
    public string? Career { get; init; }

    /// <summary>The rank to reach, 1 to 8. Elite is 8.</summary>
    public int? Rank { get; init; }

    public string? System { get; init; }

    public string? Station { get; init; }

    public string? Body { get; init; }

    /// <summary>
    /// Whether the ids this kind matches on are all present. A trigger can be written with names
    /// only — offline, or from the model — and it is this that says whether it can ever fire.
    /// </summary>
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

    private string In() => System is { Length: > 0 } system && !string.Equals(system, Body, StringComparison.Ordinal)
        ? $" in {system}"
        : string.Empty;

    private static string Address(long? address, int? body = null) =>
        address is null
            ? "an unresolved place"
            : body is null ? $"system {address}" : $"body {body} of system {address}";

    private static string Market(long? market) => market is null ? "an unresolved station" : $"market {market}";
}

/// <summary>
/// One dramatic function, anchored to a place (list.md Phase 47, "Story, not a checklist"). The
/// model is never asked for five stops; it is asked for a structure, and the trigger is where the
/// function lands.
/// </summary>
public sealed record AdventureBeat
{
    /// <summary>The chapter's name, which is what the card shows. Never a number.</summary>
    public required string Title { get; init; }

    /// <summary>Its place in the structure — setup, catalyst, midpoint, all is lost, finale.</summary>
    public string? Function { get; init; }

    public required AdventureTrigger Trigger { get; init; }

    /// <summary>What the ship's AI says when this beat is reached. The last beat's line is the ending.</summary>
    public required string Line { get; init; }
}

/// <summary>
/// The story before the scenes — the blueprint a generated adventure writes in its own turn, and
/// the questions an authored one is offered in the craft's order. Every field optional, because a
/// Commander who wants five stops and five lines is not wrong.
/// </summary>
public sealed record AdventureSpine
{
    public string? Premise { get; init; }

    /// <summary>The outer goal — what the Commander is after in this story.</summary>
    public string? Want { get; init; }

    /// <summary>The inner one — the belief the story tests, and what it would cost to be wrong.</summary>
    public string? Stake { get; init; }

    /// <summary>Where it stops being what it looked like. Withheld from the persona until its beat fires.</summary>
    public string? Turn { get; init; }

    /// <summary>What the last beat means. Withheld likewise.</summary>
    public string? Ending { get; init; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Premise) && string.IsNullOrWhiteSpace(Want) && string.IsNullOrWhiteSpace(Stake)
        && string.IsNullOrWhiteSpace(Turn) && string.IsNullOrWhiteSpace(Ending);
}

/// <summary>
/// A story the Commander progresses through, tracked from their own journal (list.md Phase 47).
/// <para>
/// <b>This is the definition, and one stamp.</b> Progress is never stored: it is a fold over the
/// journal after <see cref="AcceptedAt"/>, computed by <see cref="AdventureFold"/>, because
/// Commanders play with d47 closed and a stored beat pointer would miss every beat that fired while
/// it was not running — permanently. The same rule <see cref="Goals.GoalArc"/> states about figures,
/// arrived at from the sequential side.
/// </para>
/// </summary>
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

    /// <summary>
    /// When the Commander pressed Begin. Null until then; the boundary of the fold; and for a
    /// generated adventure the line between a draft waiting to be agreed to and a story under way.
    /// </summary>
    public DateTimeOffset? AcceptedAt { get; init; }

    /// <summary>Null unless abandoned. The fold stops here, and Begin again clears it.</summary>
    public DateTimeOffset? AbandonedAt { get; init; }

    /// <summary>
    /// The draft before the last revision, kept on a generated adventure that has not begun so
    /// <em>Put it back</em> costs a press and not a model call. Dropped on Begin.
    /// </summary>
    public Adventure? Previous { get; init; }

    public bool IsBegun => AcceptedAt is not null;

    public bool IsAbandoned => AbandonedAt is not null;

    /// <summary>A generated adventure nobody has agreed to yet. The proposal, in the one file.</summary>
    public bool IsDraft => Source == AdventureSource.Generated && AcceptedAt is null;

    /// <summary>Running: begun and not abandoned. Whether it has finished is the fold's to say.</summary>
    public bool IsActive => IsBegun && !IsAbandoned;
}

/// <summary>Bounds, so a hand-edited file cannot be a novel and a model cannot be asked for one.</summary>
public static class AdventureLimits
{
    public const int MaxAdventures = 40;

    public const int MaxBeats = 12;

    public const int MaxNameLength = 80;

    public const int MaxTitleLength = 80;

    /// <summary>
    /// A beat's line or the opening. A few sentences of prose rather than a checklist line, so
    /// its own limit rather than <see cref="Checklists.ChecklistDocument.MaxTextLength"/>.
    /// </summary>
    public const int MaxLineLength = 900;

    public const int MaxSpineLength = 700;
}
