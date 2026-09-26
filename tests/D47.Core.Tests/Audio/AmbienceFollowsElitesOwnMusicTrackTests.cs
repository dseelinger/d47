using D47.Core.Audio;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AmbienceFollowsElitesOwnMusicTrackTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-elite-music-tests",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        // A temp folder that will not delete is not a test failure.
        }
    }

    private static readonly GameStatus InSupercruise = new()
    {
        Flags = StatusFlags.Supercruise | StatusFlags.InMainShip,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    [Theory]
    [InlineData("Starport", "docked")]
    [InlineData("Exploration", "normal-space")]
    [InlineData("Supercruise", "supercruise")]
    [InlineData("OnFoot", "on-foot")]
    [InlineData("MainMenu", "main-menu")]
    [InlineData("DockingComputer", "docking-computer")]
    [InlineData("GalaxyMap", "galaxy-map")]
    [InlineData("Combat_Dogfight", "combat-dogfight")]
    [InlineData("FleetCarrier_Managment", "fleet-carrier")]
    [InlineData("SystemMap", "system-map")]
    [InlineData("SystemAndSurfaceScanner", "scanner")]
    [InlineData("Combat_LargeDogFight", "combat-large")]
    [InlineData("CombatLargeDogFight", "combat-large")]
    [InlineData("Squadrons", "squadrons")]
    [InlineData("DestinationFromSupercruise", "arrival-from-supercruise")]
    [InlineData("GalacticPowers", "powerplay")]
    [InlineData("GuardianSites", "guardian-sites")]
    [InlineData("DestinationFromHyperspace", "arrival-from-hyperspace")]
    [InlineData("Combat_SRV", "combat-srv")]
    [InlineData("Unknown_Exploration", "unknown-exploration")]
    [InlineData("Codex", "codex")]
    [InlineData("CQCMenu", "cqc-menu")]
    [InlineData("Interdiction", "interdiction")]
    [InlineData("Lifeform_FogCloud", "fog-cloud")]
    [InlineData("CQC", "cqc")]
    [InlineData("Combat_Unknown", "combat-unknown")]
    [InlineData("Unknown_Encounter", "unknown-encounter")]
    [InlineData("CapitalShip", "capital-ship")]
    [InlineData("Unknown_Settlement", "unknown-settlement")]
    public void EachEliteTrackHasItsFolder(string track, string folder)
    {
        Assert.Equal(folder, Situations.ByTrack[track]);
    }

    [Fact]
    public void TheTableHasNoRowsTheTestDoesNotName()
    {
        Assert.Equal(29, Situations.ByTrack.Count);
    }

    [Fact]
    public void ACombatTrackWithFilesPlaysCombatOverSupercruise()
    {
        var library = Library(("combat-dogfight", "fight"), ("supercruise", "cruise"));

        Assert.Equal("combat-dogfight", Situations.For(InSupercruise, FixtureTrack(), library));
    }

    [Fact]
    public void AnEmptyCombatFolderLeavesSupercruisePlaying()
    {
        var library = Library(("supercruise", "cruise"));

        Assert.Equal(Situations.Supercruise, Situations.For(InSupercruise, FixtureTrack(), library));
    }

    [Theory]
    [InlineData("NoTrack")]
    [InlineData("NoInGameMusic")]
    [InlineData("Something_New")]
    [InlineData(null)]
    public void ATrackWithNoFolderResolvesThroughStatus(string? track)
    {
        var library = Library(("general", "hum"));

        Assert.Equal(Situations.Supercruise, Situations.For(InSupercruise, track, library));
    }

    [Fact]
    public void APollWithNoMusicEventKeepsTheTrack()
    {
        var music = new EliteMusic();
        music.Observe(Read());

        music.Observe([]);

        Assert.Equal("Combat_Dogfight", music.Track);
    }

    [Fact]
    public void FirstRunMakesEveryMusicFolderEmpty()
    {
        var paths = new AppPaths(_root);
        paths.EnsureCreated();

        foreach (var folder in Situations.All)
        {
            var path = Path.Combine(paths.Audio, FolderAudioSource.MusicFolder, folder);

            Assert.True(Directory.Exists(path), folder);
            Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        }

        Assert.Subset(Situations.All.ToHashSet(), Situations.ByTrack.Values.ToHashSet());
    }

    private static string? FixtureTrack()
    {
        var music = new EliteMusic();
        music.Observe(Read());
        return music.Track;
    }

    private static List<JournalEvent> Read()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        var file = Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary."),
            "tests",
            "fixtures",
            "music",
            "Journal.2026-09-26T120000.01.log");

        var events = new List<JournalEvent>();

        foreach (var line in File.ReadLines(file))
        {
            if (JournalEvent.TryParse(line, NullLogger.Instance, out var parsed))
            {
                events.Add(parsed!);
            }
        }

        return events;
    }

    private CueLibrary Library(params (string Folder, string Track)[] tracks)
    {
        var drops = Path.Combine(_root, "drops");

        foreach (var (folder, track) in tracks)
        {
            WriteWav(Path.Combine(drops, FolderAudioSource.MusicFolder, folder, $"{track}.wav"));
        }

        Directory.CreateDirectory(Path.Combine(drops, FolderAudioSource.MusicFolder));

        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(drops, NullLogger<FolderAudioSource>.Instance));

        Assert.Empty(library.Skipped);

        return library;
    }

    private static void WriteWav(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        const int Samples = 240;
        const int Data = Samples * 2;

        using var file = File.Create(path);
        using var write = new BinaryWriter(file);

        write.Write("RIFF"u8.ToArray());
        write.Write(36 + Data);
        write.Write("WAVE"u8.ToArray());
        write.Write("fmt "u8.ToArray());
        write.Write(16);
        write.Write((short)1);
        write.Write((short)1);
        write.Write(AudioFormat.Standard.SampleRate);
        write.Write(AudioFormat.Standard.SampleRate * 2);
        write.Write((short)2);
        write.Write((short)16);
        write.Write("data"u8.ToArray());
        write.Write(Data);
        write.Write(new byte[Data]);
    }
}
