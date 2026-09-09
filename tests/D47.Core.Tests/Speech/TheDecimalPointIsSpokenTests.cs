using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

public class TheDecimalPointIsSpokenTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The reported number ----------------------------------------------------------------

    /// <summary>The number from the report, in words.</summary>
    [Theory]
    [InlineData("5.79", "five point seven nine")]
    [InlineData("1.5", "one point five")]
    [InlineData("0.5", "zero point five")]
    [InlineData("395", "three ninety-five")]
    public void ADecimalIsSaidWithItsPoint(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>The fraction is digits, never a number.</summary>
    [Fact]
    public void TheFractionIsSaidDigitByDigit()
    {
        Assert.Equal("five point seven nine", SpokenNumber.Say("5.79"));
        Assert.DoesNotContain("seventy", SpokenNumber.Say("5.79"), StringComparison.Ordinal);

        // Which is most visible on a fraction whose digits would read as a round number.
        Assert.Equal("two point five zero", SpokenNumber.Say("2.50"));
    }

    /// <summary>#177 left the whole part on its casual reading, and #184 overturned that.</summary>
    [Theory]
    [InlineData("128.5", "one hundred twenty-eight point five")]
    [InlineData("128", "one twenty-eight")]
    public void TheWholePartTakesTheReadingItsShapeAsksFor(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    // ---- The two ragged ends, ruled -----------------------------------------------------------

    [Fact]
    public void ALeadingPointSaysNoWholePart() =>
        Assert.Equal("point seven nine", SpokenNumber.Say(".79"));

    /// <summary>A trailing point is a full stop, not a decimal point, so the number is said without it.</summary>
    [Fact]
    public void ATrailingPointIsNotADecimalPoint()
    {
        Assert.Equal("five", SpokenNumber.Say("5."));

        var said = Rules.ToPhonemes("The range is 5.");

        Assert.EndsWith("fˈaɪv.", said, StringComparison.Ordinal);
    }

    // ---- What is deliberately still spelled ---------------------------------------------------

    /// <summary>Two points is a version, not a decimal, and it keeps the reading it had.</summary>
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

    /// <summary>The reported sentence, through the ladder.</summary>
    [Fact]
    public void TheReportedSentenceSaysItsPoint()
    {
        var said = Rules.ToPhonemes("5.79 ly");

        Assert.StartsWith("fˈaɪv pˈɔɪnt sˈɛvən nˈaɪn", said, StringComparison.Ordinal);
    }

    /// <summary>And the spelled reading is gone.</summary>
    [Fact]
    public void TheDecimalIsNoLongerSpelled()
    {
        var said = Rules.ToPhonemes("5.79");

        Assert.DoesNotContain(",", said, StringComparison.Ordinal);
    }

    /// <summary>The rung says so itself.</summary>
    [Fact]
    public void TheDecimalFallsOffTheNumberRung()
    {
        var rungs = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(null, null, (segment, rung, _) => rungs.Add((segment, rung)))
            .ToPhonemes("5.79");

        Assert.Contains(rungs, fell => fell.Segment == "5.79" && fell.Rung == PhonemeRung.Number);
    }
}
