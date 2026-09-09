using System.Security.Cryptography;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>The envelope, and the receipt that makes an upload checkable.</summary>
public class ADonationCarriesItsOwnReceiptTests
{
    private static readonly ExcerptPaperwork Paperwork = new(
        "0.89.0+abcdef",
        new DateTimeOffset(2026, 8, 29, 14, 25, 30, TimeSpan.Zero));

    private const string Token = "0123456789abcdef0123456789abcdef";

    private static DonationEnvelope Sealed(string payload = "the excerpt", string? donor = Token) =>
        DonationEnvelope.For(
            DonationEnvelope.Excerpt, donor, Paperwork, DonationEnvelope.Utf8.GetBytes(payload));

    /// <summary>The hash is over the scrubbed payload and nothing else.</summary>
    [Fact]
    public void TheHashIsOverThePayloadAndAnyoneCanReproduceIt()
    {
        const string payload = "### Incident excerpt\nsomething happened\n";

        var envelope = Sealed(payload);
        var independently = Convert.ToHexStringLower(
            SHA256.HashData(new System.Text.UTF8Encoding(false).GetBytes(payload)));

        Assert.Equal(independently, envelope.Sha256);
        Assert.Equal(DonationEnvelope.Utf8.GetByteCount(payload), envelope.Bytes);
    }

    /// <summary>One byte changed is a different donation, which is the whole use of a hash here.</summary>
    [Fact]
    public void OneChangedByteIsADifferentHash() =>
        Assert.NotEqual(Sealed("the excerpt").Sha256, Sealed("the excerpts").Sha256);

    /// <summary>
    /// The three fields agreed before the lanes split, present from the first build so the consent side
    /// and the transport side could be built at once.
    /// </summary>
    [Fact]
    public void TheEnvelopeCarriesTheAgreedThreeFromDayOne()
    {
        var headers = Sealed().Headers().ToDictionary(h => h.Key, h => h.Value, StringComparer.Ordinal);

        Assert.Equal("1", headers[DonationEnvelope.FormatHeader]);
        Assert.Equal(Token, headers[DonationEnvelope.DonorHeader]);
        Assert.Equal(64, headers[DonationEnvelope.Sha256Header].Length);
    }

    /// <summary>Nothing about who donated is in the body.</summary>
    [Fact]
    public void TheTokenTravelsOnTheEnvelopeAndNotInThePayload()
    {
        const string payload = "### Incident excerpt\nno identity here\n";

        Assert.DoesNotContain(Token, payload, StringComparison.Ordinal);
        Assert.Contains(
            Sealed(payload).Headers(),
            header => header.Key == DonationEnvelope.DonorHeader && header.Value == Token);
    }

    /// <summary>
    /// An envelope missing the token is refused before a request is made — which matters because a
    /// refused request has still spent one against the daily ceiling that is the reason this design
    /// cannot bill.
    /// </summary>
    [Fact]
    public void AnEnvelopeWithNoTokenIsNotWellFormed()
    {
        Assert.True(Sealed().IsWellFormed());
        Assert.False(Sealed(donor: null).IsWellFormed());
        Assert.False(Sealed(donor: "CMDR ALPHA").IsWellFormed());
    }

    /// <summary>
    /// Two prefixes, because there are two opposite retention rules and a lifecycle rule is written
    /// against a prefix.
    /// </summary>
    [Fact]
    public void TheTwoKindsLandUnderDifferentPrefixes()
    {
        Assert.StartsWith("excerpts/", DonationEnvelope.Prefix(DonationEnvelope.Excerpt), StringComparison.Ordinal);
        Assert.StartsWith("corpus/", DonationEnvelope.Prefix(DonationEnvelope.Corpus), StringComparison.Ordinal);
        Assert.NotEqual(
            DonationEnvelope.Prefix(DonationEnvelope.Excerpt),
            DonationEnvelope.Prefix(DonationEnvelope.Corpus));
    }

    /// <summary>
    /// The predicted key groups by donor, which is the accumulation #176 bought — and it is also what
    /// makes deleting one donor's everything a prefix delete.
    /// </summary>
    [Fact]
    public void ADonorsDonationsSitUnderOnePrefix()
    {
        var key = Sealed().PredictedKey();

        Assert.StartsWith($"excerpts/{Token}/", key, StringComparison.Ordinal);
        Assert.EndsWith(".md.gz", key, StringComparison.Ordinal);
    }

    /// <summary>A build stamp comes off the running binary and a local build can make it any length.</summary>
    [Fact]
    public void AnAbsurdBuildStampIsCutDownBeforeItIsAHeader()
    {
        var envelope = DonationEnvelope.For(
            DonationEnvelope.Excerpt,
            Token,
            new ExcerptPaperwork(new string('x', 5_000), Paperwork.TakenAt),
            "payload"u8);

        var build = envelope.Headers().Single(h => h.Key == DonationEnvelope.BuildHeader).Value;

        Assert.Equal(DonationEnvelope.MostHeaderCharacters, build.Length);
    }

