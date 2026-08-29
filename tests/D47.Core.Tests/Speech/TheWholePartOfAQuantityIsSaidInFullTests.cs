using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// <b>The ruling #184 asked for, taken on 2026-08-29</b>
/// (<a href="https://github.com/dseelinger/d47/issues/184">#184</a>, item 4).
/// <para>
/// <b>The question.</b> A run of digits has been read casually since the Commander ruled it —
/// <c>385</c> is <em>three eighty-five</em>, <c>1985</c> is <em>nineteen eighty-five</em> — which
/// is how anybody reads a designation aloud and is shorter, and a system name carries three of
/// them. That reading followed the digits into places that are not designations: <c>1234.5 ly</c>
/// was said <em>twelve thirty-four point five</em>, correct by the existing ruling and arguably not
/// what is wanted once a decimal has marked the token as a measurement.
/// </para>
/// <para>
/// <b>The ruling: the token's own punctuation decides, and nothing else does.</b> A decimal point
/// with digits after it, or a grouping comma, makes it a measured quantity, and its whole part
/// takes the full reading. Bare digits keep the casual designation reading, unchanged.
/// </para>
/// <para>
/// <b>Why the punctuation and not the unit word</b>, which #184 offered as the other candidate:
/// <see cref="SpokenUnits"/> (#155) rewrites <c>ly</c> to <em>light years</em> in the speech
/// pipeline <em>before</em> the ladder sees a token, so the abbreviation is already gone by the
/// time this rung is reached and what follows the number is two ordinary English words. Keying on
/// it would mean running this decision ahead of that rewrite, or keeping a second list of unit
/// words here — and a second list is a second thing to disagree with the first. The punctuation is
/// on the token, so the decision is decidable from the token alone.
/// </para>
/// <para>
/// <b>What it costs, stated so it can be overturned cheaply.</b> A bare quantity with a unit after
/// it keeps the casual reading — <c>1234 tonnes</c> is still <em>twelve thirty-four tonnes</em>.
/// That is the case this gets arguably wrong, and it is one predicate away from being got the other
/// way.
/// </para>
/// </summary>
public class TheWholePartOfAQuantityIsSaidInFullTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The reported reading -----------------------------------------------------------------

    /// <summary>
    /// <b>The token from the issue.</b> <c>1234.5</c> was <em>twelve thirty-four point five</em>,
    /// which is a year and a fraction rather than a distance.
    /// </summary>
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
    /// <b>And so does a grouping comma</b>, for the same reason and by the same test: nothing
    /// writes a grouping comma into a designation, so a token wearing one has said it is a
    /// quantity. This is the reading #183 deliberately left to this issue.
    /// </summary>
    [Theory]
    [InlineData("6,680", "six thousand six hundred eighty")]
    [InlineData("1,234", "one thousand two hundred thirty-four")]
    [InlineData("12,345", "twelve thousand three hundred forty-five")]
    [InlineData("1,234.5", "one thousand two hundred thirty-four point five")]
    public void AGroupingCommaSwitchesItToo(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// <b>The scale words, as far up as the ruling goes.</b> d47 reports credits in billions, so
    /// stopping at thousands would have moved the defect rather than fixed it.
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
    /// <b>A bare run of digits keeps the casual designation reading</b>, which is the Commander's
    /// own ruling and the half of this that must not move. <c>COL 385 SECTOR B0-GQPI</c> is the
    /// reason it exists, and a designation is never written with a comma or a point in it.
    /// </summary>
    [Theory]
    [InlineData("385", "three eighty-five")]
    [InlineData("1985", "nineteen eighty-five")]
    [InlineData("2637", "twenty-six thirty-seven")]
    [InlineData("128", "one twenty-eight")]
    [InlineData("100", "one hundred")]
    [InlineData("12", "twelve")]
    public void ABareRunOfDigitsIsStillReadCasually(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// <b>The cost of the ruling, asserted rather than left implied.</b> A bare quantity with a
    /// unit after it keeps the casual reading, because the unit is not something this rung can see.
    /// This test is here to be <em>changed</em> if the Commander overrules the ruling — it is the
    /// line that says what the decision buys and what it does not.
    /// </summary>
    [Fact]
    public void ABareQuantityWithAUnitKeepsTheCasualReading()
    {
        Assert.Equal("twelve thirty-four", SpokenNumber.Say("1234"));

        // Through the pipeline as it actually runs: the unit rewrite happens first, so by the time
        // the ladder reads the number there is no "t" left to notice — only the word "tonnes".
        Assert.Equal("1234 tonnes", SpokenUnits.Rewrite("1234 t"));
    }

    /// <summary>
    /// <b>A leading zero is part of a name whichever reading is asked for</b>, so it is still said
    /// digit by digit. A quantity is not written with one, and dropping them says a different
    /// designation.
    /// </summary>
    [Theory]
    [InlineData("007", "zero zero seven")]
    [InlineData("0.5", "zero point five")]
    public void ALeadingZeroIsStillPartOfTheName(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// <b>The fraction is still said digit by digit</b>, which the full reading does not reach:
    /// <em>one thousand two hundred thirty-four point five zero</em> and never <em>point fifty</em>.
    /// That is #177's ruling and it parts company with the whole part in every English dialect.
    /// </summary>
    [Fact]
    public void TheFractionIsStillDigitByDigit()
    {
        Assert.Equal("one thousand two hundred thirty-four point five zero", SpokenNumber.Say("1234.50"));

        Assert.DoesNotContain("fifty", SpokenNumber.Say("1234.50"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A trailing point is a full stop rather than a decimal point</b>, so it does not make the
    /// token a quantity — #177 ruled the point itself away, and this is the same ruling read at the
    /// other end. <c>5.</c> at the end of a sentence is the number five.
    /// </summary>
    [Theory]
    [InlineData("1234.", "twelve thirty-four")]
    [InlineData("5.", "five")]
    public void ATrailingPointDoesNotMakeItAQuantity(string written, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(written));

    /// <summary>
    /// <b>Beyond trillions the digits are read out one at a time</b>, which is the answer this rung
    /// has always given a run it cannot say as a number — never wrong, and the only readable one.
    /// </summary>
    [Fact]
    public void AFigureBeyondTheScaleWordsIsReadOut() =>
        Assert.Equal(
            "one two three four five six seven eight nine zero one two three four five six",
            SpokenNumber.Say("1,234,567,890,123,456"));

    // ---- Through the ladder --------------------------------------------------------------------

    /// <summary>
    /// <b>The reported line, through the pipeline in the order it actually runs</b> — the unit
    /// rewrite and then the ladder. This is the line #184 quoted, and the number in it now says
    /// what a distance says.
    /// </summary>
    [Fact]
    public void TheReportedLineSaysItsDistanceAsAQuantity()
    {
        var said = Rules.ToPhonemes(SpokenUnits.Rewrite("Deciat is 1234.5 ly out."));

        Assert.Contains("θˈaʊzənd", said, StringComparison.Ordinal);

        // And "light" is a word now rather than five letters, which is item 1 of the same issue
        // meeting item 4 in the one sentence that motivated both.
        Assert.Contains("laɪt", said, StringComparison.Ordinal);
        Assert.DoesNotContain(",", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The scale words are in the table</b>, so a build whose dictionary never downloaded says
    /// them the same way — the reason every number word is written down here rather than left to
    /// the rules, which read <em>thousand</em> and then guess.
    /// </summary>
    [Theory]
    [InlineData("thousand")]
    [InlineData("million")]
    [InlineData("billion")]
    [InlineData("trillion")]
    public void EveryScaleWordIsInTheTable(string word) =>
        Assert.True(SpokenNumber.Sounds.ContainsKey(word), $"\"{word}\" is not in the table.");

    /// <summary>
    /// <b>And every mark in a quantity is on a vowel</b>, per the house rule — the theory lists in
    /// <see cref="TheStressMarkGoesBeforeTheVowelTests"/> carry the scale words and the measured
    /// lines, and this is the local restatement on the readings this ruling newly produces.
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
