using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>The ladder that turns a name into sounds without guessing.</summary>
public class TheNameLadderTests
{
    private static readonly Phonemiser Rules = new();

    /// <summary>The Commander's own example, and the reason the ladder has the shape it has.</summary>
    [Fact]
    public void TheReportedExampleGoesDownTheLadderAsRuled()
    {
        // Fed to Phonemiser directly rather than through SpeechPipeline, so this exercises the ladder
        // below the seam on purpose: production text has already had SpokenDesignations rewrite its
        // designation numbers to words by the time it reaches here (#122).
        var said = Rules.ToPhonemes("COL 385 SECTOR B0-GQPI");

        // COL parses as one syllable and is pronounced rather than spelled.
        Assert.DoesNotContain("siː, oʊ, ˈɛl", said, StringComparison.Ordinal);

        // 385 falls off the Number rung, and below the seam that is the digit-by-digit reading —
        // never wrong, which is what a caller who bypassed the seam gets.
        Assert.Contains("θɹˈiː", said, StringComparison.Ordinal);
        Assert.Contains("ˈeɪt", said, StringComparison.Ordinal);

        // B0 is letters and digits with nothing between them, so it is spelled.
        Assert.Contains("biː, zˈiəɹoʊ", said, StringComparison.Ordinal);

        // GQPI cannot begin an English syllable and has no vowel, so it is spelled.
        Assert.Contains("dʒiː, kjuː, piː, aɪ", said, StringComparison.Ordinal);

        // And the dash between two spelled segments is voiced.
        Assert.Contains("dˈæʃ", said, StringComparison.Ordinal);
    }

    /// <summary>The ruling that pays for itself on the first jump.</summary>
    [Fact]
    public void DezhraIsPronouncedRatherThanSpelled()
    {
        Assert.True(Phonotactics.IsSayable("Dezhra"));

        var syllables = Phonotactics.Syllabify("Dezhra");

        Assert.Equal(2, syllables.Count);
        Assert.Equal("zh", syllables[0].Coda);

        var said = Rules.ToPhonemes("Shinrarta Dezhra");

        // The ʒ is what this checks.
        Assert.Contains("ʒ", said, StringComparison.Ordinal);
        Assert.DoesNotContain("zɛd", said, StringComparison.Ordinal);
        Assert.DoesNotContain("ziː", said, StringComparison.Ordinal);
    }

    /// <summary>The gate itself, on the cases that decide it.</summary>
    [Theory]
    [InlineData("COL", true)]
    [InlineData("Lave", true)]
    [InlineData("Kusauts", true)]
    [InlineData("Shinrarta", true)]
    [InlineData("Dezhra", true)]
    [InlineData("Sol", true)]
    [InlineData("Deciat", true)]
    [InlineData("GQPI", false)]
    [InlineData("XYZ", false)]
    [InlineData("BKM", false)]
    [InlineData("TZW", false)]
    public void SayableIsDecidedByParsingRatherThanByGuessing(string word, bool sayable) =>
        Assert.Equal(sayable, Phonotactics.IsSayable(word));

