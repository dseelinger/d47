using D47.Core.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>
/// What span a log covers (Phase 33, item 1: "a session — or a week").
/// </summary>
public class LogRangeTests : IDisposable
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _corpus.Dispose();
    }

    /// <summary>
    /// <b>The trap <see cref="D47.Core.Journal.SessionSummary"/> already records, met a second
    /// time.</b> Elite rolls a long session into a continuation journal without re-emitting
    /// <c>LoadGame</c>, so a session that starts from the newest file starts halfway through the
    /// evening — and the log would open with the Commander already somewhere, having done things
    /// it never mentions.
    /// </summary>
    [Fact]
    public void ASessionStartsAtTheLastLoadGameRatherThanAtTheLastFile()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Jump(Evening.AddMinutes(30), "Deciat", 8.09));

        // The continuation file. Elite writes a Fileheader and carries on; there is no LoadGame.
        _corpus.Journal(
            Evening.AddHours(3),
            JournalCorpus.Event(Evening.AddHours(3), "Fileheader", "\"part\":2"),
            JournalCorpus.Jump(Evening.AddHours(3).AddMinutes(10), "Shinrarta Dezhra", 40));

        var range = LogRanges.Resolve(
            LogSpan.Session,
            Evening.AddHours(4),
            _corpus.Files,
            NullLogger.Instance);

        Assert.Equal(Evening, range.From);
        Assert.Equal("the last session", range.Label);
    }

    /// <summary>
    /// A session that began at 23:50 and ran past midnight lives entirely inside a file named for
    /// the previous day. Filtering on the name alone would drop the first hour of every late-night
    /// log — and would do it quietly, because the log would still read as a complete evening.
    /// </summary>
    [Fact]
    public void TheFileBeforeTheWindowIsReadToo()
    {
        var late = new DateTimeOffset(3311, 4, 2, 23, 50, 0, TimeSpan.Zero);

        var yesterday = _corpus.Journal(late, JournalCorpus.LoadGame(late));
        var today = _corpus.Journal(late.AddHours(2), JournalCorpus.Event(late.AddHours(2), "Fileheader"));

        var files = LogRanges.FilesFor(
            _corpus.Files,
            LogRanges.Between(late.AddMinutes(20), late.AddHours(3)));

        Assert.Equal([yesterday, today], files);
    }

    [Fact]
    public void AWindowWayOutsideTheCorpusReadsNothing()
    {
        _corpus.Journal(Evening, JournalCorpus.LoadGame(Evening));

        Assert.Empty(LogRanges.FilesFor(
            _corpus.Files,
            LogRanges.Between(Evening.AddYears(-2), Evening.AddYears(-2).AddDays(1))));
    }

    /// <summary>
    /// Two date pickers can produce them the wrong way round, and a window with a negative length
    /// reads back as "nothing happened" rather than as an obvious mistake.
    /// </summary>
    [Fact]
    public void TwoDatesTheWrongWayRoundAreOrdered()
    {
        var range = LogRanges.Between(Evening.AddDays(3), Evening);

        Assert.Equal(Evening, range.From);
        Assert.Equal(Evening.AddDays(3), range.To);
    }

    /// <summary>
    /// A misspelt setting must not silently buy thirty days of prose, so an id nobody recognises
    /// falls back to the cheapest window rather than the widest.
    /// </summary>
    [Fact]
    public void AnUnrecognisedSpanIsASession()
    {
        Assert.Equal(LogSpan.Session, LogRanges.Parse("fortnight"));
        Assert.Equal(LogSpan.Session, LogRanges.Parse(null));
        Assert.Equal(LogSpan.Week, LogRanges.Parse("week"));
    }

    [Fact]
    public void WithNoLoadGameAnywhereItSaysSoRatherThanReportingAnEmptySession()
    {
        _corpus.Journal(Evening, JournalCorpus.Jump(Evening, "Deciat", 8.09));

        var range = LogRanges.Resolve(LogSpan.Session, Evening.AddHours(1), _corpus.Files, NullLogger.Instance);

        // "You did nothing" and "I could not find where you started" are different answers, and
        // only one of them is true.
        Assert.Contains("could not find", range.Label, StringComparison.Ordinal);
        Assert.Equal(Evening.AddHours(1).AddDays(-1), range.From);
    }
}
