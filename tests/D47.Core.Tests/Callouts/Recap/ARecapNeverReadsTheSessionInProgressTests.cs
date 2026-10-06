using D47.Core.Callouts;
using D47.Core.Logbook;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

/// <summary>The session that is starting is never recapped, and an empty one is not either.</summary>
public sealed class ARecapNeverReadsTheSessionInProgressTests : IDisposable
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _corpus.Dispose();
    }

    [Fact]
    public void OneLoadGameAndNoEarlierOneGivesNoRecap()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Jump(Evening.AddMinutes(10), "Deciat", 8.09));

        Assert.Null(LogRanges.PreviousSession(Evening, _corpus.Files, NullLogger.Instance));
    }

    [Fact]
    public void ASessionWithNothingBeyondItsOpeningAndClosingGivesNoRecap()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Event(Evening.AddMinutes(1), "Location", "\"StarSystem\":\"Deciat\""));

        var range = LogRanges.PreviousSession(Evening.AddDays(1), _corpus.Files, NullLogger.Instance)!;
        var digest = new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance).Build(_corpus.Files, range);

        Assert.Null(RecapCallout.Compose(digest));
    }
}
