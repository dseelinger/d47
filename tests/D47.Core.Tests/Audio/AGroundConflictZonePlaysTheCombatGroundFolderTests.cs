using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// On foot in a ground conflict zone, the combat-ground folder plays over the settlement folder and Elite's
/// general tracks (#2039).
/// </summary>
public class AGroundConflictZonePlaysTheCombatGroundFolderTests
{
    private static readonly GameStatus OnFootOnPlanet = new()
    {
        Flags2 = (uint)(StatusFlags2.OnFoot | StatusFlags2.OnFootOnPlanet),
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static readonly GameStatus InShip = Status(StatusFlags.InMainShip);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private static readonly (string Folder, string Track)[] Folders =
    [
        (Situations.CombatGround, "firefight"),
        (Situations.Settlement, "outpost"),
        (Situations.OnFoot, "walk"),
        (Situations.NormalSpace, "drift"),
        (Situations.Supercruise, "cruise"),
        ("combat-srv", "skirmish"),
        ("combat-thargoid-ground", "swarm"),
    ];

    private static readonly JournalEvent Approach = Event(
        """{ "timestamp":"2025-10-13T15:40:45Z", "event":"ApproachSettlement", "Name":"Lim Horticultural Range", "MarketID":3830859008 }""");

    private static readonly JournalEvent Deploy = Event(
        """{ "timestamp":"2025-10-13T15:41:56Z", "event":"DropshipDeploy", "StarSystem":"Cakutsi", "OnStation":false, "OnPlanet":true }""");

    private static readonly JournalEvent Disembark = Event(
        """{ "timestamp":"2025-10-13T15:42:00Z", "event":"Disembark", "SRV":false, "Taxi":false, "OnPlanet":true }""");

    private static readonly JournalEvent KillBond = Event(
        """{ "timestamp":"2025-10-13T15:44:48Z", "event":"FactionKillBond", "Reward":100, "VictimFaction":"Some Guild" }""");

    [Fact]
    public void ADropshipDeployThenOnFootPlaysItOverElitesOnFootTrack() =>
        Assert.Equal(Situations.CombatGround, After([Approach, Deploy], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void ItWinsOverTheSettlementFolderAndElitesExplorationTrack() =>
        Assert.Equal(Situations.CombatGround, After([Approach, Deploy], OnFootOnPlanet, "Exploration"));

    [Fact]
    public void ARedeployKeepsIt() =>
        Assert.Equal(Situations.CombatGround, After([Approach, Deploy, Deploy], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void ARetreatBookingClosesIt()
    {
        var retreat = Event("""{ "timestamp":"2025-10-13T16:04:43Z", "event":"BookDropship", "Retreat":true, "Cost":100 }""");

        Assert.Equal(Situations.Settlement, After([Approach, Deploy, retreat], OnFootOnPlanet, "OnFoot"));
    }

    [Fact]
    public void ABookingToAConflictZoneDoesNotOpenOrCloseIt()
    {
        var book = Event("""{ "timestamp":"2025-10-13T15:30:00Z", "event":"BookDropship", "Retreat":false, "Cost":100 }""");

        Assert.Equal(Situations.CombatGround, After([Approach, Deploy, book], OnFootOnPlanet, "OnFoot"));
        Assert.Equal(Situations.Settlement, After([Approach, book], OnFootOnPlanet, "OnFoot"));
    }

    [Theory]
    [InlineData("SupercruiseEntry")]
    [InlineData("FSDJump")]
    [InlineData("LoadGame")]
    [InlineData("Died")]
    public void LeavingTheZoneClosesIt(string kind)
    {
        var leave = Event($$"""{ "timestamp":"2025-10-13T16:08:24Z", "event":"{{kind}}" }""");

        Assert.Equal(Situations.OnFoot, After([Approach, Deploy, leave], OnFootOnPlanet, "OnFoot"));
    }

    [Fact]
    public void BoardingTheirOwnShipClosesIt()
    {
        var embark = Event("""{ "timestamp":"2025-10-13T16:07:26Z", "event":"Embark", "SRV":false, "Taxi":false }""");

        Assert.Equal(Situations.Settlement, After([Approach, Deploy, embark], OnFootOnPlanet, "OnFoot"));
    }

    [Fact]
    public void BoardingTheDropshipToLeaveKeepsItUntilSupercruise()
    {
        var embark = Event("""{ "timestamp":"2025-10-13T16:07:26Z", "event":"Embark", "SRV":false, "Taxi":true }""");

        Assert.Equal(Situations.CombatGround, After([Approach, Deploy, embark], OnFootOnPlanet, "OnFoot"));
        Assert.Equal(Situations.NormalSpace, After([Approach, Deploy, embark], InShip, "Exploration"));
    }

    [Fact]
    public void AnOnFootKillBondAtASettlementOpensItWithNoDropship() =>
        Assert.Equal(Situations.CombatGround, After([Approach, Disembark, KillBond], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void AKillBondFromTheShipDoesNotOpenIt() =>
        Assert.Equal(Situations.Settlement, After([Approach, KillBond], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void AKillBondOnFootWithNoSettlementHeldDoesNotOpenIt() =>
        Assert.Equal(Situations.OnFoot, After([Disembark, KillBond], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void InTheShipItIsNotPlayed() =>
        Assert.Equal(Situations.NormalSpace, After([Approach, Deploy], InShip, "Exploration"));

    [Theory]
    [InlineData("Thargoid_GroundCombat", "combat-thargoid-ground")]
    [InlineData("Combat_SRV", "combat-srv")]
    public void ElitesSpecificTracksStillWin(string track, string folder) =>
        Assert.Equal(folder, After([Approach, Deploy], OnFootOnPlanet, track));

    [Theory]
    [InlineData("NoTrack")]
    [InlineData("OnFoot")]
    public void AnEmptyFolderChangesNothing(string track)
    {
        var library = Library(Folders.Where(folder => folder.Folder != Situations.CombatGround).ToArray());

        Assert.Equal(Situations.Settlement, Situations.For(OnFootOnPlanet, track, library, true, true));
    }

    [Fact]
    public void TheFolderIsCreatedWithTheOthers() =>
        Assert.Contains(Situations.CombatGround, Situations.All);

    /// <summary>
    /// A real dropship trip, trimmed: the settlement approach, the deploy, kill bonds, a redeploy, the
    /// retreat booking and the dropship's climb into supercruise. Status.json is not in the journal, so each
    /// event sets the status it implies; the Commander is still on foot when the retreat is booked.
    /// </summary>
    [Fact]
    public void AReplayedDropshipTripPicksTheFolderFromTheDeployToTheRetreat()
    {
        var library = Library(Folders);
        var visit = new SettlementVisit();
        var music = new EliteMusic();
        var status = InSupercruise;
        var folders = new List<string>();

        foreach (var line in EmbeddedFixture.Lines("music.Journal.2025-10-13T182003.01.log"))
        {
            var journalEvent = Event(line);
            visit.Observe([journalEvent]);
            music.Observe([journalEvent]);
            status = Implied(journalEvent) ?? status;
            folders.Add(
                $"{journalEvent.Kind} {Situations.For(status, music.Track, library, visit.Held, visit.InGroundConflict)}");
        }

        Assert.Equal(
            [
                "ApproachSettlement supercruise",
                "SupercruiseExit normal-space",
                "Music normal-space",
                "DropshipDeploy combat-ground",
                "Music combat-ground",
                "FactionKillBond combat-ground",
                "FactionKillBond combat-ground",
                "FactionKillBond combat-ground",
                "DropshipDeploy combat-ground",
                "BookDropship settlement",
                "Music combat-srv",
                "Music settlement",
                "SupercruiseEntry supercruise",
                "Music supercruise",
            ],
            folders);
        Assert.False(visit.InGroundConflict);
    }

    [Fact]
    public void AmbientMusicPlaysTheFolderWhileTheZoneIsHeld()
    {
        var sink = new RecordingAudioSink();
        var audio = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var library = Library(Folders);
        var music = new AmbientMusic(audio, () => library, new Random(7));
        audio.MusicFinished += music.TrackFinished;
        music.GameRunning(running: true);

        music.Follow(OnFootOnPlanet, "OnFoot", default, atSettlement: true, inGroundConflict: true);

        Assert.Equal(Situations.CombatGround, audio.MusicFolder);
        Assert.Contains("firefight", Assert.Single(sink.Started).Name, StringComparison.Ordinal);

        music.Follow(OnFootOnPlanet, "OnFoot", default, atSettlement: true, inGroundConflict: false);

        Assert.Equal(Situations.Settlement, audio.MusicFolder);
    }

    private static GameStatus? Implied(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "SupercruiseExit" or "Embark" => InShip,
        "DropshipDeploy" => OnFootOnPlanet,
        "SupercruiseEntry" => InSupercruise,
        _ => null,
    };

    private static string After(JournalEvent[] events, GameStatus status, string track)
    {
        var visit = new SettlementVisit();
        visit.Observe(events);
        return Situations.For(status, track, Library(Folders), visit.Held, visit.InGroundConflict);
    }

    private static CueLibrary Library((string Folder, string Track)[] tracks)
    {
        const string root = @"C:\d47-music";
        var files = new MemoryFileSystem();

        foreach (var (folder, track) in tracks)
        {
            files.WriteBytes(
                Path.Combine(root, FolderAudioSource.MusicFolder, folder, $"{track}.wav"),
                WavWriter.ToBytes(new byte[480], AudioFormat.Standard));
        }

        return CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(root, files, NullLogger<FolderAudioSource>.Instance));
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };
}
