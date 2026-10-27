using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>Where a long journal history is cut into donations (#1784).</summary>
public class ALargeCorpusIsPlannedInPartsTests
{
    private const long Limit = 100;

    private static CorpusSurvey Survey(params (string Name, long Bytes)[] files) =>
        new(
            null,
            null,
            files.Length,
            files.Sum(file => file.Bytes),
            new CorpusTally(0, 0, 0, 0, 0, 0),
            [],
            files.Select(file => new CorpusFileBytes(file.Name, file.Bytes)).ToList());

    [Fact]
    public void ASpanThatFitsIsOnePart()
    {
        var parts = Survey(("a.log", 40), ("b.log", 60)).Parts(Limit);

        Assert.Equal([["a.log", "b.log"]], parts);
    }

    [Fact]
    public void PartsSplitBetweenFilesOldestFirst()
    {
        var parts = Survey(("a.log", 60), ("b.log", 50), ("c.log", 30), ("d.log", 70)).Parts(Limit);

        Assert.Equal([["a.log"], ["b.log", "c.log"], ["d.log"]], parts);
    }

    [Fact]
    public void AFileOverTheLimitIsAPartOnItsOwn()
    {
        var parts = Survey(("a.log", 30), ("big.log", 250), ("c.log", 30)).Parts(Limit);

        Assert.Equal([["a.log"], ["big.log"], ["c.log"]], parts);
    }

    [Fact]
    public void AFileExactlyAtTheLimitFillsItsPart()
    {
        var parts = Survey(("a.log", 100), ("b.log", 1)).Parts(Limit);

        Assert.Equal([["a.log"], ["b.log"]], parts);
    }

    [Fact]
    public void ASpanWithNothingInItHasNoParts()
    {
        Assert.Empty(Survey().Parts(Limit));
    }

    [Fact]
    public void ThePartLimitIsTwoHundredAndFiftySixMebibytes()
    {
        Assert.Equal(256L * 1024 * 1024, CorpusSurvey.MostPartBytes);
    }

    [Fact]
    public void TheReportSaysHowManyPartsOnlyWhenThereAreSeveral()
    {
        var paperwork = new ExcerptPaperwork("1.0.0", new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        var big = CorpusSurvey.MostPartBytes;

        var several = CorpusReport.Render(Survey(("a.log", big), ("b.log", big), ("c.log", 5)), paperwork);
        var one = CorpusReport.Render(Survey(("a.log", 5)), paperwork);

        Assert.Contains("Sent in 3 parts", several);
        Assert.DoesNotContain("parts", one.Split("**What it is for.**")[0]);
    }

    [Fact]
    public void AReceiptSaysWhichPartItIsOnlyWhenThereAreSeveral()
    {
        var envelope = new DonationEnvelope(
            1, DonationEnvelope.Corpus, null, "1.0.0", new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero),
            10, new string('a', 64));

        var part = DonationReceipt.Render(
            envelope, DonationOutcome.Stored("k"), "https://x/donate", "doc.md", false, new DonationPart(2, 5));
        var whole = DonationReceipt.Render(
            envelope, DonationOutcome.Stored("k"), "https://x/donate", "doc.md", false, new DonationPart(1, 1));

        Assert.Contains("part 2 of 5", part);
        Assert.DoesNotContain("part 1 of 1", whole);
        Assert.NotEqual(
            DonationReceipt.NamesFor(envelope, new DonationPart(1, 2)).Receipt,
            DonationReceipt.NamesFor(envelope, new DonationPart(2, 2)).Receipt);
        Assert.Equal(
            DonationReceipt.NamesFor(envelope).Receipt,
            DonationReceipt.NamesFor(envelope, new DonationPart(1, 1)).Receipt);
    }
}
