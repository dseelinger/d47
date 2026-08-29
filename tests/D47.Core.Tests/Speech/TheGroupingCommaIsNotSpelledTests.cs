using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The grouping comma (<a href="https://github.com/dseelinger/d47/issues/183">#183</a>).
/// <para>
/// <b>The same fault as #177, one character over, and found by #177's own lane.</b> <c>6,680</c>
/// is not all digits and has no letters in it, so it fell past the number rung to the bottom of the
/// ladder — and where the decimal point was silently dropped there, the comma was <em>kept</em>,
/// because the spelling rung uses a comma to write the pause between spelled letters. So the
/// Commander heard <em>"six six eight zero"</em>, read out one digit at a time with a pause where
/// the grouping had been.
/// </para>
/// <para>
/// <b>It is most of where large numbers appear.</b> d47 writes grouping commas into credits,
/// tonnages, distances and market quantities, which is everywhere a figure gets big enough to be
/// worth grouping — so this was wrong in exactly the places a number is hardest to follow by ear.
/// </para>
/// <para>
/// <b>What this file does not assert is the reading</b>, deliberately. Whether <c>6,680</c> is
/// <em>sixty-six eighty</em> or <em>six thousand six hundred eighty</em> is
/// <a href="https://github.com/dseelinger/d47/issues/184">#184</a>'s ruling, not this issue's. What
/// #183 is about is that the comma is grouping rather than content — so the assertion here is that
/// a grouped number says exactly what the same digits ungrouped say, which is true on both sides of
/// that ruling and does not have to be rewritten when it lands.
/// </para>
/// </summary>
public class TheGroupingCommaIsNotSpelledTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The reported number ------------------------------------------------------------------

    /// <summary>
    /// <b>The reported token is a number's shape now.</b> This is the whole of the fix stated as
    /// one assertion: the rung that says numbers is the rung that owns it.
    /// </summary>
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
    /// <b>And the combined form, which #183 named</b> — a grouped whole part with a decimal
    /// fraction after it. Both of #177's and #183's shapes in one token, which is what a distance
    /// or a tonnage actually looks like once it is large.
    /// </summary>
    [Theory]
    [InlineData("1,234.5")]
    [InlineData("6,680.25")]
    [InlineData("1,234,567.89")]
    public void TheCombinedFormIsANumbersShapeToo(string token) =>
        Assert.True(SpokenNumber.Looks(token), $"\"{token}\" is not being read as a number.");

    /// <summary>
    /// <b>The comma says nothing, which is the ruling this issue actually carries.</b> A grouped
    /// number is the ungrouped number: the commas describe the digits rather than adding to them.
    /// <para>
    /// Asserted this way round on purpose. It is true whichever reading #184 settles on for the
    /// whole part, so it pins the fix without pinning a decision that is not this issue's to make.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("6,680", "6680")]
    [InlineData("1,234", "1234")]
    [InlineData("1,234,567", "1234567")]
    [InlineData("1,234.5", "1234.5")]
    [InlineData("6,680.25", "6680.25")]
    public void AGroupedNumberSaysWhatTheUngroupedOneSays(string grouped, string plain) =>
        Assert.Equal(SpokenNumber.Say(plain), SpokenNumber.Say(grouped));

    /// <summary>
    /// <b>And it is not spelled any more, which is the defect written down.</b> The spelling rung
    /// joins its letters with commas — that is how Kokoro is told to pause between them — so a
    /// comma left in the phonemes is precisely what <em>"six six eight zero"</em> sounded like.
    /// </summary>
    [Fact]
    public void TheGroupedNumberIsNoLongerSpelled()
    {
        var said = Rules.ToPhonemes("6,680");

        Assert.DoesNotContain(",", said, StringComparison.Ordinal);

        // Said the other way round: the digits are not read out one at a time. "six" appears once,
        // and the two sixes of the spelled reading were adjacent.
        Assert.DoesNotContain("sˈɪks sˈɪks", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The rung says so itself.</b> One debug line naming the rung is the whole diagnosis
    /// (<see cref="PhonemeRung"/>), so the fix is asserted where a reader of the trace would look:
    /// the grouped number comes off <see cref="PhonemeRung.Number"/> rather than
    /// <see cref="PhonemeRung.Spelled"/>.
    /// </summary>
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

    /// <summary>
    /// <b>A comma every three, or it is not a grouping.</b> #183 asked for this by name, and it is
    /// the difference between owning a shape and guessing at one: a token that only looks like a
    /// number falls through to the ladder and is spelled, which is never wrong, rather than being
    /// read as though somebody had meant something else.
    /// </summary>
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

    /// <summary>
    /// <b>Said the other way round, on the shape that motivates the check.</b> A malformed token is
    /// spelled rather than silently straightened — <c>6,68</c> is not quietly read as
    /// <em>six sixty-eight</em>, because nobody wrote that.
    /// </summary>
    [Fact]
    public void AMalformedGroupingIsSpelledRatherThanStraightened()
    {
        var rungs = new List<(string Segment, PhonemeRung Rung)>();

        new Phonemiser(null, null, (segment, rung, _) => rungs.Add((segment, rung)))
            .ToPhonemes("6,68");

        Assert.Contains(rungs, fell => fell.Segment == "6,68" && fell.Rung == PhonemeRung.Spelled);
    }

    /// <summary>
    /// <b>And a comma inside the fraction is not grouping anything.</b> Nothing groups the digits
    /// after a decimal point in any convention d47 writes, so the shape is refused rather than
    /// given a reading.
    /// </summary>
    [Theory]
    [InlineData("1.234,567")]
    [InlineData("5.7,9")]
    public void ACommaInTheFractionIsNotAGrouping(string token) =>
        Assert.False(SpokenNumber.Looks(token), $"\"{token}\" is being read as a number.");

    /// <summary>
    /// <b>Everything #177 already owned is untouched.</b> Restated here because #183 rewrote the
    /// shape test wholesale rather than adding a clause to it, and a rewritten predicate is exactly
    /// where an old case goes quietly missing.
    /// </summary>
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

    /// <summary>
    /// <b>A line of the kind that carried this.</b> Credits and tonnages are where d47 writes
    /// grouping commas, and a whole line is what a Commander actually hears — so the assertion is
    /// that no part of it is being spelled. None of these lines carries a phrasing comma of its
    /// own, so any comma in the phonemes is a spelled one.
    /// </summary>
    [Theory]
    [InlineData("That will be 6,680 credits.")]
    [InlineData("You have 1,234 tonnes aboard.")]
    [InlineData("Deciat is 1,234.5 out of range.")]
    public void ALineOfGroupedFiguresIsNotSpelled(string line) =>
        Assert.DoesNotContain(",", Rules.ToPhonemes(line), StringComparison.Ordinal);

    /// <summary>
    /// <b>And the phrasing comma survives</b>, which is the other half of the same claim: this
    /// changed what a comma means <em>inside</em> a token and nothing about one between them.
    /// Kokoro reads that one as a breath, and a line stripped of its commas is said in one.
    /// </summary>
    [Fact]
    public void ACommaBetweenTokensIsStillPhrasing()
    {
        var said = Rules.ToPhonemes("Perez Ring, 6,680 credits");

        Assert.Single(said, character => character == ',');
    }

    // ---- The guard every rung that produces IPA answers to --------------------------------------

    /// <summary>
    /// <b>The stress-mark guard, restated on what this issue added.</b> The house rule is that every
    /// rung producing IPA extends the theory lists in
    /// <see cref="TheStressMarkGoesBeforeTheVowelTests"/> rather than growing a second guard beside
    /// them — those lists carry the grouped cases now, and this is the local restatement on the
    /// tokens #183 sends to a rung they were not reaching before.
    /// </summary>
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
