using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>
/// The music folders, and which one the Commander is in: Elite's own music track where it has a folder
/// with tracks, otherwise what Status.json says (Phase 12, "Ambient music").
/// </summary>
public static class Situations
{
    public const string General = "general";
    public const string Docked = "docked";
    public const string Supercruise = "supercruise";
    public const string NormalSpace = "normal-space";
    public const string OnFoot = "on-foot";
    public const string OnFootStation = "on-foot-station";
    public const string Settlement = "settlement";
    public const string CombatGround = "combat-ground";

    /// <summary>
    /// The folder each Elite <c>MusicTrack</c> plays from. <c>NoTrack</c>, <c>NoInGameMusic</c> and values
    /// not listed have none.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ByTrack =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Starport"] = Docked,
            ["Exploration"] = NormalSpace,
            ["Supercruise"] = Supercruise,
            ["OnFoot"] = OnFoot,
            ["MainMenu"] = "main-menu",
            ["DockingComputer"] = "docking-computer",
            ["GalaxyMap"] = "galaxy-map",
            ["Combat_Dogfight"] = "combat-dogfight",
            ["FleetCarrier_Managment"] = "fleet-carrier",
            ["SystemMap"] = "system-map",
            ["SystemAndSurfaceScanner"] = "scanner",
            ["Combat_LargeDogFight"] = "combat-large",
            ["CombatLargeDogFight"] = "combat-large",
            ["Squadrons"] = "squadrons",
            ["DestinationFromSupercruise"] = "arrival-from-supercruise",
            ["GalacticPowers"] = "powerplay",
            ["GuardianSites"] = "guardian-sites",
            ["DestinationFromHyperspace"] = "arrival-from-hyperspace",
            ["Combat_SRV"] = "combat-srv",
            ["Unknown_Exploration"] = "unknown-exploration",
            ["Codex"] = "codex",
            ["CQCMenu"] = "cqc-menu",
            ["Interdiction"] = "interdiction",
            ["Lifeform_FogCloud"] = "fog-cloud",
            ["CQC"] = "cqc",
            ["Titan_Encounter"] = "titan-encounter",
            ["Titan_Graveyard"] = "titan-graveyard",
            ["TGFactory"] = "thargoid-factory",
            ["Thargoid_GroundCombat"] = "combat-thargoid-ground",
            ["Combat_Hunters"] = "combat-hunters",
            ["Damaged_Starport"] = "damaged-starport",
            ["Combat_Unknown"] = "combat-unknown",
            ["Unknown_Encounter"] = "unknown-encounter",
            ["CapitalShip"] = "capital-ship",
            ["Unknown_Settlement"] = "unknown-settlement",
        };

    /// <summary>Folders of short cues: each plays one track, then the situation's own music takes over.</summary>
    public static readonly IReadOnlySet<string> OneShot =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "arrival-from-supercruise", "arrival-from-hyperspace", "interdiction",
        };

    /// <summary>Every music folder: the Status.json situations first, then the rest of the track folders.</summary>
    public static readonly IReadOnlyList<string> All =
        new[] { General, Docked, Supercruise, NormalSpace, OnFoot, OnFootStation, Settlement, CombatGround }
            .Concat(ByTrack.Values)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Elite's general tracks, which give way to a folder D47 chose from its own state.</summary>
    private static readonly IReadOnlySet<string> GeneralTracks =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OnFoot", "Starport", "Exploration" };

    /// <summary>
    /// A folder D47 chose from its own state that has tracks, or a docked folder with tracks, wins over
    /// Elite's general tracks and over no track; any other Elite track with a folder that has tracks wins over that state; otherwise
    /// <see cref="For(GameStatus, bool, bool)"/>.
    /// </summary>
    public static string For(
        GameStatus status,
        string? musicTrack,
        CueLibrary library,
        bool atSettlement = false,
        bool inGroundConflict = false)
    {
        var own = For(status, library, atSettlement, inGroundConflict);
        var chosen = IsChosen(own) || (own == Docked && library.Music(Docked).Count > 0);

        if (musicTrack is null || !ByTrack.TryGetValue(musicTrack, out var folder))
        {
            return own;
        }

        if (chosen && GeneralTracks.Contains(musicTrack))
        {
            return own;
        }

        return library.Music(folder).Count > 0 ? folder : own;
    }

    /// <summary>
    /// <see cref="For(GameStatus, bool, bool)"/>, except that a folder with no tracks is skipped for the folders
    /// used before it existed.
    /// </summary>
    public static string For(
        GameStatus status, CueLibrary library, bool atSettlement = false, bool inGroundConflict = false)
    {
        var folder = For(status, atSettlement, inGroundConflict);

        if (folder == CombatGround && library.Music(folder).Count == 0)
        {
            folder = For(status, atSettlement);
        }

        if (folder == Settlement && library.Music(folder).Count == 0)
        {
            folder = For(status);
        }

        if (folder == OnFootStation && library.Music(folder).Count == 0)
        {
            return OnFoot;
        }

        return folder;
    }

    private static bool IsChosen(string folder) =>
        string.Equals(folder, OnFootStation, StringComparison.Ordinal)
        || string.Equals(folder, Settlement, StringComparison.Ordinal)
        || string.Equals(folder, CombatGround, StringComparison.Ordinal);

    /// <summary>
    /// A pure function of the state Elite last wrote and what <see cref="SettlementVisit"/> holds — no
    /// clock, no thread.
    /// </summary>
    public static string For(GameStatus status, bool atSettlement = false, bool inGroundConflict = false)
    {
        if (!status.IsKnown)
        {
            return General;
        }

        if (inGroundConflict && status.OnFoot)
        {
            return CombatGround;
        }

        if (atSettlement
            && (status.OnFoot ? status.Has2(StatusFlags2.OnFootOnPlanet) : status.Has(StatusFlags.InSrv)))
        {
            return Settlement;
        }

        if (status.OnFoot)
        {
            return status.Has2(StatusFlags2.OnFootInStation)
                   || status.Has2(StatusFlags2.OnFootInHangar)
                   || status.Has2(StatusFlags2.OnFootSocialSpace)
                ? OnFootStation
                : OnFoot;
        }

        if (status.Has(StatusFlags.Docked))
        {
            return Docked;
        }

        if (status.Has(StatusFlags.Supercruise))
        {
            return Supercruise;
        }

        // Landed counts as normal space rather than as docked: a rock is not a station, and it is certainly
        // not supercruise.
        return status.Has(StatusFlags.InMainShip)
               || status.Has(StatusFlags.InFighter)
               || status.Has(StatusFlags.InSrv)
            ? NormalSpace
            : General;
    }
}

