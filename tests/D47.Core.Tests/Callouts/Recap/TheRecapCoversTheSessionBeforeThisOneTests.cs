using D47.Core.Callouts;
using D47.Core.Logbook;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

/// <summary>With two sessions on disk, the recap is drawn from the earlier one.</summary>
public sealed class TheRecapCoversTheSessionBeforeThisOneTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset NextEvening = Evening.AddDays(1);

    private readonly JournalCorpus _corpus = new();

    public TheRecapCoversTheSessionBeforeThisOneTests()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Jump(Evening.AddMinutes(10), "Deciat", 8.09),
            JournalCorpus.Event(Evening.AddMinutes(20), "Promotion", "\"Combat\":5"),
            JournalCorpus.Event(
                Evening.AddMinutes(30),
                "Docked",
                "\"StationName\":\"Garay Terminal\",\"StarSystem\":\"Deciat\""));

        _corpus.Journal(
            NextEvening,
            JournalCorpus.LoadGame(NextEvening),
            JournalCorpus.Jump(NextEvening.AddMinutes(5), "Sol", 12));
    }

    [Fact]
    public void TheWindowRunsFromTheEarlierLoadGameToItsLastEvent()
    {
        var range = LogRanges.PreviousSession(_corpus.FileSystem, NextEvening, _corpus.Files, NullLogger.Instance);

        Assert.NotNull(range);
        Assert.Equal(Evening, range.From);
        Assert.Equal(Evening.AddMinutes(30), range.To);
    }

    [Fact]
    public void TheLineNamesWhereItEndedTheShipAndThePromotion()
    {
        var range = LogRanges.PreviousSession(_corpus.FileSystem, NextEvening, _corpus.Files, NullLogger.Instance)!;
        var digest = new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance).Build(_corpus.Files, range);

        var line = RecapCallout.Compose(digest);

        Assert.NotNull(line);
        Assert.Contains("Garay Terminal, Deciat", line, StringComparison.Ordinal);
        Assert.Contains("Python", line, StringComparison.Ordinal);
        Assert.Contains("you were promoted", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Sol", line, StringComparison.Ordinal);
    }

    [Fact]
    public void WithTheGameClosedTheNewestSessionIsTheLastOne()
    {
        var range = LogRanges.PreviousSession(_corpus.FileSystem, NextEvening.AddHours(2), _corpus.Files, NullLogger.Instance);

        Assert.NotNull(range);
        Assert.Equal(NextEvening, range.From);
    }
}
