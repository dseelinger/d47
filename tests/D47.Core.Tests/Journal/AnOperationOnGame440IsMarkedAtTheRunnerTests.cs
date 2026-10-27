using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Game 4.4.0 writes no GameModeChange for an Operation; the docked Location at the Runner is the entry.</summary>
public class AnOperationOnGame440IsMarkedAtTheRunnerTests
{
    private const string Fileheader = """{ "timestamp":"2026-10-20T18:00:00Z", "event":"Fileheader", "gameversion":"4.4.0.2" }""";
    private const string Load = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Fixture", "GameMode":"Open" }""";
    private const string Runner = """{ "timestamp":"2026-10-20T18:05:10Z", "event":"Location", "Docked":true, "StationName":"$Operations_Runner_Name:#index=1;", "StarSystem":"Fixture" }""";
    private const string Elsewhere = """{ "timestamp":"2026-10-20T18:00:10Z", "event":"Location", "Docked":true, "StationName":"Fixture Port", "StarSystem":"Fixture" }""";
    private const string Missions = """{ "timestamp":"2026-10-20T18:05:11Z", "event":"Missions", "Active":[] }""";
    private const string Bounty = """{ "timestamp":"2026-10-20T18:10:00Z", "event":"Bounty", "TotalReward":1000 }""";
    private const string EntryMode = """{ "timestamp":"2026-10-20T18:05:00Z", "event":"GameModeChange", "GameMode":"Operation" }""";
    private const string ExitMode = """{ "timestamp":"2026-10-20T18:40:00Z", "event":"GameModeChange", "GameMode":"MainGame" }""";

    [Fact]
    public void ARunIsMarkedFromTheRunnerLocationAndItsClosingLoadGameIsNot()
    {
        var marked = Marked(Fileheader, Load, Elsewhere, Load, Runner, Missions, Bounty, Load);

        Assert.Equal(
            [false, false, false, false, true, true, true, false],
            marked.Select(e => e.InOperation));
        Assert.True(marked[^1].StartsSession);
    }

    [Fact]
    public void TwoRunsBackToBackAreBothMarked()
    {
        var marked = Marked(Fileheader, Load, Runner, Missions, Bounty, Load, Runner, Missions, Bounty, Load);

        Assert.Equal(
            [false, false, true, true, true, false, true, true, true, false],
            marked.Select(e => e.InOperation));
        Assert.Equal(3, marked.Count(e => e.StartsSession));
    }

    [Fact]
    public void ADockedLocationElsewhereOpensNothing()
    {
        var marked = Marked(Fileheader, Load, Elsewhere, Bounty);

        Assert.All(marked, e => Assert.False(e.InOperation));
    }

    [Fact]
    public void ARunnerLocationAfterTheModeChangeIsMarkedAsBefore()
    {
        var marked = Marked(Fileheader, Load, EntryMode, Runner, Bounty, Load, ExitMode, Elsewhere);

        Assert.Equal(
            [false, true, true, true, true, true, true, false],
            marked.Select(e => e.InOperation));
    }

    private static List<JournalEvent> Marked(params string[] lines) =>
        [.. OperationMarker.Mark(lines.Select(line => JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(line)))];
}