    /// <summary>
    /// Zed, for a voice that is not American — the Commander's ruling, and it costs one entry because
    /// Kokoro's voice ids carry the accent in their prefix.
    /// </summary>
    [Fact]
    public void ABritishVoiceSaysZed()
    {
        Assert.Equal("zɛd", SpokenLetters.Say('z', SpeechAccent.British));
        Assert.Equal("ziː", SpokenLetters.Say('z', SpeechAccent.American));

        Assert.Equal(SpeechAccent.British, SpokenLetters.AccentOf("bm_george"));
        Assert.Equal(SpeechAccent.British, SpokenLetters.AccentOf("bf_emma"));
        Assert.Equal(SpeechAccent.American, SpokenLetters.AccentOf("af_heart"));
        Assert.Equal(SpeechAccent.American, SpokenLetters.AccentOf("am_michael"));

        // And it reaches the whole way through, which is the half a table alone would not prove.
        Assert.Contains("zɛd", Rules.ToPhonemes("XYZ", "bm_george"), StringComparison.Ordinal);
        Assert.Contains("ziː", Rules.ToPhonemes("XYZ", "af_heart"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Designation numbers are said the way somebody reads them aloud — three eighty-five, not three
    /// hundred and eighty-five — but that casual reading is <see cref="SpokenDesignations"/>'s job now,
    /// applied at the seam ahead of every voice (#91). <see cref="SpokenNumber.Say"/> itself no longer
    /// tries to be clever about an unmeasured run: digit by digit is never wrong, and it is the reading
    /// a caller gets if it reaches this rung without having gone through the seam (#122).
    /// </summary>
    [Theory]
    [InlineData("385", "three eight five")]
    [InlineData("12", "one two")]
    [InlineData("7", "seven")]
    [InlineData("100", "one zero zero")]
    public void AnUnmeasuredRunIsReadDigitByDigit(string digits, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(digits));

    /// <summary>
    /// The casual, length-aware reading of a designation lives at the seam — the #91 ruling: three or
    /// fewer digits stay casual, four or more are read one at a time.
    /// </summary>
    [Fact]
    public void TheSeamGivesTheCasualReadingByLength()
    {
        Assert.Equal("three eighty-five", SpokenDesignations.Rewrite("385"));
        Assert.Equal("one nine eight five", SpokenDesignations.Rewrite("1985"));
    }

    /// <summary>
    /// A three-digit boxel coordinate keeps the casual reading under the same rule, with no special
    /// case for the boxel shape — the one thing #91 flagged as worth hearing before ruling it right.
    /// </summary>
    [Fact]
    public void AThreeDigitBoxelCoordinateStaysCasual() =>
        Assert.Equal(
            "Col two eighty-five Sector RE-Q d five-one thirty-two",
            SpokenDesignations.Rewrite("Col 285 Sector RE-Q d5-132"));

    /// <summary>A leading zero is part of a designation rather than a quantity, so it is kept and said.</summary>
    [Fact]
    public void LeadingZerosAreSaidRatherThanDropped() =>
        Assert.Equal("zero zero seven", SpokenNumber.Say("007"));

    /// <summary>The refinement taken while building, and it is why ordinary prose survives the ladder.</summary>
    [Fact]
    public void ADashIsSpokenOnlyBesideSomethingSpelled()
    {
        Assert.DoesNotContain("dˈæʃ", Rules.ToPhonemes("well-known"), StringComparison.Ordinal);
        Assert.DoesNotContain("dˈæʃ", Rules.ToPhonemes("re-entry"), StringComparison.Ordinal);

        Assert.Contains("dˈæʃ", Rules.ToPhonemes("B0-GQPI"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Punctuation is carried through rather than dropped: Kokoro reads it as phrasing, and a line
    /// stripped of its commas is said in one breath.
    /// </summary>
    [Fact]
    public void PunctuationSurvives()
    {
        var said = Rules.ToPhonemes("Docking granted, Commander.");

        Assert.Contains(",", said, StringComparison.Ordinal);
        Assert.EndsWith(".", said, StringComparison.Ordinal);
    }

    /// <summary>The dictionary wins where it holds a word, because it is exact and the rules are not.</summary>
    [Fact]
    public void TheDictionaryOutranksTheRules()
    {
        var withDictionary = new Phonemiser(new OneWord("through", "θɹuː"));

        Assert.Equal("θɹuː", withDictionary.ToPhonemes("through"));
        Assert.NotEqual("θɹuː", Rules.ToPhonemes("through"));
    }

    private sealed class OneWord(string word, string ipa) : IPronunciationDictionary
    {
        public string? Lookup(string looked) =>
            string.Equals(looked, word, StringComparison.OrdinalIgnoreCase) ? ipa : null;
    }
}
