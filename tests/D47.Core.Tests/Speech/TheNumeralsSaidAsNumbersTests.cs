using System.Text.RegularExpressions;
using D47.Core.Knowledge;
using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Roman numerals said as numbers where the context says one is meant, and left alone everywhere
/// else (<a href="https://github.com/dseelinger/d47/issues/138">#138</a>).
/// <para>
/// <b>The local voice spelled them, and could not have done otherwise.</b> The ladder ends in
/// <em>anything left is spelled out</em>, and a roman numeral is letters with no digits and no
/// vowels — so <c>Cobra MkIII</c> came out <em>em kay eye eye eye</em>. 74 entries in the shipped
/// tables carry one, and each carries it into every armour row underneath it.
/// </para>
/// <para>
/// The half of this that is easy to get wrong is the other direction: <c>I</c>, <c>MIX</c>,
/// <c>DID</c>, <c>CIVIC</c> and <c>MILD</c> are English words made of numeral letters, and a general
/// rule would turn ordinary prose into numbers in a voice — which is the worst place to find out.
/// </para>
/// </summary>
public class TheNumeralsSaidAsNumbersTests
{
    private static readonly Phonemiser Rules = new();

    // ---- The two spellings ----------------------------------------------------------------

    /// <summary>
    /// <b>Both spellings, and they must agree.</b> They differ by a space, and the segment splitter
    /// splits on whitespace — so the joined form arrives as one segment and the spaced form as two,
    /// which is why the rule cannot live in the per-segment ladder.
    /// </summary>
    [Theory]
    [InlineData("Cobra MkIII", "Cobra Mark three")]
    [InlineData("Cobra Mk III", "Cobra Mark three")]
    [InlineData("Kestrel Mk II", "Kestrel Mark two")]
    [InlineData("Krait MkII", "Krait Mark two")]
    [InlineData("Cobra MkIV", "Cobra Mark four")]
    [InlineData("Cobra MkV", "Cobra Mark five")]
    [InlineData("Panther Clipper Mk II", "Panther Clipper Mark two")]
    public void AMarkNumberIsSaidAsAMarkAndACardinal(string written, string expected) =>
        Assert.Equal(expected, SpokenNumerals.Expand(written));

    /// <summary>
    /// <b>The two spellings produce the same sound</b>, which is the assertion that matters — the
    /// text above is an implementation detail and the phonemes are what a Commander hears.
    /// </summary>
    [Fact]
    public void TheJoinedAndSpacedSpellingsSoundIdentical() =>
        Assert.Equal(Rules.ToPhonemes("Cobra MkIII"), Rules.ToPhonemes("Cobra Mk III"));

    /// <summary>
    /// And the sound is a mark and a number rather than four letters. Asserted through the ladder,
    /// because that is where it was wrong.
    /// </summary>
    [Fact]
    public void TheLocalVoiceNoLongerSpellsAMarkNumber()
    {
        var said = Rules.ToPhonemes("Cobra MkIII");

        // "em kay" and "eye eye eye" are what it used to be.
        Assert.DoesNotContain("ˈɛm", said, StringComparison.Ordinal);
        Assert.DoesNotContain("keɪ", said, StringComparison.Ordinal);

        // "three" is what it is now.
        Assert.Contains("θɹiː", said, StringComparison.Ordinal);
    }

