using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>The three letter-to-sound gaps found beside #179 and left on its scope fence.</summary>
public class TheRulesGapsBesideNgeTests
{
    /// <summary>The vowels a stress mark may stand in front of, as the guard defines them.</summary>
    private const string Vowels = "æɛɪɑʌɔəieaouɜ";

    // ---- 1.

    /// <summary><c>light</c> could not be said at all.</summary>
    [Theory]
    [InlineData("light")]
    [InlineData("night")]
    [InlineData("fought")]
    [InlineData("bought")]
    [InlineData("caught")]
    [InlineData("weight")]
    public void AWordWithGhtCanBeSaidAtAll(string word)
    {
        Assert.True(Phonotactics.IsSayable(word), $"\"{word}\" still cannot be parsed.");
        Assert.NotNull(LetterToSound.Pronounce(word));
    }

    /// <summary>The <c>gh</c> is silent and the vowel in front of it is long.</summary>
    [Theory]
    [InlineData("light", "laɪt")]
    [InlineData("night", "naɪt")]
    [InlineData("right", "ɹaɪt")]
    [InlineData("might", "maɪt")]
    [InlineData("sight", "saɪt")]
    [InlineData("tight", "taɪt")]
    public void AnIghtIsLongAndItsGhIsSilent(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <c>ought</c> is /ɔːt/, which is neither <c>ou</c>'s short reading nor its long one and is the
    /// one vowel that needs a rule of its own here.
    /// </summary>
    [Theory]
    [InlineData("fought", "fɔːt")]
    [InlineData("bought", "bɔːt")]
    [InlineData("sought", "sɔːt")]
    [InlineData("thought", "θɔːt")]
    [InlineData("brought", "bɹɔːt")]
    public void AnOughtIsSaidWithTheBroadVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>A digraph needs no rule and deliberately gets none.</summary>
    [Theory]
    [InlineData("caught", "kɔːt")]
    [InlineData("taught", "tɔːt")]
    [InlineData("weight", "weɪt")]
    [InlineData("freight", "fɹeɪt")]
    [InlineData("eight", "eɪt")]
    public void ADigraphBeforeGhtSaysItsOwnLength(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And the coda works away from the end of a word, which is what says the fix is in the parse
    /// rather than in a list of five words.
    /// </summary>
    [Theory]
    [InlineData("lighter", "lˈaɪtɛɹ")]
    [InlineData("lightweight", "lˈaɪtweɪt")]
    [InlineData("Shinright", "ʃˈɪnɹaɪt")]
    public void GhtIsACodaAnywhereInTheWord(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// Said as the property, which is the defect written down: no reading these rules produce for a
    /// <c>ght</c> word contains a /ɡ/, and every one of them ends on a /t/.
    /// </summary>
    [Theory]
    [InlineData("light")]
    [InlineData("night")]
    [InlineData("fought")]
    [InlineData("thought")]
    [InlineData("weight")]
    public void TheGhOfGhtIsNeverVoiced(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        Assert.DoesNotContain("ɡ", said, StringComparison.Ordinal);
        Assert.EndsWith("t", said, StringComparison.Ordinal);
    }

    // ---- 2. ngle keeps its g ------------------------------------------------------------------

    /// <summary><c>single</c> was <c>sˈɪŋəl</c> and <c>angle</c> was <c>ˈæŋəl</c>.</summary>
    [Theory]
    [InlineData("single", "sˈɪŋɡəl")]
    [InlineData("angle", "ˈæŋɡəl")]
    [InlineData("jungle", "dʒˈʌŋɡəl")]
    [InlineData("mingle", "mˈɪŋɡəl")]
    [InlineData("tangle", "tˈæŋɡəl")]
    public void AnNgleKeepsItsG(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And an <c>ng</c> that is not in front of a syllabic <c>l</c> is untouched, which is the half
    /// that says this is a shape rather than a new reading for <c>ng</c>.
    /// </summary>
    [Theory]
    [InlineData("sing", "sɪŋ")]
    [InlineData("ring", "ɹɪŋ")]
    [InlineData("Kamitrang", "kˈæmɪtɹæŋ")]
    public void AnNgWithoutASyllabicLIsUntouched(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>And <c>nge</c> keeps #179's reading, which is a different rule about the same letters.</summary>
    [Theory]
    [InlineData("change", "tʃeɪndʒ")]
    [InlineData("hinge", "hɪndʒ")]
    public void TheNgeRulingIsUntouched(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    // ---- 3. -le after a consonant -------------------------------------------------------------

    /// <summary>#179's defect survived wherever the parse kept the consonant in the onset.</summary>
    [Theory]
    [InlineData("uncle", "ˈʌnkəl")]
    [InlineData("muscle", "mˈʌskəl")]
    [InlineData("centre", "sˈɛntəɹ")]
    public void AConsonantAheadOfASyllabicLStaysAheadOfTheSchwa(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// Said as the defect rather than the reading: no word ending <c>-le</c> comes back with its schwa
    /// after the <c>l</c>.
    /// </summary>
    [Theory]
    [InlineData("uncle")]
    [InlineData("muscle")]
    [InlineData("table")]
    [InlineData("castle")]
    [InlineData("single")]
    [InlineData("circle")]
    [InlineData("Kamitrancle")]
    public void NoSyllabicLEndsOnASchwa(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        Assert.EndsWith("əl", said, StringComparison.Ordinal);
        Assert.DoesNotContain("lə", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the consonants ahead of the <c>l</c> count towards the length, which is the same rule the
    /// silent <c>e</c> already follows.
    /// </summary>
    [Theory]
    [InlineData("uncle", "ʌ")]
    [InlineData("muscle", "ʌ")]
    [InlineData("table", "eɪ")]
    [InlineData("castle", "æ")]
    public void TheClusterAheadOfTheLIsCounted(string word, string vowel) =>
        Assert.Contains(vowel, LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>
    /// <c>ck</c> was lengthening the vowel in front of a syllabic <c>l</c>, and it never should have
    /// been.
    /// </summary>
    [Theory]
    [InlineData("tickle", "tˈɪkəl")]
    [InlineData("tackle", "tˈækəl")]
    [InlineData("buckle", "bˈʌkəl")]
    [InlineData("suckle", "sˈʌkəl")]
    [InlineData("freckle", "fɹˈɛkəl")]
    public void ACkNeverLengthensTheVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And the silent-<c>e</c> side of the same row. becke is <c>bˈɛk</c> in the dictionary, and it was
    /// <c>biːk</c> here — which is the count above, on the road that almost never carries anything.
    /// </summary>
    [Fact]
    public void ACkBehindASilentENeverLengthensEither() =>
        Assert.Equal("bɛk", LetterToSound.Pronounce("becke"));

    /// <summary>
    /// The other digraphs on that list are untouched, because they are not what <c>ck</c> was: Lave and
    /// bathe lengthen, and removing the wrong row would have taken half the silent-e rule with it.
    /// </summary>
    [Theory]
    [InlineData("Lave", "leɪv")]
    [InlineData("bathe", "beɪθ")]
    [InlineData("hole", "hoʊl")]
    [InlineData("table", "tˈeɪbəl")]
    public void TheDigraphsThatDoLengthenStillDo(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    // ---- The guard every rung that produces IPA answers to --------------------------------------

    /// <summary>
    /// Every reading here goes through the stress-mark guard, which is the house rule for anything
    /// producing IPA: the theory lists in <see cref="TheStressMarkGoesBeforeTheVowelTests"/> are
    /// extended rather than a second guard written beside them.
    /// </summary>
    [Theory]
    [InlineData("light")]
    [InlineData("lighter")]
    [InlineData("thought")]
    [InlineData("single")]
    [InlineData("angle")]
    [InlineData("uncle")]
    [InlineData("muscle")]
    [InlineData("tickle")]
    [InlineData("centre")]
    public void TheseReadingsMarkAVowel(string word)
    {
        var said = LetterToSound.Pronounce(word)!;
        var at = said.IndexOf('ˈ', StringComparison.Ordinal);

        if (at < 0)
        {
            return;
        }

        Assert.True(
            at + 1 < said.Length && Vowels.Contains(said[at + 1], StringComparison.Ordinal),
            $"\"{word}\" -> \"{said}\" marks a consonant.");
    }
}
