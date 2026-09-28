using System.Collections.Immutable;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>What the journal says about the fight around the Commander in this system.</summary>
public sealed record FightSnapshot(
    int Kills,
    string? LastVictim,
    string? LastShip,
    string? LastVictimFaction,
    DateTimeOffset? LastKillAt,
    DateTimeOffset? LastActionAt,
    IReadOnlySet<string> Dead)
{
    public static readonly FightSnapshot None = new(
        0, null, null, null, null, null, ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Kills and attacks in the current system, folded from the journal. A kill is a <c>Bounty</c> or a
/// <c>FactionKillBond</c>; an action is a kill or an <c>UnderAttack</c>.
/// </summary>
public sealed class NearbyFight
{
    /// <summary>How long after the last action the fight still counts as on.</summary>
    public static readonly TimeSpan Holds = TimeSpan.FromSeconds(60);

    private FightSnapshot _snapshot = FightSnapshot.None;
    private string? _system;

    /// <summary>Immutable; safe to read from any thread.</summary>
    public FightSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>
    /// Folds one event, stamping kills and actions with <paramref name="now"/>. While priming, the events
    /// are from before launch: they add names and counts but no times. Tick thread only.
    /// </summary>
    public void Fold(JournalEvent journalEvent, DateTimeOffset now, bool priming)
    {
        var current = _snapshot;

        var next = journalEvent.Kind switch
        {
            "LoadGame" or "Died" => FightSnapshot.None,
            "FSDJump" or "CarrierJump" or "Location" => Arrived(journalEvent, current),
            "Bounty" or "FactionKillBond" => Killed(journalEvent, current, now, priming),
            "UnderAttack" when !priming => current with { LastActionAt = now },
            _ => current,
        };

        if (!ReferenceEquals(next, current))
        {
            Volatile.Write(ref _snapshot, next);
        }
    }

    /// <summary>Whether the fight is on: Elite's danger flag, or an action inside <see cref="Holds"/>.</summary>
    public bool On(DateTimeOffset now, GameStatus status) =>
        status.Has(StatusFlags.InDanger)
        || Snapshot.LastActionAt is { } at && now - at < Holds;

    private FightSnapshot Arrived(JournalEvent journalEvent, FightSnapshot current)
    {
        if (journalEvent.String("StarSystem") is not { } system
            || string.Equals(system, _system, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        _system = system;

        return FightSnapshot.None;
    }

    private static FightSnapshot Killed(
        JournalEvent journalEvent, FightSnapshot current, DateTimeOffset now, bool priming)
    {
        var bond = journalEvent.Kind == "FactionKillBond";
        var npc = bond || KillCallout.IsCommander(journalEvent) ? null : KillCallout.Resolved(journalEvent.String("PilotName_Localised"));

        return current with
        {
            Kills = current.Kills + 1,
            LastVictim = bond ? null : KillCallout.PilotOf(journalEvent),
            LastShip = bond || KillCallout.OnFoot(journalEvent) ? null : KillCallout.ShipOf(journalEvent),
            LastVictimFaction = KillCallout.Resolved(journalEvent.Named("VictimFaction")),
            LastKillAt = priming ? current.LastKillAt : now,
            LastActionAt = priming ? current.LastActionAt : now,
            Dead = npc is null ? current.Dead : current.Dead.Append(npc).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }
}
