using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>The ruling #184 asked for, taken on 2026-08-29 (#184, item 4).</summary>
public class TheWholePartOfAQuantityIsSaidInFullTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The reported reading -----------------------------------------------------------------

    /// <summary>The token from the issue.</summary>
    [Fact]
    public void TheReportedQuantityIsSaidInFull()
    {
        Assert.Equal("one thousand two hundred thirty-four point five", SpokenNumber.Say("1234.5"));

        Assert.DoesNotContain("twelve", SpokenNumber.Say("1234.5"), StringComparison.Ordinal);
    }

    /// <summary>A decimal point makes it a quantity, and its whole part is said in full.</summary>
    [Theory]
    [InlineData("1234.5", "one thousand two hundred thirty-four point five")]
    [InlineData("128.5", "one hundred twenty-eight point five")]
    [InlineData("2637.5", "two thousand six hundred thirty-seven point five")]
    [InlineData("100.5", "one hundred point five")]
    public void ADecimalSwitchesTheWholePartToTheFullReading(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// And so does a grouping comma, for the same reason and by the same test: nothing writes a
    /// grouping comma into a designation, so a token wearing one has said it is a quantity.
    /// </summary>
    [Theory]
    [InlineData("6,680", "six thousand six hundred eighty")]
    [InlineData("1,234", "one thousand two hundred thirty-four")]
    [InlineData("12,345", "twelve thousand three hundred forty-five")]
    [InlineData("1,234.5", "one thousand two hundred thirty-four point five")]
    public void AGroupingCommaSwitchesItToo(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// The scale words, as far up as the ruling goes. d47 reports credits in billions, so stopping at
    /// thousands would have moved the defect rather than fixed it.
    /// </summary>
    [Theory]
    [InlineData("1,000", "one thousand")]
    [InlineData("2,000,000", "two million")]
    [InlineData("1,000,000,000", "one billion")]
    [InlineData("1,000,000,000,000", "one trillion")]
    [InlineData(
        "9,876,543,210",
        "nine billion eight hundred seventy-six million five hundred forty-three thousand "
        + "two hundred ten")]
    public void TheScaleWordsGoUpToTrillions(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    // ---- What the ruling deliberately leaves alone ---------------------------------------------

    /// <summary>
    /// A bare, unmeasured run of digits is read digit by digit here — the casual designation reading
    /// the Commander asked for is still given, but by <see cref="SpokenDesignations"/> at the seam
    /// rather than by <see cref="SpokenNumber"/> itself (#122).
    /// </summary>
    [Theory]
    [InlineData("385", "three eight five")]
    [InlineData("1985", "one nine eight five")]
    [InlineData("2637", "two six three seven")]
    [InlineData("128", "one two eight")]
    [InlineData("100", "one zero zero")]
    [InlineData("12", "one two")]
    public void ABareRunOfDigitsIsReadDigitByDigit(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>The casual reading a Commander actually hears, given at the seam rather than here.</summary>
    [Fact]
    public void TheSeamStillGivesTheCasualReading()
    {
        Assert.Equal("three eighty-five", SpokenDesignations.Rewrite("385"));

        // Through the pipeline as it actually runs: the unit rewrite happens first, so by the time the ladder
        // reads the number there is no "t" left to notice — only the word "tonnes".
        Assert.Equal("1234 tonnes", SpokenUnits.Rewrite("1234 t"));
    }

    /// <summary>
    /// A leading zero is part of a name whichever reading is asked for, so it is still said digit by
    /// digit.
    /// </summary>
    [Theory]
    [InlineData("007", "zero zero seven")]
    [InlineData("0.5", "zero point five")]
    public void ALeadingZeroIsStillPartOfTheName(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// The fraction is still said digit by digit, which the full reading does not reach: one thousand
    /// two hundred thirty-four point five zero and never point fifty.
    /// </summary>
    [Fact]
    public void TheFractionIsStillDigitByDigit()
    {
        Assert.Equal("one thousand two hundred thirty-four point five zero", SpokenNumber.Say("1234.50"));

        Assert.DoesNotContain("fifty", SpokenNumber.Say("1234.50"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A trailing point is a full stop rather than a decimal point, so it does not make the token a
    /// quantity — #177 ruled the point itself away, and this is the same ruling read at the other end.
    /// </summary>
    [Theory]
    [InlineData("1234.", "one two three four")]
    [InlineData("5.", "five")]
    public void ATrailingPointDoesNotMakeItAQuantity(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// Beyond trillions the digits are read out one at a time, which is the answer this rung has always
    /// given a run it cannot say as a number — never wrong, and the only readable one.
    /// </summary>
    [Fact]
    public void AFigureBeyondTheScaleWordsIsReadOut() =>
        Assert.Equal(
            "one two three four five six seven eight nine zero one two three four five six",
            SpokenNumber.Say("1,234,567,890,123,456"));

    // ---- Through the ladder --------------------------------------------------------------------

    /// <summary>
    /// The reported line, through the pipeline in the order it actually runs — the unit rewrite and
    /// then the ladder.
    /// </summary>
    [Fact]
    public void TheReportedLineSaysItsDistanceAsAQuantity()
    {
        var said = Rules.ToPhonemes(SpokenUnits.Rewrite("Deciat is 1234.5 ly out."));

        Assert.Contains("θˈaʊzənd", said, StringComparison.Ordinal);

        // And "light" is a word now rather than five letters, which is item 1 of the same issue meeting item
        // 4 in the one sentence that motivated both.
        Assert.Contains("laɪt", said, StringComparison.Ordinal);
        Assert.DoesNotContain(",", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The scale words are in the table, so a build whose dictionary never downloaded says them the
    /// same way — the reason every number word is written down here rather than left to the rules,
    /// which read thousand and then guess.
    /// </summary>
    [Theory]
    [InlineData("thousand")]
    [InlineData("million")]
    [InlineData("billion")]
    [InlineData("trillion")]
    public void EveryScaleWordIsInTheTable(string word) =>
        Assert.True(SpokenNumber.Sounds.ContainsKey(word), $"\"{word}\" is not in the table.");

    /// <summary>
    /// And every mark in a quantity is on a vowel, per the house rule — the theory lists in <see
    /// cref="TheStressMarkGoesBeforeTheVowelTests"/> carry the scale words and the measured lines, and
    /// this is the local restatement on the readings this ruling newly produces.
    /// </summary>
    [Theory]
    [InlineData("1234.5")]
    [InlineData("6,680")]
    [InlineData("9,876,543,210")]
    public void EveryMarkInAQuantityIsOnAVowel(string token)
    {
        var said = Rules.ToPhonemes(token);

        for (var i = said.IndexOfAny(['ˈ', 'ˌ']); i >= 0; i = said.IndexOfAny(['ˈ', 'ˌ'], i + 1))
        {
            Assert.True(
                i + 1 < said.Length
                && "æɛɪɑʌɔəieaouɜ".Contains(said[i + 1], StringComparison.Ordinal),
                $"\"{token}\" -> \"{said}\" marks a consonant at {i}.");
        }
    }
}