/// <summary>The <c>MusicTrack</c> of the latest <c>Music</c> event, kept across polls.</summary>
public sealed class EliteMusic
{
    /// <summary>Null until a <c>Music</c> event has been read.</summary>
    public string? Track { get; private set; }

    public void Observe(IEnumerable<JournalEvent> events)
    {
        foreach (var journalEvent in events)
        {
            if (journalEvent.Kind == "Music")
            {
                Track = journalEvent.String("MusicTrack");
            }
        }
    }
}

/// <summary>
/// The settlement of the latest <c>ApproachSettlement</c>, held until <c>SupercruiseEntry</c>, <c>FSDJump</c>,
/// <c>LoadGame</c>, <c>Died</c> or a <c>Docked</c> anywhere but a settlement's own pad. Also whether the
/// Commander is in a ground conflict zone: from a <c>DropshipDeploy</c>, or the first on-foot
/// <c>FactionKillBond</c> with a settlement held, until a retreat or taxi booking, <c>Died</c>,
/// <c>SupercruiseEntry</c>, <c>FSDJump</c>, <c>LoadGame</c> or an <c>Embark</c> into their own ship.
/// </summary>
public sealed class SettlementVisit
{
    private bool _afoot;

    public bool Held { get; private set; }

    public bool InGroundConflict { get; private set; }

