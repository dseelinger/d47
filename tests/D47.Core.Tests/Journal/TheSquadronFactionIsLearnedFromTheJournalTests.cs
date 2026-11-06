using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class TheSquadronFactionIsLearnedFromTheJournalTests
{
    private const string Fid = "F1";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Line(string time, string body) => "{\"timestamp\":\"2026-10-" + time + "Z\"," + body + "}";

    private static string Login(string time) =>
        Line(time, "\"event\":\"LoadGame\",\"FID\":\"" + Fid + "\",\"Commander\":\"Jameson\"");

    private static string Startup(string time, long id) =>
        Line(time, "\"event\":\"SquadronStartup\",\"SquadronName\":\"Greybeards\",\"SquadronID\":" + id + ",\"CurrentRank\":1");

    private static string Jump(string time, string faction, string kind = "FSDJump") =>
        Line(time, "\"event\":\"" + kind + "\",\"StarSystem\":\"Hutton\",\"Factions\":[{\"Name\":\"Other\",\"SquadronFaction\":false},{\"Name\":\""
            + faction + "\",\"SquadronFaction\":true}]");

    private static SquadronFactionSightings Fold(params string[] lines)
    {
        var squadron = SquadronState.None;
        var sightings = SquadronFactionSightings.Empty;

        foreach (var line in lines)
        {
            var parsed = Event(line);
            squadron = squadron.Apply(parsed);
            sightings = sightings.Apply(parsed, squadron);
        }

        return sightings;
    }

    private static SquadronState Member(long id) => SquadronState.None.Apply(Event(Startup("01T00:00:00", id)));

    [Fact]
    public void TheNewestSightingWinsAfterAChangeOfFaction()
    {
        var sightings = Fold(
            Login("01T09:00:00"),
            Startup("01T09:00:01", 7),
            Jump("01T09:05:00", "Kestrel Reach Compact"),
            Jump("02T09:05:00", "Hutton Truckers", "Location"),
            Jump("03T09:05:00", "Kestrel Reach Compact", "CarrierJump"));

        Assert.Equal("Kestrel Reach Compact", sightings.For("7"));

        var changed = Fold(Login("01T09:00:00"), Startup("01T09:00:01", 7), Jump("01T09:05:00", "Kestrel Reach Compact"), Jump("02T09:05:00", "Hutton Truckers"));

        Assert.Equal("Hutton Truckers", changed.For("7"));
    }

    [Fact]
    public void ASightingUnderOneSquadronDoesNotAnswerForAnother()
    {
        var sightings = Fold(Login("01T09:00:00"), Startup("01T09:00:01", 7), Jump("01T09:05:00", "Kestrel Reach Compact"));

        Assert.Null(sightings.For("8"));
        Assert.Null(SquadronFactionResolver.Resolve(sightings, Member(8), null));
    }

    [Fact]
    public void TheStoredNameAnswersOnlyWhenThereIsNoSighting()
    {
        var sightings = Fold(Login("01T09:00:00"), Startup("01T09:00:01", 7), Jump("01T09:05:00", "Kestrel Reach Compact"));

        Assert.Equal("Kestrel Reach Compact", SquadronFactionResolver.Resolve(sightings, Member(7), "Hutton Truckers"));
        Assert.Equal("Hutton Truckers", SquadronFactionResolver.Resolve(SquadronFactionSightings.Empty, Member(7), "Hutton Truckers"));
        Assert.Null(SquadronFactionResolver.Resolve(SquadronFactionSightings.Empty, Member(7), null));
        Assert.Null(SquadronFactionResolver.Resolve(sightings, SquadronState.None, "Hutton Truckers"));
    }

    [Fact]
    public void AFlaggedJumpBeforeSquadronStartupRecordsNothing()
    {
        var sightings = Fold(Login("01T09:00:00"), Jump("01T09:00:01", "Kestrel Reach Compact"), Startup("01T09:00:02", 7));

        Assert.False(sightings.IsKnown);
    }

    [Fact]
    public void ASightingWithNoSquadronIdIsKeyedByTheName()
    {
        var sightings = Fold(
            Login("01T09:00:00"),
            Line("01T09:00:01", "\"event\":\"SquadronStartup\",\"SquadronName\":\"Greybeards\",\"CurrentRank\":1"),
            Jump("01T09:05:00", "Kestrel Reach Compact"));

        Assert.Equal("Kestrel Reach Compact", sightings.For("Greybeards"));
    }

    [Fact]
    public void RestoredSightingsKeepTheNewerOneAndAddTheRest()
    {
        var live = Fold(Login("03T09:00:00"), Startup("03T09:00:01", 7), Jump("03T09:05:00", "Hutton Truckers"));
        var older = Fold(Login("01T09:00:00"), Startup("01T09:00:01", 7), Jump("01T09:05:00", "Kestrel Reach Compact"));
        var other = Fold(Login("01T09:00:00"), Startup("01T09:00:01", 9), Jump("01T09:05:00", "Alpha Mining"));

        var merged = live.With(older).With(other);

        Assert.Equal("Hutton Truckers", merged.For("7"));
        Assert.Equal("Alpha Mining", merged.For("9"));
        Assert.Equal("Hutton Truckers", older.With(live).For("7"));
    }

    [Fact]
    public void TheHistoryBackfillReadsEveryFileNotAWindow()
    {
        var files = new MemoryFileSystem();
        files.WriteLines("j/Journal.2026-01-01T000000.01.log", [Login("01T09:00:00"), Startup("01T09:00:01", 7), Jump("01T09:05:00", "Kestrel Reach Compact")]);

        for (var day = 1; day <= 28; day++)
        {
            files.WriteLines($"j/Journal.2026-02-{day:00}T000000.01.log", [Login("01T09:00:00"), Startup("01T09:00:01", 7)]);
        }

        var paths = files.Enumerate("j", "*.log").OrderBy(path => path, StringComparer.Ordinal).ToList();

        var found = SquadronFactionBackfill.FromHistory(files, paths, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal("Kestrel Reach Compact", found[Fid].For("7"));
    }

    [Fact]
    public void ALiveFlaggedEventUpdatesTheGameState()
    {
        var store = new GameStateStore();

        foreach (var line in new[] { Login("01T09:00:00"), Startup("01T09:00:01", 7), Jump("01T09:05:00", "Kestrel Reach Compact") })
        {
            store.Apply(Event(line));
        }

        var state = store.Active;

        Assert.NotNull(state);
        Assert.Equal("Kestrel Reach Compact", state.SquadronFactions.For("7"));
    }
}
