using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>Where a scene happens: on foot at a settlement, or in a ship fight.</summary>
public enum ScenePlace
{
    Settlement,
    Ship,
}

/// <summary>What just happened in a scene, as a beat the people there react to.</summary>
public enum SceneBeatKind
{
    /// <summary>The Commander is on foot at the settlement and nobody knows yet.</summary>
    Arrived,

    /// <summary>The first attack on the Commander in the scene: they have been found.</summary>
    Spotted,

    /// <summary>The Commander killed someone on foot.</summary>
    Down,

    /// <summary>Someone killed the Commander: on foot at a settlement, or in a ship fight.</summary>
    CommanderDown,

    /// <summary>
    /// The Commander left: boarded a ship, an SRV or a taxi after being spotted or killing, or entered
    /// supercruise after a ship fight.
    /// </summary>
    Gone,

    /// <summary>The Commander dropped into a conflict zone, an extraction site or pirate activity.</summary>
    Site,

    /// <summary>A pilot pulled the Commander out of supercruise.</summary>
    Interdicted,

    /// <summary>The first attack on the Commander's ship in the scene.</summary>
    Engaged,

    /// <summary>The Commander destroyed a ship.</summary>
    ShipDown,
}

/// <summary>One beat of a scene, with what the brief for it needs.</summary>
/// <param name="Scene">Which scene of the session this belongs to, counted from one.</param>
/// <param name="Settlement">The settlement, for a scene on foot; null in a ship.</param>
/// <param name="Faction">The settlement's owner; in a ship, the interdictor's faction or the last victim's.</param>
/// <param name="Victim">Who was killed, for <see cref="SceneBeatKind.Down"/> and <see cref="SceneBeatKind.ShipDown"/>.</param>
/// <param name="Kills">The Commander's kills so far in the scene.</param>
/// <param name="Merged">How many of those kills this beat reports.</param>
/// <param name="Seen">Whether any attack on the Commander has come since the scene opened.</param>
/// <param name="Killer">Who killed the Commander, for <see cref="SceneBeatKind.CommanderDown"/>.</param>
/// <param name="Site">The kind of place dropped into, such as "Conflict Zone [Low Intensity]".</param>
/// <param name="Ship">The victim's ship for <see cref="SceneBeatKind.ShipDown"/>, the killer's for <see cref="SceneBeatKind.CommanderDown"/>.</param>
/// <param name="Sides">Both factions of a conflict zone, from its kill bonds.</param>
public sealed record SceneBeat(
    SceneBeatKind Kind,
    int Scene,
    string? Settlement,
    string? Faction,
    string? Government,
    string? Body,
    string? Victim = null,
    int Kills = 0,
    int Merged = 0,
    bool Seen = false,
    string? Killer = null,
    ScenePlace Place = ScenePlace.Settlement,
    string? System = null,
    string? Site = null,
    string? Interdictor = null,
    bool Submitted = false,
    string? Ship = null,
    IReadOnlyList<string>? Sides = null);

/// <summary>The scene the Commander is in, or the last one, as folded from the journal.</summary>
/// <param name="Beats">How many beats have been folded this session; a change means <see cref="Last"/> is new.</param>
public sealed record SceneSnapshot(bool Open, int Beats, SceneBeat? Last)
{
    public static readonly SceneSnapshot None = new(false, 0, null);

    /// <summary>Whether a ship scene is open.</summary>
    public bool InShip => Open && Last?.Place == ScenePlace.Ship;
}

/// <summary>
/// A scene at a settlement on foot: opened by <c>Disembark</c> on the body of the last
/// <c>ApproachSettlement</c>, closed by <c>SupercruiseEntry</c>, <c>FSDJump</c>, <c>LoadGame</c>,
/// <c>Died</c> or an approach to another settlement. Or a scene in a ship: opened by a drop into a
/// conflict zone, an extraction site or pirate activity, an NPC's interdiction, or the first attack in
/// normal space with no scene open, and closed by <c>SupercruiseEntry</c>, <c>FSDJump</c>,
/// <c>LoadGame</c>, <c>Died</c>, <c>Docked</c> or <c>Disembark</c>.
/// </summary>
public sealed class SceneTracker
{
    private static readonly string[] Sites =
        ["Conflict Zone", "Power Conflict Zone", "Resource Extraction Site", "Pirate Activity Detected"];

    private SceneSnapshot _snapshot = SceneSnapshot.None;

    private Approach? _approach;
    private Approach? _open;
    private Fight? _fight;
    private string? _system;
    private bool _normalSpace;
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
                _normalSpace = false;
                CloseFight();
                Disembarked(journalEvent);
                break;

            case "SupercruiseDestinationDrop":
                Dropped(journalEvent);
                break;

            case "Interdicted" when !journalEvent.Bool("IsPlayer"):
                _normalSpace = true;
                OpenFight(new Fight
                {
                    Faction = KillCallout.Resolved(journalEvent.Named("Faction")),
                    Interdictor = KillCallout.Resolved(journalEvent.Named("Interdictor")),
                    Submitted = journalEvent.Bool("Submitted"),
                });
                Beat(SceneBeatKind.Interdicted);
                break;

            case "SupercruiseExit":
                _normalSpace = true;
                _system = journalEvent.String("StarSystem") ?? _system;
                break;

            case "Undocked" or "Liftoff" or "DockSRV":
                _normalSpace = true;
                break;

            case "Docked":
                _normalSpace = false;
                CloseFight();
                break;

            case "LaunchSRV":
                _normalSpace = false;
                break;

