using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The silent final e, reported 2026-08-28 against the local voice shipped in 0.84.0 (#153).
/// <para>
/// <b>The rules voiced every one of them, by design.</b> An unstressed final vowel reduces to a
/// schwa — right for the a of <em>Dezhra</em>, and applied without exception to an e that English
/// does not say at all. <c>lave</c> parses as <c>lav.e</c>, so the e became a syllable of its own
/// and <em>Lave</em> came out <em>lav-uh</em>.
/// </para>
/// <para>
/// <b>The reported word is not the proof and could not be.</b> <c>observe</c> is in the shipped
/// dictionary and never reached these rules — see
/// <see cref="TheMarksAroundAWordAreNotSpelledTests"/> for what was actually heard. What makes this
/// half of the report real anyway is the tail: half the proper nouns in the galaxy end consonant
/// plus e, and every one of them said <em>-uh</em> the day it missed the dictionary. <em>Lave</em>
/// is in the game's opening credits.
/// </para>
/// </summary>
public class TheSilentFinalEIsNotVoicedTests
{
    /// <summary>
    /// <b>The e is not said.</b> Stated as the property rather than as a spelling, because what
    /// went wrong was one extra syllable on the end and that is what an assertion should catch.
    /// </summary>
    [Theory]
    [InlineData("Lave")]
    [InlineData("Vale")]
    [InlineData("Vane")]
    [InlineData("Hive")]
    [InlineData("Sole")]
    [InlineData("Orbite")]
    [InlineData("Eravate")]
    [InlineData("Shinrarte")]
    [InlineData("observe")]
    [InlineData("serve")]
    [InlineData("paste")]
    [InlineData("dense")]
    public void AFinalEAfterAConsonantIsSilent(string word)
    {
        var said = LetterToSound.Pronounce(word)!;

        Assert.False(
            said.EndsWith('ə') || said.EndsWith("iː", StringComparison.Ordinal),
            $"\"{word}\" -> \"{said}\" still says the e on the end.");
    }

    /// <summary>
    /// <b>And it lengthens what it left.</b> The other half of the same rule: the e in
    /// <em>Lave</em> is not merely silent, it is what makes the a say its own name. Without this
    /// the fix would trade <em>lav-uh</em> for <em>lav</em>, which is a different wrong answer.
    /// </summary>
    [Theory]
    [InlineData("Lave", "leɪv")]
    [InlineData("Vale", "veɪl")]
    [InlineData("Vane", "veɪn")]
    [InlineData("Hive", "haɪv")]
    [InlineData("Sole", "soʊl")]
    [InlineData("Prime", "pɹaɪm")]
    [InlineData("Type", "taɪp")]
    [InlineData("Cute", "kjuːt")]
    public void OneConsonantAwayTheVowelGoesLong(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>It only reaches over one sound.</b> <em>serve</em>, <em>dense</em> and <em>paste</em> are
    /// short, and they are short because two consonants stand between the vowel and the e. A rule
    /// that lengthened regardless would say <em>seerve</em> for the word this issue was reported
    /// about.
    /// </summary>
    [Theory]
    [InlineData("serve", "sɛɹv")]
    [InlineData("curve", "kʌɹv")]
    [InlineData("nerve", "nɛɹv")]
    [InlineData("dense", "dɛns")]
    [InlineData("paste", "pæst")]
    [InlineData("matte", "mæt")]
    public void TwoConsonantsAwayItStaysShort(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>The e softens as well as lengthens</b>, which is the same letter doing English's other
    /// job. Without it the fix would introduce a new wrongness at the moment it removed the old
    /// one: <em>ace</em> read with a /k/ is worse than <em>ac-uh</em>, because it is a different
    /// word rather than an odd one.
    /// </summary>
    [Theory]
    [InlineData("ace", "eɪs")]
    [InlineData("race", "ɹeɪs")]
    [InlineData("page", "peɪdʒ")]
    [InlineData("rage", "ɹeɪdʒ")]
    public void ACAndAGSoftenBehindTheSilentE(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>A syllable that ends in an e it says is untouched.</b> <c>-le</c> and <c>-re</c> carry an
    /// onset, so the e is a syllable an English speaker pronounces and the silent-e rule is right
    /// to leave it alone. This is the boundary the rule is drawn at, and it is worth an assertion
    /// because the obvious cheap version — drop a trailing e — would take these with it.
    /// <para>
    /// <b>The boundary is unchanged; the readings moved with #179.</b> This file pinned
    /// <c>tˈæblə</c>, which was the shape of the defect that issue then reported: the e is said,
    /// but English says the schwa in front of the <c>l</c> rather than behind it — <c>tˈeɪbəl</c>.
    /// What is still asserted here is what this test was always for, that these words keep a
    /// syllable the silent-e rule would have taken off them.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("table", "tˈeɪbəl")]
    [InlineData("candle", "kˈændəl")]
    public void AnEWithAnOnsetIsStillSaid(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And an ordinary final vowel still reduces, which is the rule this one had to be carved out
    /// of rather than replace: the a of <em>Dezhra</em> is a schwa and always was.
    /// </summary>
    [Theory]
    [InlineData("Dezhra", "dˈɛʒɹə")]
    [InlineData("Shinrarta", "ʃˈɪnɹæɹtə")]
    [InlineData("Kamitra", "kˈæmɪtɹə")]
    public void AFinalAStillReduces(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// <b>A word that loses its final syllable loses its stress mark with it</b>, because a single
    /// syllable carries none — the existing ruling, which the silent e must not be allowed to leave
    /// a mark behind in violation of. <em>Lave</em> is one syllable once the e is gone.
    /// </summary>
    [Theory]
    [InlineData("Lave")]
    [InlineData("Hive")]
    [InlineData("serve")]
    public void ASilentEDoesNotLeaveAMarkOnAOneSyllableWord(string word) =>
        Assert.DoesNotContain("ˈ", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);
}
