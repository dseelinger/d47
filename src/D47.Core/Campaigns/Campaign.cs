using System.Collections.Immutable;
using D47.Core.Journal;

namespace D47.Core.Campaigns;

/// <summary>What the Commander means to do with the campaign faction in one system.</summary>
public enum SystemIntent
{
    Hold,
    Grow,
    ExpandFromHere,
    StayUnder75,
    TakeControl,
    LeaveAlone,
}

/// <summary>The Commander's BGS work of one kind on a resolved front: signed marks for missions, credits otherwise.</summary>
public sealed record ChronicleWork(BgsWorkKind Kind, long Amount);

/// <summary>
/// A resolved front: what kind it was, where, against whom, how it ended, the ticks it spanned and the
/// Commander's own BGS work on it.
/// </summary>
public sealed record ChronicleEntry(
    string Kind,
    string System,
    string? Opponent,
    string Outcome,
    IReadOnlyList<DateTimeOffset> Ticks,
    IReadOnlyList<ChronicleWork> Work);

/// <summary>One Commander's campaign: the faction, an intent per system, and the chronicle of resolved fronts.</summary>
public sealed record Campaign
{
    public static readonly Campaign None = new();

    /// <summary>The faction the Commander named, or null to follow the squadron's faction.</summary>
    public string? Faction { get; init; }

    /// <summary>The intents the Commander has set, by system name, case ignored.</summary>
    public ImmutableDictionary<string, SystemIntent> Intents { get; init; } =
        ImmutableDictionary.Create<string, SystemIntent>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ChronicleEntry> Chronicle { get; init; } = [];

    /// <summary>The faction named, else <paramref name="squadronFaction"/>.</summary>
    public string? FactionOr(string? squadronFaction) =>
        Faction is { Length: > 0 } named ? named : squadronFaction is { Length: > 0 } ? squadronFaction : null;

    /// <summary>The intent set for <paramref name="system"/>, else Hold where the faction controls it and LeaveAlone where it does not.</summary>
    public SystemIntent IntentFor(string system, bool controls) =>
        Intents.TryGetValue(system, out var set) ? set : controls ? SystemIntent.Hold : SystemIntent.LeaveAlone;

    public bool IsEmpty => Faction is null && Intents.IsEmpty && Chronicle.Count == 0;
}