            case "Location":
                _system = journalEvent.String("StarSystem") ?? _system;
                break;

            case "UnderAttack":
                Attacked();
                break;

            case "Bounty" when _open is not null && KillCallout.OnFoot(journalEvent):
                Killed(KillCallout.Resolved(journalEvent.String("PilotName_Localised")));
                break;

            case "Bounty" when _fight is not null && !KillCallout.OnFoot(journalEvent):
                ShipKilled(KillCallout.PilotOf(journalEvent), KillCallout.ShipOf(journalEvent), journalEvent.Named("VictimFaction"));
                break;

            case "FactionKillBond" when _fight is not null:
                Sided(journalEvent.Named("AwardingFaction"));
                Sided(journalEvent.Named("VictimFaction"));
                ShipKilled(null, null, journalEvent.Named("VictimFaction"));
                break;

            case "CommitCrime" when _open is not null
                && string.Equals(journalEvent.String("CrimeType"), "onFoot_murder", StringComparison.OrdinalIgnoreCase):
                Killed(KillCallout.Resolved(journalEvent.String("Victim")));
                break;

            case "Died":
                _normalSpace = false;
                Died(journalEvent);
                break;

            case "Embark":
                _normalSpace = !journalEvent.Bool("SRV") && !journalEvent.Bool("Taxi") && !journalEvent.Bool("OnStation");

                if (_open is not null && _stirred)
                {
                    _stirred = false;
                    Beat(SceneBeatKind.Gone);
                }

                break;

            case "SupercruiseEntry":
                _normalSpace = false;

                if (_fight is not null && _stirred)
                {
                    Beat(SceneBeatKind.Gone);
                }

                _approach = null;
                Close();
                break;

            case "FSDJump" or "LoadGame":
                _normalSpace = false;
                _system = journalEvent.String("StarSystem");
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
        Opened();

        Beat(SceneBeatKind.Arrived);
    }

    private void Dropped(JournalEvent journalEvent)
    {
        if (journalEvent.Named("Type") is not { } type
            || !Sites.Any(site => type.StartsWith(site, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _normalSpace = true;
        OpenFight(new Fight { Site = type });
        Beat(SceneBeatKind.Site);
    }

    private void Attacked()
    {
        if (_open is null && _fight is null && _normalSpace)
        {
            OpenFight(new Fight());
        }

        if (_seen || _open is null && _fight is null)
        {
            return;
        }

        _seen = true;
        _stirred = true;
        Beat(_fight is null ? SceneBeatKind.Spotted : SceneBeatKind.Engaged);
    }

    private void OpenFight(Fight fight)
    {
        Close();

        _fight = fight;
        Opened();
    }

    private void Opened()
    {
        _scene++;
        _kills = 0;
        _seen = false;
        _stirred = false;
    }

    private void Killed(string? victim)
    {
        _kills++;
        _stirred = true;

        Beat(SceneBeatKind.Down, victim: victim);
    }

    private void ShipKilled(string? pilot, string? ship, string? faction)
    {
        _kills++;
        _stirred = true;
        _fight!.Faction = KillCallout.Resolved(faction) ?? _fight.Faction;

        Beat(SceneBeatKind.ShipDown, victim: pilot, ship: ship);
    }

    private void Sided(string? faction)
    {
        if (KillCallout.Resolved(faction) is { } side
            && !_fight!.Sides.Contains(side, StringComparer.OrdinalIgnoreCase))
        {
            _fight.Sides = [.. _fight.Sides, side];
        }
    }

    private void Died(JournalEvent journalEvent)
    {
        var ship = journalEvent.String("KillerShip");
        var onFoot = ship is not null && ship.Contains("suitai", StringComparison.OrdinalIgnoreCase);
        var killer = KillCallout.Resolved(journalEvent.String("KillerName"));

        if (_open is not null && onFoot)
        {
            Beat(SceneBeatKind.CommanderDown, killer: killer);
        }
        else if (_fight is not null && ship is { Length: > 0 } && !onFoot)
        {
            Beat(SceneBeatKind.CommanderDown, killer: killer, ship: EliteSpecifications.Ship(ship)?.Name);
        }

        Close();
    }

    private void CloseFight()
    {
        if (_fight is not null)
        {
            Close();
        }
    }

    private void Close()
    {
        if (_open is null && _fight is null)
        {
            return;
        }

        _open = null;
        _fight = null;

        var current = _snapshot;
        Volatile.Write(ref _snapshot, current with { Open = false });
    }

    private void Beat(SceneBeatKind kind, string? victim = null, string? killer = null, string? ship = null)
    {
        var beat = _fight is { } fight
            ? new SceneBeat(
                kind, _scene, null, fight.Faction, null, null, victim, _kills, Merged: 0, _seen, killer,
                ScenePlace.Ship, _system, fight.Site, fight.Interdictor, fight.Submitted, ship,
                fight.Sides.Count > 1 ? fight.Sides : null)
            : new SceneBeat(
                kind, _scene, _open!.Name, _open.Faction, _open.Government, _open.Body, victim, _kills, Merged: 0, _seen, killer);

        Volatile.Write(ref _snapshot, new SceneSnapshot(true, _snapshot.Beats + 1, beat));
    }

    private sealed record Approach(string Name, string? Faction, string? Government, string? Body);

    private sealed class Fight
    {
        public string? Faction { get; set; }

        public string? Site { get; set; }

        public string? Interdictor { get; set; }

        public bool Submitted { get; set; }

        public IReadOnlyList<string> Sides { get; set; } = [];
    }
}
