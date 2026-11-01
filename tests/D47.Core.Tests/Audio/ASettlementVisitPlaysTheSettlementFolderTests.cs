using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// On foot or in the SRV at a settlement approached and not left, the settlement folder plays over
/// Elite's general tracks (#2038).
/// </summary>
public class ASettlementVisitPlaysTheSettlementFolderTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus OnFootOnPlanet = new()
    {
        Flags2 = (uint)(StatusFlags2.OnFoot | StatusFlags2.OnFootOnPlanet),
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static readonly GameStatus InSrv = Status(StatusFlags.InSrv);

    private static readonly GameStatus LandedInShip = Status(StatusFlags.Landed | StatusFlags.InMainShip);

    private static readonly GameStatus InShip = Status(StatusFlags.InMainShip);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private static readonly (string Folder, string Track)[] Folders =
    [
        (Situations.Settlement, "outpost"),
        (Situations.OnFoot, "walk"),
        (Situations.NormalSpace, "drift"),
        (Situations.Supercruise, "cruise"),
        ("combat-srv", "skirmish"),
        ("combat-dogfight", "dogfight"),
        ("galaxy-map", "chart"),
    ];

    private static readonly JournalEvent Approach = Event(
        """{ "timestamp":"2025-11-21T14:44:49Z", "event":"ApproachSettlement", "Name":"Brunt Leisure", "MarketID":3867298048, "BodyName":"Mackenzie's Legacy" }""");

    [Fact]
    public void OnFootAtASettlementPlaysItOverElitesOnFootTrack() =>
        Assert.Equal(Situations.Settlement, After([Approach], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void InTheSrvAtASettlementPlaysItOverElitesExplorationTrack() =>
        Assert.Equal(Situations.Settlement, After([Approach], InSrv, "Exploration"));

    [Theory]
    [InlineData("Combat_SRV", "combat-srv")]
    [InlineData("Combat_Dogfight", "combat-dogfight")]
    [InlineData("GalaxyMap", "galaxy-map")]
    public void ElitesSpecificTracksStillWinAtASettlement(string track, string folder) =>
        Assert.Equal(folder, After([Approach], InSrv, track));

    [Theory]
    [InlineData("SupercruiseEntry")]
    [InlineData("FSDJump")]
    [InlineData("LoadGame")]
    [InlineData("Died")]
    public void LeavingTheSettlementDropsIt(string kind)
    {
        var leave = Event($$"""{ "timestamp":"2025-11-21T14:51:26Z", "event":"{{kind}}" }""");

        Assert.Equal(Situations.OnFoot, After([Approach, leave], OnFootOnPlanet, "OnFoot"));
    }

    [Theory]
    [InlineData("CraterOutpost")]
    [InlineData("CraterPort")]
    [InlineData("PlanetaryConstructionDepot")]
    public void DockingAtAPlanetaryPortDropsIt(string stationType) =>
        Assert.Equal(Situations.OnFoot, After([Approach, Docked(stationType)], OnFootOnPlanet, "OnFoot"));

    [Fact]
    public void OnFootFromASettlementsOwnPadPlaysIt() =>
        Assert.Equal(Situations.Settlement, After([Approach, Docked("OnFootSettlement")], OnFootOnPlanet, "OnFoot"));

    private static JournalEvent Docked(string stationType) => Event(
        $$"""{ "timestamp":"2025-11-21T14:46:34Z", "event":"Docked", "StationName":"Brunt Leisure", "StationType":"{{stationType}}", "MarketID":3867298048 }""");

    [Fact]
    public void AnotherApproachHoldsTheSettlementAgain()
    {
        var leave = Event("""{ "timestamp":"2025-11-21T14:51:26Z", "event":"SupercruiseEntry" }""");

        Assert.Equal(Situations.Settlement, After([Approach, leave, Approach], OnFootOnPlanet, "OnFoot"));
    }

    [Fact]
    public void InTheShipLandedAtASettlementStaysNormalSpace() =>
        Assert.Equal(Situations.NormalSpace, After([Approach], LandedInShip, "Exploration"));

    [Fact]
    public void WithNoSettlementApproachedOnFootIsOnFoot() =>
        Assert.Equal(Situations.OnFoot, After([], OnFootOnPlanet, "OnFoot"));

    [Theory]
    [InlineData("NoTrack", Situations.NormalSpace)]
    [InlineData(null, Situations.NormalSpace)]
    [InlineData("Exploration", Situations.NormalSpace)]
    public void AnEmptySettlementFolderChangesNothingInTheSrv(string? track, string expected)
    {
        var library = Library(Folders.Where(folder => folder.Folder != Situations.Settlement).ToArray());

        Assert.Equal(expected, Situations.For(InSrv, track, library, atSettlement: true));
    }

    [Theory]
    [InlineData("NoTrack")]
    [InlineData("OnFoot")]
    public void AnEmptySettlementFolderChangesNothingOnFoot(string track)
    {
        var library = Library(Folders.Where(folder => folder.Folder != Situations.Settlement).ToArray());

        Assert.Equal(Situations.OnFoot, Situations.For(OnFootOnPlanet, track, library, atSettlement: true));
    }

    [Fact]
    public void TheSettlementFolderIsCreatedWithTheOthers() =>
        Assert.Contains(Situations.Settlement, Situations.All);

    [Fact]
    public void AmbientMusicPlaysTheSettlementFolderWhileOneIsHeld()
    {
        var sink = new RecordingAudioSink();
        var audio = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var library = Library(Folders);
        var music = new AmbientMusic(audio, () => library, new Random(7));
        audio.MusicFinished += music.TrackFinished;
        music.GameRunning(running: true);

        music.Follow(OnFootOnPlanet, "OnFoot", Start, atSettlement: true);

        Assert.Equal(Situations.Settlement, audio.MusicFolder);
        Assert.Contains("outpost", Assert.Single(sink.Started).Name, StringComparison.Ordinal);

        music.Follow(OnFootOnPlanet, "OnFoot", Start.AddSeconds(1), atSettlement: false);

        Assert.Equal(Situations.OnFoot, audio.MusicFolder);
    }

    /// <summary>
    /// A real settlement visit: landed, on foot, back in the ship, out in the SRV and on foot from it, then
    /// back aboard and away into supercruise. Status.json is not in the journal, so each event sets the
    /// status it implies.
    /// </summary>
    [Fact]
    public void AReplayedSettlementVisitPicksTheFolderFromLandingToSupercruise()
    {
        var library = Library(Folders);
        var visit = new SettlementVisit();
        var music = new EliteMusic();
        var status = InSupercruise;
        var folders = new List<string>();

        foreach (var line in EmbeddedFixture.Lines("music.Journal.2025-11-21T144449.01.log"))
        {
            var journalEvent = Event(line);
            visit.Observe([journalEvent]);
            music.Observe([journalEvent]);
            status = Implied(journalEvent) ?? status;
            folders.Add($"{journalEvent.Kind} {Situations.For(status, music.Track, library, visit.Held)}");
        }

        Assert.Equal(
            [
                "ApproachSettlement supercruise",
                "SupercruiseExit normal-space",
                "Music normal-space",
                "Touchdown normal-space",
                "Disembark settlement",
                "Music settlement",
                "Embark on-foot",
                "Music normal-space",
                "LaunchSRV settlement",
                "Disembark settlement",
                "Music settlement",
                "Embark settlement",
                "Music combat-srv",
                "Music settlement",
                "DockSRV normal-space",
                "Liftoff normal-space",
                "Music galaxy-map",
                "Music normal-space",
                "StartJump normal-space",
                "SupercruiseEntry supercruise",
            ],
            folders);
        Assert.False(visit.Held);
    }

    private static GameStatus? Implied(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "SupercruiseExit" or "Liftoff" => InShip,
        "Touchdown" or "DockSRV" => LandedInShip,
        "Disembark" => OnFootOnPlanet,
        "LaunchSRV" => InSrv,
        "Embark" => journalEvent.Bool("SRV") ? InSrv : LandedInShip,
        "SupercruiseEntry" => InSupercruise,
        _ => null,
    };

    private static string After(JournalEvent[] events, GameStatus status, string track)
    {
        var visit = new SettlementVisit();
        visit.Observe(events);
        return Situations.For(status, track, Library(Folders), visit.Held);
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
