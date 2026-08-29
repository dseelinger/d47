using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// Consenting to a corpus nobody can read
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>The control #160 shipped does not survive this.</b> It was "the Commander reads the scrubbed
/// excerpt and says yes to that", and nobody reads 383 MB. What replaces it is a report sized by
/// the number of distinct event <i>kinds</i> rather than the number of events, carrying one real
/// scrubbed instance of each — so nothing is agreed to unseen in kind even though most of it is
/// unseen in volume.
/// </para>
/// </summary>
public class ACorpusIsConsentedByKindTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-corpus").FullName;

    private static readonly ExcerptPaperwork Paperwork =
        new("0.88.0+test", new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Journal(string startedAt, params string[] lines) =>
        File.WriteAllLines(Path.Combine(_root, $"Journal.{startedAt}.01.log"), lines);

    private CorpusSurvey Survey(Pseudonyms names) =>
        CorpusDonation.Survey(
            _root,
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue,
            names,
            cancel: TestContext.Current.CancellationToken);

    /// <summary>
    /// <b>The property the whole design rests on.</b> Every sample printed in the report is a line
    /// that is actually in the payload, byte for byte — which is #160's "what is shown is what
    /// leaves" surviving into a payload that cannot be shown in full. A report assembled by one
    /// code path and a payload by another would be two artefacts, and only one of them was read.
    /// </summary>
    [Fact]
    public void EverySampleInTheReportIsALineInThePayload()
    {
        Journal(
            "2026-01-01T100000",
            """{"timestamp":"2026-01-01T10:00:00Z","event":"Commander","Name":"JOHN DEPARAGON","FID":"F1234567"}""",
            """{"timestamp":"2026-01-01T10:01:00Z","event":"FSDJump","StarSystem":"Sol","SquadronFaction":true}""",
            """{"timestamp":"2026-01-01T10:02:00Z","event":"Docked","StationName":"Abraham Lincoln"}""");

        var names = new Pseudonyms();
        var survey = Survey(names);

        var payload = new StringWriter();
        CorpusDonation.Write(
            _root,
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue,
            names,
            payload,
            cancel: TestContext.Current.CancellationToken);

        var lines = payload.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(survey.Kinds);

        foreach (var kind in survey.Kinds)
        {
            Assert.NotNull(kind.Sample);
            Assert.Contains(kind.Sample, lines);
        }
    }

    /// <summary>
    /// <b>The size argument, which is the only reason this is consentable at all.</b> The report is
    /// O(kinds) and the payload is O(events). Twenty times the events of the same kinds must not
    /// make the document a reader has to get through any longer — otherwise this is the same
    /// problem with extra steps, which is what reviewing session by session would have been.
    /// </summary>
    [Fact]
    public void TheReportDoesNotGrowWithTheNumberOfEvents()
    {
        static string[] Jumps(int howMany) =>
        [
            .. Enumerable.Range(0, howMany).Select(i =>
                $$"""{"timestamp":"2026-01-01T10:{{i / 60 % 60:00}}:{{i % 60:00}}Z","event":"FSDJump","StarSystem":"Sol"}"""),
        ];

        Journal("2026-01-01T100000", Jumps(5));
        var small = CorpusReport.Render(Survey(new Pseudonyms()), Paperwork);
        var smallEvents = Survey(new Pseudonyms()).Tally.Events;

        File.Delete(Path.Combine(_root, "Journal.2026-01-01T100000.01.log"));
        Journal("2026-01-01T100000", Jumps(100));
        var large = CorpusReport.Render(Survey(new Pseudonyms()), Paperwork);
        var largeEvents = Survey(new Pseudonyms()).Tally.Events;

        Assert.Equal(5, smallEvents);
        Assert.Equal(100, largeEvents);

        // Twenty times the payload. The document a person reads grows by the width of a couple of
        // numbers, not by the number of events.
        Assert.True(
            large.Length < small.Length + 200,
            $"report grew from {small.Length} to {large.Length} for 20x the events");
    }

    /// <summary>
    /// A reader checking this report is checking the scrub, so the instance shown for a kind is one
    /// the scrub touched — even where an untouched instance of the same kind is longer.
    /// </summary>
    [Fact]
    public void AChangedInstanceIsShownInPreferenceToAnUntouchedOne()
    {
        Journal(
            "2026-01-01T100000",

            // Changed: SquadronFaction is dropped outright.
            """{"timestamp":"2026-01-01T10:00:00Z","event":"FSDJump","StarSystem":"Sol","SquadronFaction":true}""",

            // Untouched, and deliberately much longer, so length alone would pick the wrong one.
            """{"timestamp":"2026-01-01T10:01:00Z","event":"FSDJump","StarSystem":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""");

        var jump = Assert.Single(Survey(new Pseudonyms()).Kinds, kind => kind.Kind == "FSDJump");

        Assert.Equal(2, jump.Events);
        Assert.Equal(1, jump.Changed);
        Assert.Contains("Sol", jump.Sample);
        Assert.DoesNotContain("SquadronFaction", jump.Sample);
    }

    /// <summary>
    /// Within the same class the longest wins, because it is the maximal-exposure instance — the
    /// one with the most fields that survived. Consenting to the worst case is a stronger act than
    /// consenting to a typical one.
    /// </summary>
    [Fact]
    public void TheLongestInstanceOfAKindIsTheOneShown()
    {
        Journal(
            "2026-01-01T100000",
            """{"timestamp":"2026-01-01T10:00:00Z","event":"Docked"}""",
            """{"timestamp":"2026-01-01T10:01:00Z","event":"Docked","StarSystem":"Sol","MarketID":128}""");

        var docked = Assert.Single(Survey(new Pseudonyms()).Kinds, kind => kind.Kind == "Docked");

        Assert.Equal(2, docked.Events);
        Assert.Equal(0, docked.Changed);
        Assert.Contains("MarketID", docked.Sample);
    }

    /// <summary>
    /// Kinds the scrub left alone are listed too. <b>An inventory that shows only what was touched
    /// is a curated one</b>, and the claim this report makes is that every kind is accounted for.
    /// </summary>
    [Fact]
    public void UntouchedKindsAreListedRatherThanOmitted()
    {
        Journal(
            "2026-01-01T100000",
            """{"timestamp":"2026-01-01T10:00:00Z","event":"Commander","Name":"JOHN DEPARAGON","FID":"F1234567"}""",
            """{"timestamp":"2026-01-01T10:01:00Z","event":"Music","MusicTrack":"Exploration"}""");

        var report = CorpusReport.Render(Survey(new Pseudonyms()), Paperwork);

        Assert.Contains("Commander", report);
        Assert.Contains("Music", report);
        Assert.Contains("Kinds the scrub left alone — 1 of 2", report);
        Assert.Contains("Kinds the scrub changed — 1 of 2", report);
    }

    /// <summary>
    /// The Commander's name never reaches the report, which is the point of the field list — and
    /// the report is the thing that will be pasted somewhere, so a leak here is the whole leak.
    /// </summary>
    [Fact]
    public void TheCommandersNameIsNotInTheReport()
    {
        Journal(
            "2026-01-01T100000",
            """{"timestamp":"2026-01-01T10:00:00Z","event":"Commander","Name":"JOHN DEPARAGON","FID":"F1234567"}""");

        var report = CorpusReport.Render(Survey(new Pseudonyms()), Paperwork);

        Assert.DoesNotContain("JOHN DEPARAGON", report);
        Assert.DoesNotContain("F1234567", report);
    }

    /// <summary>
    /// <b>A line the parser cannot read appears in no other count</b>, so it is counted here or it
    /// is invisible. The excerpt path can afford to drop these silently because a Commander reads
    /// that payload in full; nobody reads a corpus.
    /// </summary>
    [Fact]
    public void ALineThatCannotBeReadIsCountedRatherThanDroppedSilently()
    {
        Journal(
            "2026-01-01T100000",
            """{"timestamp":"2026-01-01T10:00:00Z","event":"Docked"}""",
            "{ this is not an event",
            string.Empty);

        var survey = Survey(new Pseudonyms());

        Assert.Equal(1, survey.Tally.Events);

        // The blank line is not one of them: a journal file ends with one and it means nothing.
        Assert.Equal(1, survey.Tally.Unreadable);
        Assert.Contains("could not be read as an event at all", CorpusReport.Render(survey, Paperwork));
    }

    /// <summary>
    /// An empty folder is a real case — a Commander who has never flown, or a wrong path — and it
    /// has to render rather than throw, saying it found nothing rather than implying it found some.
    /// </summary>
    [Fact]
    public void AnEmptyRangeSaysSoRatherThanRenderingAnEmptyClaim()
    {
        var survey = Survey(new Pseudonyms());
        var report = CorpusReport.Render(survey, Paperwork);

        Assert.Equal(0, survey.Tally.Events);
        Assert.Contains("an empty range — no events were found", report);
    }

    /// <summary>
    /// <b>The report a corpus donor reads had no retention sentence and no notice at all</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/168">#168</a>,
    /// <a href="https://github.com/dseelinger/d47/issues/166">#166</a>). The excerpt's said where it
    /// came from and how long it lasts; this one said neither — and it is the donation with the
    /// harder sentence to write, because the answer is *indefinitely*. A retention period is a
    /// promise to a donor, so it belongs where consent is given rather than only in the receipt
    /// written afterwards.
    /// </summary>
    [Fact]
    public void TheReportSaysHowLongItIsKeptAndWhereToReadWhoHoldsIt()
    {
        var report = CorpusReport.Render(Survey(new Pseudonyms()), Paperwork);

        Assert.Contains("kept indefinitely", report);
        Assert.Contains(DonationNotice.Url, report);
    }
}
