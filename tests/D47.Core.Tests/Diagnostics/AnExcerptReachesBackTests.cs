using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>Reading an incident out of the journal and log folders rather than the running session.</summary>
public class AnExcerptReachesBackTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private const string _root = "C:/d47-test/reach";

    private readonly MemoryFileSystem _files = new();

    private string Journal(string startedAt, params string[] lines)
    {
        var path = Path.Combine(_root, $"Journal.{startedAt}.01.log");
        _files.WriteLines(path, lines);
        return path;
    }

    private void Log(string day, params string[] lines) =>
        _files.WriteLines(Path.Combine(_root, $"d47-{day}.log"), lines);

    private static string Event(string at, string kind) =>
        $$"""{"timestamp":"{{at}}","event":"{{kind}}"}""";

    /// <summary>
    /// The change this file is named for: an excerpt now spans the files a window touches, rather than the one Elite
    /// happens to have open.
    /// </summary>
    [Fact]
    public void TheJournalHalfCrossesSessionBoundaries()
    {
        Journal("2026-08-26T100000", Event("2026-08-26T10:00:00Z", "Liftoff"));
        Journal("2026-08-27T100000", Event("2026-08-27T10:00:00Z", "FSDJump"));
        Journal("2026-08-28T100000", Event("2026-08-28T10:00:00Z", "Docked"));

        var entries = IncidentSources.Journals(
            _files,
            _root,
            new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(["FSDJump", "Docked"], entries.Select(entry => entry.Kind));
    }

    /// <summary>Oldest first, because that is the order a replay needs.</summary>
    [Fact]
    public void TheJournalHalfComesBackInTheOrderItHappened()
    {
        Journal("2026-08-27T100000", Event("2026-08-27T10:00:00Z", "FSDJump"));
        Journal("2026-08-28T100000", Event("2026-08-28T10:00:00Z", "Docked"));

        var entries = IncidentSources.Journals(
            _files,
            _root,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(entries[0].Timestamp < entries[1].Timestamp);
    }

    /// <summary>A long session is why the lower bound cannot be the filename.</summary>
    [Fact]
    public void TheFileThatWasOpenWhenTheWindowBeganIsRead()
    {
        Journal(
            "2026-08-28T060000",
            Event("2026-08-28T06:00:00Z", "LoadGame"),
            Event("2026-08-28T12:30:00Z", "FSDJump"));

        Journal("2026-08-28T200000", Event("2026-08-28T20:00:00Z", "Shutdown"));

        var entries = IncidentSources.Journals(
            _files,
            _root,
            new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 28, 13, 0, 0, TimeSpan.Zero));

        Assert.Equal(["FSDJump"], entries.Select(entry => entry.Kind));
    }

    /// <summary>
    /// The log half spans days too — and the day comes off the filename, which is what lets the same
    /// clock time on two nights be told apart.
    /// </summary>
    [Fact]
    public void TheLogHalfCrossesMidnight()
    {
        Log("20260827", "[23:58:00 INF] D47.App.AppHost: the night before");
        Log("20260828", "[23:58:00 INF] D47.App.AppHost: just before");
        Log("20260829", "[00:01:00 INF] D47.App.AppHost: just after");

        var entries = IncidentSources.Logs(
            _files,
            _root,
            new DateTimeOffset(2026, 8, 28, 23, 55, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 29, 0, 5, 0, TimeSpan.Zero),
            Utc);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.Text.Contains("just before"));
        Assert.Contains(entries, entry => entry.Text.Contains("just after"));
        Assert.DoesNotContain(entries, entry => entry.Text.Contains("the night before"));
    }

    /// <summary>
    /// A folder that is not there is a legitimate state — no Elite install, or a Commander who has
    /// moved their journals — rather than an error to throw mid-donation.
    /// </summary>
    [Fact]
    public void AMissingFolderIsEmptyRatherThanAThrow()
    {
        var absent = Path.Combine(_root, "nowhere");

        Assert.Empty(IncidentSources.Journals(_files, absent, DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
        Assert.Empty(IncidentSources.Logs(_files, absent, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, Utc));
    }

    /// <summary>
    /// The spans on offer stop short of everything, on purpose: the consent this window asks for is
    /// read this and say yes to it, and past some size nobody reads it.
    /// </summary>
    [Fact]
    public void TheSpansOnOfferStopAtSomethingReadable()
    {
        Assert.All(ExcerptSpan.All, span => Assert.True(span.Before <= TimeSpan.FromHours(12)));

        // Tightest first, so the default is the narrowest rather than whatever ended up on top.
        Assert.Equal(ExcerptSpan.All.OrderBy(span => span.Before), ExcerptSpan.All);
        Assert.Equal(ExcerptSpan.All[0], ExcerptSpan.Default);
    }

    /// <summary>A span puts a window around the mark; the mark itself is unchanged by the choice.</summary>
    [Fact]
    public void ASpanIsAWindowAroundTheMark()
    {
        var marked = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var request = ExcerptSpan.All.Single(span => span.Name == "The last 12 hours").Around(marked, includeMySpeech: true);

        Assert.Equal(marked, request.MarkedAt);
        Assert.Equal(marked.AddHours(-12), request.From);
        Assert.True(request.IncludeMySpeech);
    }
}