    /// <summary>It is a cardinal, not an ordinal: <em>Mark Three</em>, never <em>Mark the Third</em>.</summary>
    [Fact]
    public void TheNumeralIsACardinal()
    {
        var said = SpokenNumerals.Expand("Cobra MkIII");

        Assert.DoesNotContain("third", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("the", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Punctuation around it survives, so a hull at the end of a sentence still stops.</summary>
    [Theory]
    [InlineData("Cobra MkIII.", "Cobra Mark three.")]
    [InlineData("(Cobra MkIII)", "(Cobra Mark three)")]
    [InlineData("Kestrel Mk II,", "Kestrel Mark two,")]
    [InlineData("Cobra Mk. III", "Cobra Mark three")]
    public void ThePunctuationAroundItIsKept(string written, string expected) =>
        Assert.Equal(expected, SpokenNumerals.Expand(written));

    // ---- The other family ------------------------------------------------------------------

    /// <summary>
    /// <b>Sudarsky classes, which are said on scans</b> — the other place d47 talks constantly.
    /// Elite writes <c>Sudarsky class I gas giant</c> and <see cref="BodyCatalogue"/> holds
    /// <c>Class I gas giant</c> as a spoken name.
    /// </summary>
    [Theory]
    [InlineData("Class I gas giant", "Class one gas giant")]
    [InlineData("Class II gas giant", "Class two gas giant")]
    [InlineData("Class III gas giant", "Class three gas giant")]
    [InlineData("Class IV gas giant", "Class four gas giant")]
    [InlineData("Class V gas giant", "Class five gas giant")]
    [InlineData("Sudarsky class I gas giant", "Sudarsky class one gas giant")]
    public void AGasGiantsClassIsSaidAsANumber(string written, string expected) =>
        Assert.Equal(expected, SpokenNumerals.Expand(written));

    /// <summary>
    /// <b>And "class" alone is not the context, deliberately.</b> On its own it would convert
    /// <i>"the class I attended"</i>, which is a sentence a persona could easily say and which would
    /// come out <i>"the class one attended"</i>. The whole population is the five Sudarsky bodies
    /// and every one of them is followed by <c>gas</c>, so requiring it loses nothing real and
    /// closes the only false positive this context has.
    /// </summary>
    [Theory]
    [InlineData("the class I attended")]
    [InlineData("a masterclass I enjoyed")]
    [InlineData("Class I remember well")]
    public void ClassFollowedByAPronounIsNotAClassNumber(string prose) =>
        Assert.Equal(prose, SpokenNumerals.Expand(prose));

    // ---- What must never be converted -------------------------------------------------------

    /// <summary>
    /// <b>The failure that would be found in a headset rather than in a test.</b> Every one of these
    /// is made only of numeral letters, and three of them parse as some number or other under a
    /// naive reading. In ordinary prose none of them is a numeral.
    /// </summary>
    [Theory]
    [InlineData("I")]
    [InlineData("MIX")]
    [InlineData("DID")]
    [InlineData("CIVIC")]
    [InlineData("MILD")]
    [InlineData("LIVID")]
    [InlineData("DIM")]
    [InlineData("LID")]
    public void AWordMadeOfNumeralLettersIsNotConvertedInProse(string word)
    {
        Assert.Equal(word, SpokenNumerals.Expand(word));
        Assert.Equal($"a {word} thing", SpokenNumerals.Expand($"a {word} thing"));

        // And with a context word in the same line but not in front of it, which is the shape a
        // whole-line rule would get wrong.
        Assert.Equal($"{word} Mk", SpokenNumerals.Expand($"{word} Mk"));
    }

    /// <summary>
    /// <b>Only three of the eight above are even well-formed numerals</b>, and the round trip is
    /// what says so: parsing <c>MILD</c> gives 1449, and 1449 renders as <c>MCDXLIX</c>, which is
    /// not what arrived.
    /// </summary>
    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    [InlineData("V", 5)]
    [InlineData("MIX", 1009)]
    public void AWellFormedNumeralParses(string numeral, int value) =>
        Assert.Equal(value, SpokenNumerals.Value(numeral));

    [Theory]
    [InlineData("MILD")]
    [InlineData("DID")]
    [InlineData("CIVIC")]
    [InlineData("LIVID")]
    [InlineData("IIII")]
    [InlineData("VV")]
    [InlineData("IC")]
    [InlineData("iii")]
    [InlineData("")]
    [InlineData("Cobra")]
    public void AnythingNotACanonicalNumeralIsNotOne(string text) =>
        Assert.Null(SpokenNumerals.Value(text));

    /// <summary>
    /// <b>Bare <c>I</c> after the written-out <c>Mark</c> is excluded, and only there.</b>
    /// <i>"Mark I saw him"</i> is an English sentence and <i>"Mark one saw him"</i> is what
    /// converting it would produce. Every mark number in the shipped tables is II or higher, so the
    /// exclusion costs nothing — and <c>Mk</c>, which is not a word at all, needs no such guard.
    /// </summary>
    [Fact]
    public void MarkOneIsLeftAloneWhereMkOneIsNot()
    {
        Assert.Equal("Mark I saw him", SpokenNumerals.Expand("Mark I saw him"));
        Assert.Equal("Mark II", SpokenNumerals.Expand("Mark II"));
        Assert.Equal("Mark one", SpokenNumerals.Expand("Mk I"));
    }

    /// <summary>
    /// A word the dictionary holds is unaffected: this runs before the ladder and hands it ordinary
    /// English, so nothing it emits takes a different road through the rungs below.
    /// </summary>
    [Fact]
    public void AWordTheDictionaryHoldsIsUnaffected()
    {
        const string line = "The station is closed and the market is quiet.";

        Assert.Equal(line, SpokenNumerals.Expand(line));
        Assert.Equal(Rules.ToPhonemes(line), Rules.ToPhonemes(SpokenNumerals.Expand(line)));
    }

    /// <summary>A line with nothing to do comes back byte for byte, spacing included.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Docking  request   granted.")]
    public void ALineWithNothingToDoIsUntouched(string line) =>
        Assert.Equal(line, SpokenNumerals.Expand(line));

    // ---- The whole shipped population --------------------------------------------------------

    /// <summary>
    /// <b>The 74 table entries asserted as a set, so a new hull with a new spelling fails the suite
    /// rather than being discovered by ear.</b> Read out of the shipped catalogues rather than
    /// listed here, which is the point: a list written down beside the rule is a list that goes
    /// stale the first time Frontier adds a ship.
    /// </summary>
    [Fact]
    public void EveryMarkNumberInTheShippedTablesIsSaidAsANumber()
    {
        var carrying = EliteSpecifications.Ships.Select(ship => ship.Name)
            .Concat(EliteSpecifications.Modules.Select(module => module.Name))
            .Concat(BodyCatalogue.Subtypes)
            .Where(name => Regex.IsMatch(name, @"\bMk\.?\s?[IVXLCDM]+\b"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The population is real, so an empty match set cannot make this pass by finding nothing.
        Assert.NotEmpty(carrying);

        foreach (var name in carrying)
        {
            var said = SpokenNumerals.Expand(name);

            Assert.DoesNotContain("Mk", said, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Mark ", said, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// And the same for the gas giants, which are the other family and come from a different
    /// catalogue.
    /// </summary>
    [Fact]
    public void EveryGasGiantClassInTheShippedNamesIsSaidAsANumber()
    {
        var classes = BodyCatalogue.Subtypes
            .Where(name => Regex.IsMatch(name, @"\bClass\s+[IVXLCDM]+\b"))
            .ToList();

        Assert.NotEmpty(classes);

        foreach (var name in classes)
        {
            Assert.DoesNotMatch(new Regex(@"\bClass\s+[IVXLCDM]+\b"), SpokenNumerals.Expand(name));
        }
    }
}
