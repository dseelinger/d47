using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The decimal point, said (<a href="https://github.com/dseelinger/d47/issues/177">#177</a>).
/// <para>
/// <b>Every decimal d47 spoke lost its point.</b> <c>5.79</c> is not all digits and has no letters,
/// so it fell past the number rung to the bottom of the ladder — and the spelling rung has no sound
/// for a full stop, so it dropped it in silence. The Commander heard <em>"five, seven, nine"</em>
/// where the answer was <em>five point seven nine</em>.
/// </para>
/// <para>
/// <b>It was constant rather than occasional.</b> d47 says decimals in most of what it reports:
/// distances in light years, tonnages, percentages, credits with fractions. Every one of them was
/// wrong the same way, which is why this went first of the four.
/// </para>
/// </summary>
public class TheDecimalPointIsSpokenTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The reported number ----------------------------------------------------------------

    /// <summary>
    /// The number from the report, in words. Asserted on the words rather than the phonemes because
    /// the words are the ruling; the phonemes are how the ladder then says them.
    /// </summary>
    [Theory]
    [InlineData("5.79", "five point seven nine")]
    [InlineData("1.5", "one point five")]
    [InlineData("0.5", "zero point five")]
    [InlineData("395", "three ninety-five")]
    public void ADecimalIsSaidWithItsPoint(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// <b>The fraction is digits, never a number.</b> <em>Five point seventy-nine</em> is a
    /// different quantity to anybody listening, and this is the one place a decimal parts company
    /// with the casual reading the whole part gets.
    /// </summary>
    [Fact]
    public void TheFractionIsSaidDigitByDigit()
    {
        Assert.Equal("five point seven nine", SpokenNumber.Say("5.79"));
        Assert.DoesNotContain("seventy", SpokenNumber.Say("5.79"), StringComparison.Ordinal);

        // Which is most visible on a fraction whose digits would read as a round number.
        Assert.Equal("two point five zero", SpokenNumber.Say("2.50"));
    }

    /// <summary>
    /// <b>And the whole part keeps the casual reading it always had</b>, so the point is the only
    /// thing this changed. <c>128.5</c> is <em>one twenty-eight point five</em>, which is the
    /// designation reading the Commander ruled for a run of digits, untouched.
    /// </summary>
    [Theory]
    [InlineData("128.5", "one twenty-eight point five")]
    [InlineData("128", "one twenty-eight")]
    public void TheWholePartIsUnchanged(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    // ---- The two ragged ends, ruled -----------------------------------------------------------

    /// <summary>
    /// <b>A leading point says no whole part rather than inventing a zero for it.</b> That is what
    /// a person reading <c>.79</c> aloud says, and inventing a digit d47 was not given is the one
    /// thing a number rung must never do.
    /// </summary>
    [Fact]
    public void ALeadingPointSaysNoWholePart() =>
        Assert.Equal("point seven nine", SpokenNumber.Say(".79"));

    /// <summary>
    /// <b>A trailing point is a full stop, not a decimal point</b>, so the number is said without
    /// it. In practice it never arrives: a token's trailing full stop is trimmed off as phrasing
    /// before the ladder sees anything, which the second assertion pins — <c>5.</c> at the end of a
    /// sentence is the number five and a full stop, and Kokoro reads that stop as phrasing.
    /// </summary>
    [Fact]
    public void ATrailingPointIsNotADecimalPoint()
    {
        Assert.Equal("five", SpokenNumber.Say("5."));

        var said = Rules.ToPhonemes("The range is 5.");

        Assert.EndsWith("fˈaɪv.", said, StringComparison.Ordinal);
    }

    // ---- What is deliberately still spelled ---------------------------------------------------

    /// <summary>
    /// <b>Two points is a version, not a decimal</b>, and it keeps the reading it had. Admitting it
    /// here would say <c>0.90.0</c> as a number nobody asked for; leaving it spelled is what the
    /// ladder already does for a shape it cannot say.
    /// <para>
    /// <b><c>6,680</c> used to be listed here and no longer is.</b> #177 left the grouping comma
    /// out on purpose, saying a shape this rung did not yet own should be spelled rather than
    /// guessed at — and #183 is the issue that came back and made it a shape this rung owns. The
    /// version number is untouched, because two points is still a different reading.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("0.90.0")]
    public void AShapeThisRungDoesNotOwnIsLeftToTheLadder(string token) =>
        Assert.False(SpokenNumber.Looks(token));

    /// <summary>And a run with no digit in it is not a number however it is punctuated.</summary>
    [Theory]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("GQPI")]
    public void AnythingWithoutADigitIsNotANumber(string token) =>
        Assert.False(SpokenNumber.Looks(token));

    // ---- Through the ladder, which is where it was wrong ---------------------------------------

    /// <summary>
    /// <b>The reported sentence, through the ladder.</b> The number now falls off the number rung
    /// rather than the spelling rung, so the point is a word and the digits are not read out one at
    /// a time with pauses between them.
    /// </summary>
    [Fact]
    public void TheReportedSentenceSaysItsPoint()
    {
        var said = Rules.ToPhonemes("5.79 ly");

        Assert.StartsWith("fˈaɪv pˈɔɪnt sˈɛvən nˈaɪn", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the spelled reading is gone. A spelled run is joined by commas — that is how Kokoro
    /// spells a pause between letters — so a comma inside the number is exactly the defect,
    /// written down.
    /// </summary>
    [Fact]
    public void TheDecimalIsNoLongerSpelled()
    {
        var said = Rules.ToPhonemes("5.79");

        Assert.DoesNotContain(",", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The rung says so itself.</b> One debug line naming the rung is the whole diagnosis
    /// (<see cref="PhonemeRung"/>), so the fix is asserted where a reader of the trace would look:
    /// the decimal comes off <see cref="PhonemeRung.Number"/>, not <see cref="PhonemeRung.Spelled"/>.
    /// </summary>
    [Fact]
    public void TheDecimalFallsOffTheNumberRung()
    {
        var rungs = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(null, null, (segment, rung, _) => rungs.Add((segment, rung)))
            .ToPhonemes("5.79");

        Assert.Contains(rungs, fell => fell.Segment == "5.79" && fell.Rung == PhonemeRung.Number);
    }
}
