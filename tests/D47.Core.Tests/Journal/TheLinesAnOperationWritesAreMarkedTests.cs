using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The journal lines an Operation writes, in the order Elite writes them.</summary>
public class TheLinesAnOperationWritesAreMarkedTests
{
    private const string Root = @"C:\d47-test\journals";

    private const string Fileheader = """{ "timestamp":"2026-10-20T18:00:00Z", "event":"Fileheader", "part":1, "gameversion":"4.4.1.0" }""";
    private const string Commander = """{ "timestamp":"2026-10-20T18:00:05Z", "event":"Commander", "FID":"F1", "Name":"Fixture" }""";
    private const string Rank = """{ "timestamp":"2026-10-20T18:00:05Z", "event":"Rank", "Combat":3 }""";
    private const string SessionLoad = """{ "timestamp":"2026-10-20T18:00:05Z", "event":"LoadGame", "FID":"F1", "Commander":"Fixture" }""";
    private const string SessionMode = """{ "timestamp":"2026-10-20T18:00:05Z", "event":"GameModeChange", "GameMode":"MainGame" }""";
    private const string Location = """{ "timestamp":"2026-10-20T18:00:10Z", "event":"Location", "StarSystem":"Sol" }""";

    private const string EntryCommander = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"Commander", "FID":"F1", "Name":"Fixture" }""";
    private const string EntryLoad = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Fixture" }""";
    private const string EntryMode = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"GameModeChange", "GameMode":"Operation" }""";
    private const string Bounty = """{ "timestamp":"2026-10-20T18:10:00Z", "event":"Bounty", "TotalReward":1000 }""";
    private const string ExitCommander = """{ "timestamp":"2026-10-20T18:40:00Z", "event":"Commander", "FID":"F1", "Name":"Fixture" }""";
    private const string ExitLoad = """{ "timestamp":"2026-10-20T18:40:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Fixture" }""";
    private const string ExitMode = """{ "timestamp":"2026-10-20T18:40:00Z", "event":"GameModeChange", "GameMode":"MainGame" }""";
    private const string ExitLocation = """{ "timestamp":"2026-10-20T18:40:05Z", "event":"Location", "StarSystem":"Sol" }""";

    private const string Statistics = """{ "timestamp":"2026-10-20T18:00:05Z", "event":"Statistics" }""";
    private const string Shutdown = """{ "timestamp":"2026-10-20T18:20:00Z", "event":"Shutdown" }""";

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void ASessionStartMarksNothingAndItsLoadGameStartsASession()
    {
        var marked = Marked(Fileheader, Commander, Rank, SessionLoad, SessionMode, Location);

        Assert.Equal(["Fileheader", "Commander", "Rank", "LoadGame", "GameModeChange", "Location"], marked.Select(e => e.Kind));
        Assert.All(marked, e => Assert.False(e.InOperation));
        Assert.True(marked.Single(e => e.Kind == "LoadGame").StartsSession);
    }

    [Fact]
    public void AnEntryAndAnExitAreMarkedFromTheEntryLoadGameThroughTheExitModeChange()
    {
        var marked = Marked(
            Fileheader, Commander, SessionLoad, SessionMode, Location,
            EntryCommander, EntryLoad, EntryMode, Bounty,
            ExitCommander, ExitLoad, ExitMode, ExitLocation);

        Assert.Equal(
            [false, false, false, false, false, false, true, true, true, true, true, true, false],
            marked.Select(e => e.InOperation));
        Assert.Equal(1, marked.Count(e => e.StartsSession));
    }

    [Fact]
    public void AnEntryLoadGameEndingOnePollIsMarkedByTheModeChangeInTheNext()
    {
        var marker = new OperationMarker();

        var first = marker.Next(Events(Fileheader, SessionLoad, SessionMode, EntryCommander, EntryLoad));
        var quiet = marker.Next([]);
        var second = marker.Next(Events(EntryMode, Bounty));

        Assert.Equal(["Fileheader", "LoadGame", "GameModeChange", "Commander"], first.Select(e => e.Kind));
        Assert.Empty(quiet);
        Assert.Equal(["LoadGame", "GameModeChange", "Bounty"], second.Select(e => e.Kind));
        Assert.All(second, e => Assert.True(e.InOperation));
    }

    [Fact]
    public void AJournalWithNoModeChangeStartsASessionAtItsLoadGame()
    {
        var marked = Marked(Fileheader, Commander, SessionLoad, Statistics, Location);

        Assert.All(marked, e => Assert.False(e.InOperation));
        Assert.True(marked.Single(e => e.Kind == "LoadGame").StartsSession);
    }

    [Fact]
    public void AShutdownInsideAnOperationClosesIt()
    {
        var marked = Marked(SessionLoad, SessionMode, EntryLoad, EntryMode, Bounty, Shutdown, Fileheader, SessionLoad, SessionMode, Location);

        Assert.Equal(
            [false, false, true, true, true, false, false, false, false, false],
            marked.Select(e => e.InOperation));
    }

    [Fact]
    public void AFileheaderInsideAnOperationClosesIt()
    {
        var marked = Marked(EntryLoad, EntryMode, Bounty, Fileheader, Location);

        Assert.Equal([true, true, true, false, false], marked.Select(e => e.InOperation));
    }

    [Fact]
    public void ALoadGameEndingAWholeFileIsReleasedUnmarked()
    {
        var marked = Marked(Fileheader, Commander, SessionLoad);

        Assert.Equal("LoadGame", marked[^1].Kind);
        Assert.True(marked[^1].StartsSession);
    }

    [Fact]
    public void AHeldLoadGameIsReleasedAheadOfTheNewerFileAtASwitch()
    {
        Write("Journal.2026-10-20T180000.01.log", Fileheader, Commander, SessionLoad);

        var spine = new JournalSpine(Root, _files, new GameStateStore(), NullLoggerFactory.Instance);
        var first = spine.Poll();

        Write("Journal.2026-10-20T190000.01.log", Fileheader, Location);
        var second = spine.Poll();

        Assert.Equal(["Fileheader", "Commander"], first.Select(e => e.Kind));
        Assert.Equal(["LoadGame", "Fileheader", "Location"], second.Select(e => e.Kind));
        Assert.True(second[0].StartsSession);
        Assert.Equal(second.Count, spine.Receipts.Count);
    }

    [Fact]
    public void APrimingPollReleasesItsHeldLoadGameAndFoldsIt()
    {
        Write("Journal.2026-10-20T180000.01.log", Fileheader, Commander, SessionLoad);

        var gameState = new GameStateStore();
        var spine = new JournalSpine(Root, _files, gameState, NullLoggerFactory.Instance);
        var events = spine.Poll(priming: true);

        Assert.Equal(["Fileheader", "Commander", "LoadGame"], events.Select(e => e.Kind));
        Assert.True(events[^1].StartsSession);
        Assert.Equal(events.Count, spine.Receipts.Count);
    }

    private static List<JournalEvent> Marked(params string[] lines) => [.. OperationMarker.Mark(Events(lines))];

    private static List<JournalEvent> Events(params string[] lines) =>
        [.. lines.Select(line => JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(line))];

    private void Write(string fileName, params string[] lines) =>
        _files.WriteText(Path.Combine(Root, fileName), string.Join("\n", lines) + "\n");
}
