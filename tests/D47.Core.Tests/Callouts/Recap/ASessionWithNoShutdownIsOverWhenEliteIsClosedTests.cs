using D47.Core.Callouts;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

/// <summary>A session whose journal stops without a Shutdown is recapped as finished once the game is gone.</summary>
public sealed class ASessionWithNoShutdownIsOverWhenEliteIsClosedTests
{
    private static readonly DateTimeOffset Morning = new(3311, 4, 2, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Afternoon = Morning.AddHours(5);

    private readonly JournalCorpus _corpus = new();

    private void FlyTheMorningWithNoShutdown() =>
        _corpus.Journal(
            Morning,
            JournalCorpus.Event(Morning, "Fileheader", "\"part\":1"),
            JournalCorpus.LoadGame(Morning.AddMinutes(1)),
            JournalCorpus.Jump(Morning.AddMinutes(10), "Deciat", 8.09),
            JournalCorpus.Event(
                Morning.AddMinutes(30),
                "Docked",
                "\"StationName\":\"Garay Terminal\",\"StarSystem\":\"Deciat\""));

    private void OpenAnotherJournal(int part) =>
        _corpus.Journal(Afternoon.AddMinutes(-5), JournalCorpus.Event(Afternoon.AddMinutes(-5), "Fileheader", $"\"part\":{part}"));

    private string? Recap(bool gameRunning) =>
        RecapCallout.ComposeAt(_corpus.FileSystem, Afternoon, _corpus.Files, NullLoggerFactory.Instance, gameRunning);

    [Fact]
    public void WithEliteClosedItOpensWithLastSession()
    {
        FlyTheMorningWithNoShutdown();

        var line = Recap(gameRunning: false);

        Assert.NotNull(line);
        Assert.StartsWith("Last session", line, StringComparison.Ordinal);
        Assert.Contains("Garay Terminal, Deciat", line, StringComparison.Ordinal);
    }

    [Fact]
    public void WithEliteRunningMidSessionItOpensWithEarlierThisSession()
    {
        FlyTheMorningWithNoShutdown();

        var line = Recap(gameRunning: true);

        Assert.NotNull(line);
        Assert.StartsWith("Earlier this session", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewJournalWithNoLoginYetEndsTheSessionBeforeIt()
    {
        FlyTheMorningWithNoShutdown();
        OpenAnotherJournal(part: 1);

        var line = Recap(gameRunning: true);

        Assert.NotNull(line);
        Assert.StartsWith("Last session", line, StringComparison.Ordinal);
        Assert.Contains("Garay Terminal, Deciat", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ALaterPartOfTheSameJournalKeepsTheSessionRunning()
    {
        FlyTheMorningWithNoShutdown();
        OpenAnotherJournal(part: 2);

        var line = Recap(gameRunning: true);

        Assert.NotNull(line);
        Assert.StartsWith("Earlier this session", line, StringComparison.Ordinal);
    }
}
