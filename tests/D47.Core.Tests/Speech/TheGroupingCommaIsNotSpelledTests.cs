using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

public class TheGroupingCommaIsNotSpelledTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The reported number ------------------------------------------------------------------

    /// <summary>The reported token is a number's shape now.</summary>
    [Theory]
    [InlineData("6,680")]
    [InlineData("1,234")]
    [InlineData("12,345")]
    [InlineData("123,456")]
    [InlineData("1,234,567")]
    [InlineData("9,876,543,210")]
    public void AGroupedNumberIsANumbersShape(string token) =>
        Assert.True(SpokenNumber.Looks(token), $"\"{token}\" is not being read as a number.");

    /// <summary>
    /// And the combined form, which #183 named — a grouped whole part with a decimal fraction after it.
    /// </summary>
    [Theory]
    [InlineData("1,234.5")]
    [InlineData("6,680.25")]
    [InlineData("1,234,567.89")]
    public void TheCombinedFormIsANumbersShapeToo(string token) =>
        Assert.True(SpokenNumber.Looks(token), $"\"{token}\" is not being read as a number.");

    /// <summary>The comma adds no digits, which is the half of the ruling this issue carries.</summary>
    [Theory]
    [InlineData("1,234.5", "1234.5")]
    [InlineData("6,680.25", "6680.25")]
    [InlineData("6,680.0", "6680.0")]
    [InlineData("1,234,567.89", "1234567.89")]
    public void AGroupedNumberSaysWhatTheUngroupedOneSays(string grouped, string plain) =>
        Assert.Equal(SpokenNumber.Say(plain), SpokenNumber.Say(grouped));

    /// <summary>And it is not spelled any more, which is the defect written down.</summary>
    [Fact]
    public void TheGroupedNumberIsNoLongerSpelled()
    {
        var said = Rules.ToPhonemes("6,680");

        Assert.DoesNotContain(",", said, StringComparison.Ordinal);

        // Said the other way round: the digits are not read out one at a time. "six" appears once, and the
        // two sixes of the spelled reading were adjacent.
        Assert.DoesNotContain("sˈɪks sˈɪks", said, StringComparison.Ordinal);
    }

    /// <summary>The rung says so itself.</summary>
    [Theory]
    [InlineData("6,680")]
    [InlineData("1,234.5")]
    public void AGroupedNumberFallsOffTheNumberRung(string token)
    {
        var rungs = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(null, null, (segment, rung, _) => rungs.Add((segment, rung)))
            .ToPhonemes(token);

        Assert.Contains(rungs, fell => fell.Segment == token && fell.Rung == PhonemeRung.Number);
    }

    // ---- The validation, which is what keeps this honest --------------------------------------

    /// <summary>A comma every three, or it is not a grouping: a token that only looks like a number falls through to the ladder and is spelled, which is never wrong.</summary>
    [Theory]
    [InlineData("6,68")]        // the last group is short
    [InlineData("6,6800")]      // and long
    [InlineData("1,2345")]      // long by two
    [InlineData("12,34,567")]   // the Indian grouping, which this is not
    [InlineData("1234,567")]    // grouped from the wrong end
    [InlineData(",680")]        // no first group at all
    [InlineData("6,")]          // nor a second
    [InlineData("6,,680")]      // an empty group between two good ones
    [InlineData("1,234.5.6")]   // a grouped version number is still a version number
    [InlineData("1.234,5")]     // the continental spelling, which d47 never writes
    public void AMalformedGroupingStillFallsThrough(string token) =>
        Assert.False(SpokenNumber.Looks(token), $"\"{token}\" is being read as a number.");

    /// <summary>Said the other way round, on the shape that motivates the check.</summary>
    [Fact]
    public void AMalformedGroupingIsSpelledRatherThanStraightened()
    {
        var rungs = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(null, null, (segment, rung, _) => rungs.Add((segment, rung)))
            .ToPhonemes("6,68");

        Assert.Contains(rungs, fell => fell.Segment == "6,68" && fell.Rung == PhonemeRung.Spelled);
    }

    /// <summary>And a comma inside the fraction is not grouping anything.</summary>
    [Theory]
    [InlineData("1.234,567")]
    [InlineData("5.7,9")]
    public void ACommaInTheFractionIsNotAGrouping(string token) =>
        Assert.False(SpokenNumber.Looks(token), $"\"{token}\" is being read as a number.");

    /// <summary>Everything #177 already owned is untouched.</summary>
    [Theory]
    [InlineData("385")]
    [InlineData("5.79")]
    [InlineData("0.5")]
    [InlineData(".79")]
    [InlineData("5.")]
    [InlineData("007")]
    [InlineData("1234567890123")]
    public void TheShapesThisRungAlreadyOwnedAreStillOwned(string token) =>
        Assert.True(SpokenNumber.Looks(token), $"\"{token}\" is no longer being read as a number.");

    // ---- Through the ladder, where it was heard ------------------------------------------------

    /// <summary>A line of the kind that carried this.</summary>
    [Theory]
    [InlineData("That will be 6,680 credits.")]
    [InlineData("You have 1,234 tonnes aboard.")]
    [InlineData("Deciat is 1,234.5 out of range.")]
    public void ALineOfGroupedFiguresIsNotSpelled(string line) =>
        Assert.DoesNotContain(",", Rules.ToPhonemes(line), StringComparison.Ordinal);

    /// <summary>
    /// And the phrasing comma survives, which is the other half of the same claim: this changed what a
    /// comma means inside a token and nothing about one between them.
    /// </summary>
    [Fact]
    public void ACommaBetweenTokensIsStillPhrasing()
    {
        var said = Rules.ToPhonemes("Perez Ring, 6,680 credits");

        Assert.Single(said, character => character == ',');
    }

    // ---- The guard every rung that produces IPA answers to --------------------------------------

    /// <summary>The stress-mark guard, restated on what this issue added.</summary>
    [Theory]
    [InlineData("6,680")]
    [InlineData("1,234.5")]
    [InlineData("1,234,567")]
    public void EveryMarkInAGroupedNumberIsOnAVowel(string token)
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
