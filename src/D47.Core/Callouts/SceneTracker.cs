using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>What just happened in a scene at a settlement, as a beat the people there react to.</summary>
public enum SceneBeatKind
{
    /// <summary>The Commander is on foot at the settlement and nobody knows yet.</summary>
    Arrived,

    /// <summary>The first attack on the Commander in the scene: they have been found.</summary>
    Spotted,

    /// <summary>The Commander killed someone on foot.</summary>
    Down,

    /// <summary>Someone on foot killed the Commander.</summary>
    CommanderDown,

    /// <summary>The Commander boarded a ship, an SRV or a taxi after being spotted or killing.</summary>
    Gone,
}

/// <summary>One beat of a scene, with what the brief for it needs.</summary>
/// <param name="Scene">Which scene of the session this belongs to, counted from one.</param>
/// <param name="Victim">Who was killed, for <see cref="SceneBeatKind.Down"/>.</param>
/// <param name="Kills">The Commander's kills so far in the scene.</param>
/// <param name="Merged">How many of those kills this beat reports.</param>
/// <param name="Seen">Whether any attack on the Commander has come since the scene opened.</param>
/// <param name="Killer">Who killed the Commander, for <see cref="SceneBeatKind.CommanderDown"/>.</param>
public sealed record SceneBeat(
    SceneBeatKind Kind,
    int Scene,
    string Settlement,
    string? Faction,
    string? Government,
    string? Body,
    string? Victim = null,
    int Kills = 0,
    int Merged = 0,
    bool Seen = false,
    string? Killer = null);

/// <summary>The scene the Commander is in, or the last one, as folded from the journal.</summary>
/// <param name="Beats">How many beats have been folded this session; a change means <see cref="Last"/> is new.</param>
public sealed record SceneSnapshot(bool Open, int Beats, SceneBeat? Last)
{
    public static readonly SceneSnapshot None = new(false, 0, null);
}

/// <summary>
/// A scene at a settlement on foot: opened by <c>Disembark</c> on the body of the last
/// <c>ApproachSettlement</c>, closed by <c>SupercruiseEntry</c>, <c>FSDJump</c>, <c>LoadGame</c>,
/// <c>Died</c> or an approach to another settlement.
/// </summary>
public sealed class SceneTracker
{
    private SceneSnapshot _snapshot = SceneSnapshot.None;

    private Approach? _approach;
    private Approach? _open;
    private int _scene;
    private int _kills;
    private bool _seen;
    private bool _stirred;

    /// <summary>Immutable; safe to read from any thread.</summary>
    public SceneSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Folds one event. Tick thread only.</summary>
    public void Fold(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "ApproachSettlement":
                Approached(journalEvent);
                break;

            case "Disembark":
                Disembarked(journalEvent);
                break;

            case "UnderAttack" when _open is not null && !_seen:
                _seen = true;
                _stirred = true;
                Beat(SceneBeatKind.Spotted);
                break;

            case "Bounty" when _open is not null && KillCallout.OnFoot(journalEvent):
                Killed(KillCallout.Resolved(journalEvent.String("PilotName_Localised")));
                break;

            case "CommitCrime" when _open is not null
                && string.Equals(journalEvent.String("CrimeType"), "onFoot_murder", StringComparison.OrdinalIgnoreCase):
                Killed(KillCallout.Resolved(journalEvent.String("Victim")));
                break;

            case "Died":
                Died(journalEvent);
                break;

            case "Embark" when _open is not null && _stirred:
                _stirred = false;
                Beat(SceneBeatKind.Gone);
                break;

            case "SupercruiseEntry" or "FSDJump" or "LoadGame":
                _approach = null;
                Close();
                break;
        }
    }

    private void Approached(JournalEvent journalEvent)
    {
        if (journalEvent.String("Name") is not { Length: > 0 } name)
        {
            return;
        }

        if (_open is not null && !string.Equals(_open.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            Close();
        }

        _approach = new Approach(
            name,
            journalEvent.Object("StationFaction")?.String("Name"),
            journalEvent.Named("StationGovernment") is { Length: > 0 } government && government[0] != '$' ? government : null,
            journalEvent.String("BodyName"));
    }

    private void Disembarked(JournalEvent journalEvent)
    {
        if (_open is not null
            || _approach is not { } approach
            || !journalEvent.Bool("OnPlanet")
            || journalEvent.Bool("Taxi")
            || !string.Equals(journalEvent.String("Body"), approach.Body, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _open = approach;
        _scene++;
        _kills = 0;
        _seen = false;
        _stirred = false;

        Beat(SceneBeatKind.Arrived);
    }

    private void Killed(string? victim)
    {
        _kills++;
        _stirred = true;

        Beat(SceneBeatKind.Down, victim: victim);
    }

    private void Died(JournalEvent journalEvent)
    {
        if (_open is not null
            && journalEvent.String("KillerShip") is { } ship
            && ship.Contains("suitai", StringComparison.OrdinalIgnoreCase))
        {
            Beat(SceneBeatKind.CommanderDown, killer: KillCallout.Resolved(journalEvent.String("KillerName")));
        }

        Close();
    }

    private void Close()
    {
        if (_open is null)
        {
            return;
        }

        _open = null;

        var current = _snapshot;
        Volatile.Write(ref _snapshot, current with { Open = false });
    }

    private void Beat(SceneBeatKind kind, string? victim = null, string? killer = null)
    {
        var at = _open!;
        var beat = new SceneBeat(
            kind, _scene, at.Name, at.Faction, at.Government, at.Body, victim, _kills, Merged: 0, _seen, killer);

        Volatile.Write(ref _snapshot, new SceneSnapshot(true, _snapshot.Beats + 1, beat));
    }

    private sealed record Approach(string Name, string? Faction, string? Government, string? Body);
}
