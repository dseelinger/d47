using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Three gaps in the letter-to-sound rules, found next door to #153/#150 and fixed together.
/// </summary>
public class ThreeLetterToSoundGapsTests
{
    // ---- 1. nge keeps its soft g --------------------------------------------------------------

    /// <summary><c>change</c> was <c>tʃæŋ</c>.</summary>
    [Theory]
    [InlineData("change", "tʃeɪndʒ")]
    [InlineData("range", "ɹeɪndʒ")]
    [InlineData("strange", "stɹeɪndʒ")]
    [InlineData("hinge", "hɪndʒ")]
    [InlineData("fringe", "fɹɪndʒ")]
    [InlineData("binge", "bɪndʒ")]
    [InlineData("plunge", "plʌndʒ")]
    public void AnNgeKeepsItsSoftG(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>Said the other way round: the affricate is there at all, and the velar nasal is not.</summary>
    [Theory]
    [InlineData("change")]
    [InlineData("hinge")]
    [InlineData("Deciange")]
    public void TheAffricateIsNoLongerSwallowedByTheNasal(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        Assert.Contains("ndʒ", said, StringComparison.Ordinal);
        Assert.DoesNotContain("ŋ", said, StringComparison.Ordinal);
    }

    // ---- The ruling #179 asked for, and what settles it ---------------------------------------

    /// <summary>
    /// The vowel-length ruling, measured rather than judged. #179 left <c>nge</c> open because the
    /// examples pull both ways — change lengthens, hinge does not — and asked for a decision.
    /// </summary>
    [Theory]
    [InlineData("change", "eɪ")]
    [InlineData("range", "eɪ")]
    [InlineData("arrange", "eɪ")]
    [InlineData("hinge", "ɪ")]
    [InlineData("fringe", "ɪ")]
    [InlineData("plunge", "ʌ")]
    [InlineData("binge", "ɪ")]
    public void OnlyAnAIsLengthenedBeforeNge(string word, string vowel) =>
        Assert.Contains(vowel + "ndʒ", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>
    /// <c>ze</c> lengthens: 1,227 of 1,236 dictionary entries. maze, size, doze, prize — the count is
    /// as near exceptionless as this file gets.
    /// </summary>
    [Theory]
    [InlineData("maze", "meɪz")]
    [InlineData("size", "saɪz")]
    [InlineData("doze", "doʊz")]
    [InlineData("prize", "pɹaɪz")]
    public void AZeLengthensTheVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary><c>dge</c> never does: 74 of 76 short.</summary>
    [Theory]
    [InlineData("badge", "bædʒ")]
    [InlineData("bridge", "bɹɪdʒ")]
    [InlineData("judge", "dʒʌdʒ")]
    [InlineData("hedge", "hɛdʒ")]
    public void ADgeNeverLengthensTheVowel(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// A digraph needs no row and is deliberately not asked about: <c>au</c>, <c>ou</c> and <c>ee</c>
    /// already carry their own length, which is why these come out right without being special cases —
    /// and they were the whole of the apparent counter-evidence in the count.
    /// </summary>
    [Theory]
    [InlineData("lounge", "aʊndʒ")]
    [InlineData("chaunge", "ɔːndʒ")]
    public void ADigraphSaysItsOwnLength(string word, string expected) =>
        Assert.Contains(expected, LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    // ---- 2.

    /// <summary><c>tah</c> was <c>tæh</c>, and English has no coda /h/ anywhere.</summary>
    [Theory]
    [InlineData("tah", "tæ")]
    [InlineData("rah", "ɹæ")]
    [InlineData("kah", "kæ")]
    public void ALoneCodaHIsDropped(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>Only a lone one.</summary>
    [Theory]
    [InlineData("Dezhra", "ʒ")]
    [InlineData("bath", "θ")]
    [InlineData("dish", "ʃ")]
    [InlineData("rich", "tʃ")]
    public void ACodaThatMerelyEndsInHKeepsItsSound(string word, string sound) =>
        Assert.Contains(sound, LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    /// <summary>Said as the property: no reading these rules produce contains a /h/ in a coda.</summary>
    [Theory]
    [InlineData("tah")]
    [InlineData("rah")]
    [InlineData("Shinrartah")]
    [InlineData("Kamitrah")]
    public void NoReadingEndsOnAnH(string word) =>
        Assert.DoesNotContain("h", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);

    // ---- 3. -le and -re are syllabic ----------------------------------------------------------

    /// <summary><c>table</c> was <c>tˈæblə</c>.</summary>
    [Theory]
    [InlineData("table", "tˈeɪbəl")]
    [InlineData("maple", "mˈeɪpəl")]
    [InlineData("noble", "nˈoʊbəl")]
    [InlineData("cycle", "sˈaɪkəl")]
    [InlineData("acre", "ˈeɪkəɹ")]
    [InlineData("ogre", "ˈoʊɡəɹ")]
    public void ASyllabicLOrRPutsItsSchwaFirst(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And two sounds in front of it keep the vowel short, which is the same rule the silent <c>e</c>
    /// already follows and the reason <c>dge</c> exists — apple and little are not ape-le and lite-le.
    /// </summary>
    [Theory]
    [InlineData("apple", "ˈæpəl")]
    [InlineData("little", "lˈɪtəl")]
    [InlineData("castle", "kˈæstəl")]
    [InlineData("gentle", "dʒˈɛntəl")]
    public void TwoSoundsInFrontOfItKeepTheVowelShort(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>The boundary against the silent e, from the other side.</summary>
    [Theory]
    [InlineData("Lave", "leɪv")]
    [InlineData("hole", "hoʊl")]
    [InlineData("male", "meɪl")]
    public void AnEHandedBackBareIsStillTheSilentERule(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    // ---- And the guard the whole file answers to ----------------------------------------------

    /// <summary>
    /// Every reading here goes through the stress-mark guard, which is the rule for anything that
    /// produces IPA: the theory list in <see cref="TheStressMarkGoesBeforeTheVowelTests"/> is extended
    /// rather than a second guard written beside it.
    /// </summary>
    [Theory]
    [InlineData("table")]
    [InlineData("acre")]
    [InlineData("gentle")]
    [InlineData("change")]
    [InlineData("tah")]
    public void TheseReadingsMarkAVowel(string word)
    {
        var said = LetterToSound.Pronounce(word)!;
        var at = said.IndexOf('ˈ', StringComparison.Ordinal);

        if (at < 0)
        {
            return;
        }

        Assert.True(
            at + 1 < said.Length
            && "æɛɪɑʌɔəieaouɜ".Contains(said[at + 1], StringComparison.Ordinal),
            $"\"{word}\" -> \"{said}\" marks a consonant.");
    }
}
