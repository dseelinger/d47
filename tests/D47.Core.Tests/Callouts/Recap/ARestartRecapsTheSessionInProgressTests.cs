using D47.Core.Callouts;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

/// <summary>When d47 restarts mid-session, the recap covers the flying since the login.</summary>
public sealed class ARestartRecapsTheSessionInProgressTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Restart = Evening.AddHours(2);

    private readonly JournalCorpus _corpus = new();

    private string? RecapAtRestart() => RecapCallout.ComposeAt(_corpus.FileSystem, Restart, _corpus.Files, NullLoggerFactory.Instance);

    [Fact]
    public void JumpsAndADockBeforeTheRestartOpenWithEarlierThisSession()
    {
        _corpus.Journal(
            Evening.AddDays(-1),
            JournalCorpus.LoadGame(Evening.AddDays(-1)),
            JournalCorpus.Jump(Evening.AddDays(-1).AddMinutes(10), "Sol", 12));
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Jump(Evening.AddMinutes(10), "Deciat", 8.09),
            JournalCorpus.Event(
                Evening.AddMinutes(30),
                "Docked",
                "\"StationName\":\"Garay Terminal\",\"StarSystem\":\"Deciat\""));

        var line = RecapAtRestart();

        Assert.NotNull(line);
        Assert.StartsWith("Earlier this session", line, StringComparison.Ordinal);
        Assert.Contains("Garay Terminal, Deciat", line, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyALocationAfterTheLoginFallsBackToTheSessionBefore()
    {
        _corpus.Journal(
            Evening.AddDays(-1),
            JournalCorpus.LoadGame(Evening.AddDays(-1)),
            JournalCorpus.Jump(Evening.AddDays(-1).AddMinutes(10), "Deciat", 8.09),
            JournalCorpus.Event(
                Evening.AddDays(-1).AddMinutes(30),
                "Docked",
                "\"StationName\":\"Garay Terminal\",\"StarSystem\":\"Deciat\""));
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Event(Evening.AddMinutes(1), "Location", "\"StarSystem\":\"Deciat\""));

        var line = RecapAtRestart();

        Assert.NotNull(line);
        Assert.StartsWith("Last session", line, StringComparison.Ordinal);
        Assert.Contains("Garay Terminal, Deciat", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ASessionThatEndedWithShutdownIsRecappedAsTheLastSession()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Jump(Evening.AddMinutes(10), "Deciat", 8.09),
            JournalCorpus.Event(Evening.AddMinutes(30), "Shutdown"));

        var line = RecapAtRestart();

        Assert.NotNull(line);
        Assert.StartsWith("Last session", line, StringComparison.Ordinal);
    }
}
