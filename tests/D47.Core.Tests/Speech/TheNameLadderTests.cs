using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The ladder that turns a name into sounds without guessing (Phase 59).
/// <para>
/// <b>This exists because the alternative measured zero.</b> Kokoro takes phonemes and has no text
/// path, and the neural grapheme-to-phoneme model scored <b>0.0% exact</b> on words drawn from its
/// own training dictionary. Elite has 400 billion system names, so the tail cannot be covered by
/// extending a dictionary — it has to be decided by rule.
/// </para>
/// </summary>
public class TheNameLadderTests
{
    private static readonly Phonemiser Rules = new();

    /// <summary>
    /// <b>The Commander's own example, and the reason the ladder has the shape it has.</b> Asked
    /// for on 2026-08-28: <c>COL 385 SECTOR B0-GQPI</c> should come out roughly <em>call three
    /// eighty-five sector bee zero dash gee queue pee eye</em>.
    /// <para>
    /// Asserted as the five decisions rather than as one exact string, because the IPA for
    /// <em>call</em> is a detail of the rules and the decisions are the design: which segments are
    /// pronounced, which are spelled, and where the dash is voiced.
    /// </para>
    /// </summary>
    [Fact]
    public void TheReportedExampleGoesDownTheLadderAsRuled()
    {
        var said = Rules.ToPhonemes("COL 385 SECTOR B0-GQPI");

        // COL parses as one syllable and is pronounced rather than spelled.
        Assert.DoesNotContain("siː, oʊ, ˈɛl", said, StringComparison.Ordinal);

        // 385 is said casually, as a person reads a designation.
        Assert.Contains("θɹiː", said, StringComparison.Ordinal);
        Assert.Contains("eɪti", said, StringComparison.Ordinal);

        // B0 is letters and digits with nothing between them, so it is spelled.
        Assert.Contains("biː, ˈzɪɹoʊ", said, StringComparison.Ordinal);

        // GQPI cannot begin an English syllable and has no vowel, so it is spelled.
        Assert.Contains("dʒiː, kjuː, piː, aɪ", said, StringComparison.Ordinal);

        // And the dash between two spelled segments is voiced.
        Assert.Contains("dˈæʃ", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The ruling that pays for itself on the first jump.</b> <c>zh</c> is not English spelling,
    /// every English speaker says it in <em>measure</em>, and the Commander ruled on 2026-08-28
    /// that sounds an English speaker can make are spoken rather than spelled.
    /// <para>
    /// It costs nothing but an entry in the coda inventory: <c>Dezhra</c> then parses as
    /// <c>dezh-ra</c> with no special case anywhere. Without it, the most-spoken system name in the
    /// game would be read out letter by letter.
    /// </para>
    /// </summary>
    [Fact]
    public void DezhraIsPronouncedRatherThanSpelled()
    {
        Assert.True(Phonotactics.IsSayable("Dezhra"));

        var syllables = Phonotactics.Syllabify("Dezhra");

        Assert.Equal(2, syllables.Count);
        Assert.Equal("zh", syllables[0].Coda);

        var said = Rules.ToPhonemes("Shinrarta Dezhra");

        // The ʒ is the whole point. If it is being spelled, "zed" or "zee" appears instead.
        Assert.Contains("ʒ", said, StringComparison.Ordinal);
        Assert.DoesNotContain("zɛd", said, StringComparison.Ordinal);
        Assert.DoesNotContain("ziː", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The gate itself, on the cases that decide it. A word is sayable when its letters parse into
    /// legal syllables, which is a closed question rather than an opinion.
    /// </summary>
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
    /// <b>Zed, for a voice that is not American</b> — the Commander's ruling, and it costs one
    /// entry because Kokoro's voice ids carry the accent in their prefix.
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
    /// Numbers are said the way somebody reads a designation aloud, which the Commander stated
    /// outright: <em>three eighty-five</em>, not <em>three hundred and eighty-five</em>.
    /// </summary>
    [Theory]
    [InlineData("385", "three eighty-five")]
    [InlineData("12", "twelve")]
    [InlineData("7", "seven")]
    [InlineData("100", "one hundred")]
    [InlineData("1985", "nineteen eighty-five")]
    public void NumbersAreSaidCasually(string digits, string expected) =>
        Assert.Equal(expected, SpokenNumber.Say(digits));

    /// <summary>
    /// A leading zero is part of a designation rather than a quantity, so it is kept and said. This
    /// is the difference between two different system names.
    /// </summary>
    [Fact]
    public void LeadingZerosAreSaidRatherThanDropped() =>
        Assert.Equal("zero zero seven", SpokenNumber.Say("007"));

    /// <summary>
    /// <b>The refinement taken while building, and it is why ordinary prose survives the ladder.</b>
    /// A dash between two words is a compound's joint and is silent; a dash beside something being
    /// spelled is part of the designation and is said. Without this, every hyphenated word d47
    /// speaks would have a spoken <em>dash</em> in the middle of it.
    /// </summary>
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

    /// <summary>
    /// The dictionary wins where it holds a word, because it is exact and the rules are not. This
    /// is the top rung and it must stay the top rung.
    /// </summary>
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
