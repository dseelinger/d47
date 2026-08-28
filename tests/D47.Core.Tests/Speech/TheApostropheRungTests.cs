using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Words with an apostrophe in them, which the ladder used to spell out one letter at a time.
/// <para>
/// <b>The report that caused this.</b> The first line the local voice ever said aloud was
/// <em>"Systems responding, Commander. Ship's docked in Buzhang Ku"</em>, and <c>Ship's</c> came
/// out <em>ess aitch eye pee ess</em>. Not a bug in the spelling rung — that rung was doing exactly
/// what it is for — but a missing rung above it: an apostrophe makes a word fail
/// <c>All(char.IsLetter)</c>, so both rungs that can pronounce anything skipped it and the last
/// rung, which never refuses, spelled it.
/// </para>
/// <para>
/// The dictionary cannot be the fix. <b>0 of its 274,927 entries contain an apostrophe</b>, so the
/// stem is looked up and the ending is derived.
/// </para>
/// </summary>
public class TheApostropheRungTests
{
    /// <summary>
    /// A dictionary holding the handful of stems these cases need, with the shipped one's shape:
    /// no apostrophes anywhere in it, because the real one has none.
    /// </summary>
    private sealed class Stems : IPronunciationDictionary
    {
        private readonly Dictionary<string, string> _words = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ship"] = "ʃˈɪp",
            ["ships"] = "ʃˈɪps",
            ["commander"] = "kəmˈændɚ",
            ["it"] = "ɪt",
            ["is"] = "ɪz",
            ["does"] = "dˈʌz",
            ["are"] = "ɑːɹ",
            ["have"] = "hˈæv",
            ["we"] = "wiː",
            ["they"] = "ðeɪ",
            ["hull"] = "hˈʌl",
        };

        public string? Lookup(string word) => _words.GetValueOrDefault(word);
    }

    private static readonly Phonemiser Rules = new(new Stems());

    /// <summary>
    /// <b>The reported line.</b> The possessive lands on exactly the transcription the dictionary
    /// holds for the plural, which is the check worth making: the derived ending is not merely
    /// sayable, it is the one an entry would have had.
    /// </summary>
    [Fact]
    public void ThePossessiveIsSaidRatherThanSpelled()
    {
        var said = Rules.ToPhonemes("Ship's docked");

        Assert.StartsWith("ʃˈɪps", said, StringComparison.Ordinal);
        Assert.DoesNotContain("ˈɛs", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The curly apostrophe a language model writes is the same word as the straight one a
    /// Commander types. Everything d47 speaks was written by a model, so this is the common case
    /// rather than the exotic one.
    /// </summary>
    [Fact]
    public void TheCurlyApostropheIsTheSameWord()
    {
        Assert.Equal(Rules.ToPhonemes("Ship's"), Rules.ToPhonemes("Ship’s"));
    }

    /// <summary>
    /// Which of the three sounds <c>'s</c> makes depends on what it lands on, and getting it wrong
    /// is audible: <em>Commander's</em> with a voiceless s is a hiss.
    /// </summary>
    [Theory]
    [InlineData("Ship's", "ʃˈɪps")]
    [InlineData("Commander's", "kəmˈændɚz")]
    [InlineData("Hull's", "hˈʌlz")]
    [InlineData("It's", "ɪts")]
    public void TheEndingFollowsTheSoundBeforeIt(string word, string expected)
    {
        Assert.Equal(expected, Rules.ToPhonemes(word));
    }

    /// <summary>
    /// <c>n't</c> is a syllable of its own after a consonant and not after a vowel or an r, which
    /// is the difference between <em>ˈɪzənt</em> and <em>ɑːɹnt</em>.
    /// </summary>
    [Theory]
    [InlineData("isn't", "ɪzənt")]
    [InlineData("doesn't", "dˈʌzənt")]
    [InlineData("haven't", "hˈævənt")]
    [InlineData("aren't", "ɑːɹnt")]
    public void TheNegationIsBuiltFromTheWordBeforeTheN(string word, string expected)
    {
        Assert.Equal(expected, Rules.ToPhonemes(word));
    }

    /// <summary>
    /// The irregular ones are a table because no rule reaches them: <em>don't</em> is not
    /// <em>do</em> with an ending on it, and the dictionary's <c>wont</c> and <c>ill</c> are real
    /// words that are not the ones written.
    /// </summary>
    [Theory]
    [InlineData("don't", "dˈoʊnt")]
    [InlineData("won't", "wˈoʊnt")]
    [InlineData("I'll", "aɪl")]
    [InlineData("I'm", "aɪm")]
    public void TheIrregularOnesComeFromTheTable(string word, string expected)
    {
        Assert.Equal(expected, Rules.ToPhonemes(word));
    }

    /// <summary>
    /// <c>can't</c> is the one contraction that changes across the Atlantic, and a British voice
    /// saying the American one is the small wrongness heard every time — the same argument that
    /// bought <em>zed</em> its table entry.
    /// </summary>
    [Fact]
    public void CantTakesTheVoicesOwnVowel()
    {
        Assert.Equal("kˈænt", Rules.ToPhonemes("can't", "af_heart"));
        Assert.Equal("kˈɑːnt", Rules.ToPhonemes("can't", "bm_george"));
    }

    /// <summary>
    /// A stem nothing can pronounce is still spelled whole rather than spelled and then given a
    /// possessive, which would be a designation with an English ending stapled to it.
    /// </summary>
    [Fact]
    public void AStemNobodyCanSayFallsBackToSpelling()
    {
        var said = Rules.ToPhonemes("GQPI's");

        Assert.Contains("dʒiː, kjuː, piː, aɪ", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the rung reaches invented names too, which is the point of deriving rather than
    /// listing: no dictionary is ever going to hold <em>Buzhang's</em>.
    /// </summary>
    [Fact]
    public void AnInventedNameStillTakesItsPossessive()
    {
        var said = Rules.ToPhonemes("Buzhang's");

        Assert.EndsWith("z", said, StringComparison.Ordinal);
        Assert.DoesNotContain("biː, juː", said, StringComparison.Ordinal);
    }
}
