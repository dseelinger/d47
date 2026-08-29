using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The three letter-to-sound gaps found beside #179 and left on its scope fence
/// (<a href="https://github.com/dseelinger/d47/issues/184">#184</a>).
/// <para>
/// All of them are pre-existing and none was made worse by #179, which is why they waited. They
/// are one file's business and one reader's, exactly as #179's three were — a coda English has no
/// spelling rule for, a /ɡ/ that vanished, and a schwa on the wrong side of an <c>l</c> in the
/// words #179's own fix could not reach.
/// </para>
/// <para>
/// The fourth item of #184 is a ruling rather than a rule and lives in
/// <see cref="TheWholePartOfAQuantityIsSaidInFullTests"/>.
/// </para>
/// </summary>
public class TheRulesGapsBesideNgeTests
{
    /// <summary>The vowels a stress mark may stand in front of, as the guard defines them.</summary>
    private const string Vowels = "æɛɪɑʌɔəieaouɜ";

    // ---- 1. The silent gh ---------------------------------------------------------------------

    /// <summary>
    /// <b><c>light</c> could not be said at all.</b> <c>ght</c> was not a coda the parser knew, so
    /// the parse tried <c>gh</c>, was left with a <c>t</c> that begins no syllable, backtracked to
    /// <c>g</c>, was left with <c>ht</c>, and gave up — and a word the rules cannot say is spelled.
    /// On a build whose dictionary failed to download, <em>light</em> was <em>ell eye gee aitch
    /// tee</em>.
    /// <para>
    /// It matters more than its rarity suggests because of #155: <c>ly</c> is rewritten to
    /// <em>light years</em> before any provider sees it, so this is a word d47 now says constantly.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <b>The <c>gh</c> is silent and the vowel in front of it is long.</b> Measured rather than
    /// judged: of the shipped dictionary's entries ending in a single <c>i</c> and <c>ght</c>,
    /// <b>181 of 182</b> are /aɪt/ — the one exception is <em>anight</em>.
    /// </summary>
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
    /// <b><c>ought</c> is /ɔːt/</b>, which is neither <c>ou</c>'s short reading nor its long one and
    /// is the one vowel that needs a rule of its own here. Counted: 54 of the dictionary's 56
    /// <c>-ought</c> entries, the exceptions being <em>drought</em> and <em>dought</em>.
    /// </summary>
    [Theory]
    [InlineData("fought", "fɔːt")]
    [InlineData("bought", "bɔːt")]
    [InlineData("sought", "sɔːt")]
    [InlineData("thought", "θɔːt")]
    [InlineData("brought", "bɹɔːt")]
    public void AnOughtIsSaidWithTheBroadVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>A digraph needs no rule and deliberately gets none.</b> <c>au</c> and <c>ei</c> already
    /// carry their own length, which is why these come out right without being special cases — the
    /// same argument #179 made for <c>lounge</c>, and the same reason the apparent counter-evidence
    /// in the <c>-ight</c> count was almost all <em>weight</em> and <em>freight</em> compounds.
    /// </summary>
    [Theory]
    [InlineData("caught", "kɔːt")]
    [InlineData("taught", "tɔːt")]
    [InlineData("weight", "weɪt")]
    [InlineData("freight", "fɹeɪt")]
    [InlineData("eight", "eɪt")]
    public void ADigraphBeforeGhtSaysItsOwnLength(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>And the coda works away from the end of a word</b>, which is what says the fix is in the
    /// parse rather than in a list of five words.
    /// </summary>
    [Theory]
    [InlineData("lighter", "lˈaɪtɛɹ")]
    [InlineData("lightweight", "lˈaɪtweɪt")]
    [InlineData("Shinright", "ʃˈɪnɹaɪt")]
    public void GhtIsACodaAnywhereInTheWord(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>Said as the property, which is the defect written down:</b> no reading these rules
    /// produce for a <c>ght</c> word contains a /ɡ/, and every one of them ends on a /t/.
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

    /// <summary>
    /// <b><c>single</c> was <c>sˈɪŋəl</c> and <c>angle</c> was <c>ˈæŋəl</c>.</b> The <c>ng</c>
    /// spelling answers /ŋ/, which is right at the end of a word — <em>sing</em>, <em>long</em> —
    /// and wrong in front of a syllabic <c>l</c>, where English says both sounds.
    /// <para>
    /// Counted like the rest: <b>63 of the dictionary's 64</b> entries ending <c>-ngle</c> have
    /// /ŋɡ/, the exception being <em>comingle</em>, which is a prefix on <em>mingle</em>. Every
    /// reading below is the dictionary's own, exactly.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("single", "sˈɪŋɡəl")]
    [InlineData("angle", "ˈæŋɡəl")]
    [InlineData("jungle", "dʒˈʌŋɡəl")]
    [InlineData("mingle", "mˈɪŋɡəl")]
    [InlineData("tangle", "tˈæŋɡəl")]
    public void AnNgleKeepsItsG(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>And an <c>ng</c> that is not in front of a syllabic <c>l</c> is untouched</b>, which is
    /// the half that says this is a shape rather than a new reading for <c>ng</c>. A /ɡ/ intruded
    /// into <em>sing</em> would be a worse defect than the one being fixed, and it is the shape
    /// #179 already had to keep <c>nge</c> away from.
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

    /// <summary>
    /// <b>#179's defect survived wherever the parse kept the consonant in the onset.</b> Whether
    /// two consonants between the vowel and the <c>l</c> are handed back as coda-then-<c>l</c> or
    /// as coda-then-<c>Cl</c> turns on which of them <see cref="Phonotactics"/> admits as a coda:
    /// <c>st</c> is one, so <em>castle</em> is <em>cas.tle</em> and #179 caught it, while <c>nc</c>
    /// is not, so <em>uncle</em> is <em>un.cle</em> and #179 missed it. To a listener the two words
    /// are the same shape, and in the second the whole original defect was still there —
    /// <em>uncle</em> was <c>ˈʌnklə</c>, <em>muscle</em> was <c>mˈʌsklə</c>, <em>centre</em> was
    /// <c>sˈɛntɹə</c>.
    /// </summary>
    [Theory]
    [InlineData("uncle", "ˈʌnkəl")]
    [InlineData("muscle", "mˈʌskəl")]
    [InlineData("centre", "sˈɛntəɹ")]
    public void AConsonantAheadOfASyllabicLStaysAheadOfTheSchwa(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>Said as the defect rather than the reading:</b> no word ending <c>-le</c> comes back with
    /// its schwa after the <c>l</c>. That is the <c>tˈæblə</c> shape, and it is the whole of what
    /// items 3 of #179 and #184 are about.
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
    /// <b>And the consonants ahead of the <c>l</c> count towards the length</b>, which is the same
    /// rule the silent <c>e</c> already follows. The cluster between the vowel of <em>uncle</em>
    /// and its <c>l</c> is <c>nc</c> whichever side of the syllable break the <c>c</c> was written
    /// on, and two sounds do not lengthen — without this the widening above would have made
    /// <em>uncle</em> <c>jˈuːnkəl</c>.
    /// </summary>
    [Theory]
    [InlineData("uncle", "ʌ")]
    [InlineData("muscle", "ʌ")]
    [InlineData("table", "eɪ")]
    [InlineData("castle", "æ")]
    public void TheClusterAheadOfTheLIsCounted(string word, string vowel) =>
        Assert.Contains(vowel, LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>
    /// <b><c>ck</c> was lengthening the vowel in front of a syllabic <c>l</c>, and it never should
    /// have been.</b> It is one sound, so it sat on the list of digraphs a silent <c>e</c> reaches
    /// across — and <c>ck</c> is precisely how English writes a <em>short</em> vowel, exactly as
    /// <c>dge</c> is. <em>tickle</em> was <c>tˈaɪkəl</c>, <em>tackle</em> was <c>tˈeɪkəl</c> and
    /// <em>suckle</em> was <c>sjˈuːkəl</c>.
    /// <para>
    /// Counted on both roads it reached: <b>64 of 65</b> <c>-ckle</c> entries are short, the
    /// exception being <em>ickle</em> — and of the 13 entries ending in a vowel and <c>cke</c>,
    /// which is the silent-<c>e</c> side of the same test, <b>every one</b> is short.
    /// </para>
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
    /// <b>And the silent-<c>e</c> side of the same row.</b> <em>becke</em> is <c>bˈɛk</c> in the
    /// dictionary, and it was <c>biːk</c> here — which is the count above, on the road that
    /// almost never carries anything.
    /// </summary>
    [Fact]
    public void ACkBehindASilentENeverLengthensEither() =>
        Assert.Equal("bɛk", LetterToSound.Pronounce("becke"));

    /// <summary>
    /// <b>The other digraphs on that list are untouched</b>, because they are not what <c>ck</c>
    /// was: <em>Lave</em> and <em>bathe</em> lengthen, and removing the wrong row would have taken
    /// half the silent-e rule with it.
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
    /// <b>Every reading here goes through the stress-mark guard</b>, which is the house rule for
    /// anything producing IPA: the theory lists in
    /// <see cref="TheStressMarkGoesBeforeTheVowelTests"/> are extended rather than a second guard
    /// written beside them. Restated here on this issue's words, because a rule that moves a schwa
    /// past a consonant and a rule that drops a coda are both the shape of change that once put a
    /// mark in front of one.
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