    public void Observe(IEnumerable<JournalEvent> events)
    {
        foreach (var journalEvent in events)
        {
            switch (journalEvent.Kind)
            {
                case "ApproachSettlement":
                case "Docked" when journalEvent.String("StationType") == "OnFootSettlement":
                    Held = true;
                    break;
                case "SupercruiseEntry" or "FSDJump" or "LoadGame" or "Died":
                    Held = false;
                    InGroundConflict = false;
                    _afoot = false;
                    break;
                case "Docked":
                    Held = false;
                    break;
                case "DropshipDeploy":
                    _afoot = true;
                    InGroundConflict = true;
                    break;
                case "Disembark":
                    _afoot = !journalEvent.Bool("SRV");
                    break;
                case "Embark":
                    _afoot = false;
                    InGroundConflict &= journalEvent.Bool("Taxi");
                    break;
                case "LaunchSRV" or "DockSRV":
                    _afoot = false;
                    break;
                case "BookDropship" when journalEvent.Bool("Retreat"):
                case "BookTaxi":
                    InGroundConflict = false;
                    break;
                case "FactionKillBond" when _afoot && Held:
                    InGroundConflict = true;
                    break;
            }
        }
    }
}

/// <summary>
/// The ambience layer: which situation is playing and which track comes next (Phase 12, "Ambient
/// music"). Each folder keeps its own shuffle, so coming back to a folder continues its order. A track
/// <paramref name="rejected"/> says is queued for deletion is never drawn.
/// </summary>
public sealed class Ambience(Random? shuffle = null, Func<MusicTrack, bool>? rejected = null)
{
    private readonly Random _shuffle = shuffle ?? Random.Shared;

    private readonly Dictionary<string, Order> _orders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Null until the first <see cref="Enter"/>, so the first one is always a change.</summary>
    private string? _situation;

    /// <summary>What is being played for, which is not always what the Commander is doing.</summary>
    public string Situation => _situation ?? Situations.General;

    /// <summary>The folder the last <see cref="Next"/> drew from.</summary>
    public string Playing { get; private set; } = Situations.General;

    /// <summary>Points this at a situation, and says whether that is a change.</summary>
    public bool Enter(string situation)
    {
        if (string.Equals(_situation, situation, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _situation = situation;
        return true;
    }

    /// <summary>The folder the situation plays from: its own when it has tracks, otherwise general.</summary>
    public string Folder(CueLibrary library) =>
        library.Music(Situation).Count == 0 ? Situations.General : Situation;

    /// <summary>The next track, or null when there is nothing to play.</summary>
    public MusicTrack? Next(CueLibrary library)
    {
        var folder = Folder(library);
        Playing = folder;
        return Draw(folder, library);
    }

    /// <summary>The next track of a folder's shuffle, whatever the situation is, or null when it is empty.</summary>
    public MusicTrack? Draw(string folder, CueLibrary library)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            if (!_orders.TryGetValue(folder, out var order)
                || order.Next >= order.Tracks.Count
                || !ReferenceEquals(order.From, library))
            {
                order = Fill(folder, library);
                _orders[folder] = order;
            }

            while (order.Next < order.Tracks.Count)
            {
                var track = order.Tracks[order.Next++];

                if (rejected?.Invoke(track) != true)
                {
                    return track;
                }
            }
        }

        return null;
    }

    private Order Fill(string folder, CueLibrary library)
    {
        var tracks = library.Music(folder).ToList();

        // Fisher-Yates, in place.
        for (var i = tracks.Count - 1; i > 0; i--)
        {
            var j = _shuffle.Next(i + 1);
            (tracks[i], tracks[j]) = (tracks[j], tracks[i]);
        }

        return new Order(tracks, library);
    }

    /// <summary>A folder's shuffle, and the library it was drawn from.</summary>
    private sealed class Order(List<MusicTrack> tracks, CueLibrary from)
    {
        public List<MusicTrack> Tracks { get; } = tracks;

        public CueLibrary From { get; } = from;

        public int Next { get; set; }
    }
}
