using D47.Core.Logbook;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>
/// "A sentence that cannot be traced back is the bug this item exists to prevent" (list.md
/// Phase 33, item 2).
/// </summary>
public class LogAuditTests
{
    [Fact]
    public void ASentenceWithNoCitationIsFoundAndMarkedWhereItStands()
    {
        var audit = LogAudit.Check(
            "I dropped into Deciat and went looking for Felicity [1]. It was raining on the pad, "
            + "and I thought about my father.",
            Digest(2));

        Assert.Equal(2, audit.Sentences);
        var untraced = Assert.Single(audit.Uncited);
        Assert.Contains("my father", untraced.Text, StringComparison.Ordinal);

        Assert.False(audit.Clean);
        Assert.Contains(LogAudit.UnsupportedMarker.Trim(), audit.Annotated, StringComparison.Ordinal);
        Assert.Contains("1 of 2 sentences", audit.Summary(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Worse than citing nothing, because it looks like evidence: a reader glancing at the
    /// brackets sees a sourced claim.
    /// </summary>
    [Fact]
    public void ACitationNamingAFactThatDoesNotExistIsItsOwnFailure()
    {
        var audit = LogAudit.Check("I made a fortune on the way out [9].", Digest(3));

        Assert.Empty(audit.Uncited);
        Assert.Equal([9], audit.Unknown);
        Assert.False(audit.Clean);
        Assert.Contains("does not exist", audit.Summary(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Elite reports a great many decimals — 8.09 ly, 0.24 g — and a splitter that treated each as
    /// a full stop would report half a log as uncited fragments and mark a clean file as broken.
    /// </summary>
    [Fact]
    public void ADecimalPointDoesNotEndASentence()
    {
        var audit = LogAudit.Check("It was 8.09 ly to Deciat and 0.24 g on the pad [1].", Digest(2));

        Assert.Single(audit.Claims);
        Assert.True(audit.Clean);
    }

    /// <summary>
    /// Headings are structure. A blockquote is not — that is where the ship's-AI commentary lives
    /// in the third voice, and an interjection that invents something is exactly as wrong as a
    /// paragraph that does.
    /// </summary>
    [Fact]
    public void HeadingsAreExemptAndCommentaryIsNot()
    {
        var audit = LogAudit.Check(
            """
            ## The run out

            Forty-two jumps and nothing to show for them [1].

            > I did tell you.
            """,
            Digest(2));

        Assert.Equal(2, audit.Sentences);
        Assert.Single(audit.Uncited);
        Assert.Contains("I did tell you", audit.Uncited[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheFactsActuallyCitedAreCountedAsUsed()
    {
        var audit = LogAudit.Check("A quiet run out and back [1,3].", Digest(5));

        Assert.Equal([1, 3], audit.Used);
        Assert.True(audit.Clean);
    }

    /// <summary>
    /// The prose has to come back out unchanged when nothing was wrong with it, or the file the
    /// Commander keeps is not the log that was written.
    /// </summary>
    [Fact]
    public void ACleanLogIsReproducedExactly()
    {
        const string Prose = """
                             I dropped into Deciat just after nine [1]. Felicity was in, which was the
                             point of the trip [2].

                             The way back was quiet [3].
                             """;

        var audit = LogAudit.Check(Prose, Digest(3));

        Assert.True(audit.Clean);
        Assert.Equal(Prose.ReplaceLineEndings(), audit.Annotated.ReplaceLineEndings());
    }

    private static LogDigest Digest(int facts) => new()
    {
        Range = LogRanges.Between(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)),
        JournalsRead = 1,
        EventsRead = 10,
        Facts =
        [
            .. Enumerable.Range(1, facts).Select(id => new LogFact
            {
                Id = id,
                Kind = LogFactKind.Travel,
                Statement = $"Fact {id}.",
            }),
        ],
    };
}