    /// <summary>
    /// A header value with a newline in it is a request-splitting attempt wearing a version number's
    /// clothes.
    /// </summary>
    [Fact]
    public void AHeaderValueCannotCarryALineBreak()
    {
        var envelope = DonationEnvelope.For(
            DonationEnvelope.Excerpt,
            Token,
            new ExcerptPaperwork("0.1.0\r\nd47-kind: corpus", Paperwork.TakenAt),
            "payload"u8);

        var build = envelope.Headers().Single(h => h.Key == DonationEnvelope.BuildHeader).Value;

        Assert.DoesNotContain('\r', build);
        Assert.DoesNotContain('\n', build);
    }

    /// <summary>The excerpt's receipt claims the document is the payload, and it is.</summary>
    [Fact]
    public void AnExcerptReceiptSaysItsSiblingIsTheBytesThatLeft()
    {
        const string payload = "### Incident excerpt\nthe whole thing\n";

        var envelope = Sealed(payload);
        var receipt = DonationReceipt.Render(
            envelope,
            DonationOutcome.Stored(envelope.PredictedKey()),
            "https://example.invalid/donate",
            DonationReceipt.NamesFor(envelope).Document,
            documentIsPayload: true);

        Assert.Contains(envelope.Sha256, receipt, StringComparison.Ordinal);
        Assert.Contains("is the payload", receipt, StringComparison.Ordinal);
        Assert.Contains(envelope.PredictedKey(), receipt, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a corpus receipt says the opposite in as many words, rather than letting a reader carry the
    /// excerpt's claim across.
    /// </summary>
    [Fact]
    public void ACorpusReceiptSaysThePayloadIsNotKept()
    {
        var envelope = DonationEnvelope.For(
            DonationEnvelope.Corpus, Token, Paperwork, 401_000_000, new string('a', 64));

        var receipt = DonationReceipt.Render(
            envelope,
            DonationOutcome.Stored(envelope.PredictedKey()),
            "https://example.invalid/donate",
            "the report",
            documentIsPayload: false);

        Assert.Contains("not kept here", receipt, StringComparison.Ordinal);
        Assert.Contains("indefinitely", receipt, StringComparison.Ordinal);
    }

    /// <summary>A refusal is written down too.</summary>
    [Fact]
    public void ARefusalGetsAReceiptThatSaysNothingArrived()
    {
        var envelope = Sealed();
        var receipt = DonationReceipt.Render(
            envelope,
            DonationOutcome.Refused("The endpoint refused it with 413. Nothing was written."),
            "https://example.invalid/donate",
            DonationReceipt.NamesFor(envelope).Document,
            documentIsPayload: true);

        Assert.Contains("did not arrive", receipt, StringComparison.Ordinal);
        Assert.Contains("nothing to delete", receipt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both files land, and the document is the payload byte for byte — asserted by hashing what was
    /// actually written rather than by trusting the string that was passed in.
    /// </summary>
    [Fact]
    public void WritingLeavesTheBytesAndTheirHashSideBySide()
    {
        var folder = Directory.CreateTempSubdirectory("d47-receipt").FullName;

        try
        {
            const string payload = "### Incident excerpt\nkept beside the executable\n";

            var envelope = Sealed(payload);
            var written = DonationReceipt.Write(
                folder,
                envelope,
                DonationOutcome.Stored(envelope.PredictedKey()),
                "https://example.invalid/donate",
                payload,
                documentIsPayload: true);

            Assert.NotNull(written);

            var document = Path.Combine(folder, DonationReceipt.NamesFor(envelope).Document);

            Assert.Equal(
                envelope.Sha256,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(document))));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// The withdrawal route is on the receipt, because a receipt is the thing a donor still has when
    /// they have forgotten everything else about the donation.
    /// </summary>
    [Fact]
    public void TheReceiptSaysHowToStopTheGrouping()
    {
        var envelope = Sealed();
        var receipt = DonationReceipt.Render(
            envelope,
            DonationOutcome.Stored(envelope.PredictedKey()),
            "https://example.invalid/donate",
            "d.md",
            documentIsPayload: true);

        Assert.Contains("donor-token.txt", receipt, StringComparison.Ordinal);
        Assert.Contains("does not reach back", receipt, StringComparison.Ordinal);
    }

    /// <summary>
 /// "Quote the object name" stopped one step short of a withdrawal route: it told a donor
    /// exactly what to say and nothing about who to say it to.
    /// </summary>
    [Fact]
    public void TheReceiptSaysWhereToAsk()
    {
        var envelope = Sealed();

        Assert.Contains(
            DonationNotice.Url,
            DonationReceipt.Render(
                envelope,
                DonationOutcome.Stored(envelope.PredictedKey()),
                "https://example.invalid/donate",
                "d.md",
                documentIsPayload: true),
            StringComparison.Ordinal);
    }
}
