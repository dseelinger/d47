using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// Reading an incident off disk rather than out of memory
/// (<a href="https://github.com/dseelinger/d47/issues/173">#173</a>).
/// <para>
/// <b>The defect was never that the window was small.</b> It was that the control implied a reach
/// the sources did not have: the journal half read a 4,000-event buffer fed by a spine that tails
/// the newest journal, and the log half read the newest <c>d47-*.log</c>. A Commander who restarted
/// d47 and asked for sixty minutes got twenty, and was told nothing.
/// </para>
/// </summary>
public class AnExcerptReachesBackTests : IDisposable
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private readonly string _root = Directory.CreateTempSubdirectory("d47-reach").FullName;

    private string Journal(string startedAt, params string[] lines)
    {
        var path = Path.Combine(_root, $"Journal.{startedAt}.01.log");
        File.WriteAllLines(path, lines);
        return path;
    }

    private void Log(string day, params string[] lines) =>
        File.WriteAllLines(Path.Combine(_root, $"d47-{day}.log"), lines);

    private static string Event(string at, string kind) =>
        $$"""{"timestamp":"{{at}}","event":"{{kind}}"}""";

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// The whole point: an excerpt now spans the files a window touches, rather than the one Elite
    /// happens to have open.
    /// </summary>
    [Fact]
    public void TheJournalHalfCrossesSessionBoundaries()
    {
        Journal("2026-08-26T100000", Event("2026-08-26T10:00:00Z", "Liftoff"));
        Journal("2026-08-27T100000", Event("2026-08-27T10:00:00Z", "FSDJump"));
        Journal("2026-08-28T100000", Event("2026-08-28T10:00:00Z", "Docked"));

        var entries = IncidentSources.Journals(
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
            _root,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(entries[0].Timestamp < entries[1].Timestamp);
    }

    /// <summary>
    /// <b>A long session is why the lower bound cannot be the filename.</b> A journal that started
    /// six hours before the window has a name outside it and events inside it, so the file that was
    /// open when the window began is read whatever its name says — while a file that began after
    /// the window ended cannot hold anything in it and is never opened.
    /// </summary>
    [Fact]
    public void TheFileThatWasOpenWhenTheWindowBeganIsRead()
    {
        Journal(
            "2026-08-28T060000",
            Event("2026-08-28T06:00:00Z", "LoadGame"),
            Event("2026-08-28T12:30:00Z", "FSDJump"));

        Journal("2026-08-28T200000", Event("2026-08-28T20:00:00Z", "Shutdown"));

        var entries = IncidentSources.Journals(
            _root,
            new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 28, 13, 0, 0, TimeSpan.Zero));

        Assert.Equal(["FSDJump"], entries.Select(entry => entry.Kind));
    }

    /// <summary>
    /// The log half spans days too — and the day comes off the filename, which is what lets the
    /// same clock time on two nights be told apart.
    /// </summary>
    [Fact]
    public void TheLogHalfCrossesMidnight()
    {
        Log("20260827", "[23:58:00 INF] D47.App.AppHost: the night before");
        Log("20260828", "[23:58:00 INF] D47.App.AppHost: just before");
        Log("20260829", "[00:01:00 INF] D47.App.AppHost: just after");

        var entries = IncidentSources.Logs(
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

        Assert.Empty(IncidentSources.Journals(absent, DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
        Assert.Empty(IncidentSources.Logs(absent, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, Utc));
    }

    /// <summary>
    /// Elite holds the current journal open and Serilog holds today's log open, so a reader that
    /// wanted them to itself would throw on exactly the two files an incident is most likely to be
    /// in.
    /// </summary>
    [Fact]
    public void FilesSomethingElseIsWritingAreStillRead()
    {
        var path = Journal("2026-08-28T100000", Event("2026-08-28T10:00:00Z", "FSDJump"));

        using var held = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        Assert.Single(IncidentSources.Journals(_root, DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
    }

    /// <summary>
    /// The spans on offer stop short of everything, on purpose: the consent this window asks for is
    /// <i>read this and say yes to it</i>, and past some size nobody reads it. That is
    /// <a href="https://github.com/dseelinger/d47/issues/174">#174</a>.
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
