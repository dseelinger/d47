using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>The silent final e against the local voice shipped in 0.84.0.</summary>
public class TheSilentFinalEIsNotVoicedTests
{
    /// <summary>The e is not said.</summary>
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

    /// <summary>And it lengthens what it left.</summary>
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
    /// It only reaches over one sound. serve, dense and paste are short, and they are short because two
    /// consonants stand between the vowel and the e.
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

    /// <summary>The e softens as well as lengthens, which is the same letter doing English's other job.</summary>
    [Theory]
    [InlineData("ace", "eɪs")]
    [InlineData("race", "ɹeɪs")]
    [InlineData("page", "peɪdʒ")]
    [InlineData("rage", "ɹeɪdʒ")]
    public void ACAndAGSoftenBehindTheSilentE(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>A syllable that ends in an e it says is untouched.</summary>
    [Theory]
    [InlineData("table", "tˈeɪbəl")]
    [InlineData("candle", "kˈændəl")]
    public void AnEWithAnOnsetIsStillSaid(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// And an ordinary final vowel still reduces, which is the rule this one had to be carved out of
    /// rather than replace: the a of Dezhra is a schwa and always was.
    /// </summary>
    [Theory]
    [InlineData("Dezhra", "dˈɛʒɹə")]
    [InlineData("Shinrarta", "ʃˈɪnɹæɹtə")]
    [InlineData("Kamitra", "kˈæmɪtɹə")]
    public void AFinalAStillReduces(string word, string expected) =>
        Assert.Equal(expected, LetterToSound.Pronounce(word));

    /// <summary>
    /// A word that loses its final syllable loses its stress mark with it, because a single syllable
    /// carries none — the existing ruling, which the silent e must not be allowed to leave a mark
    /// behind in violation of.
    /// </summary>
    [Theory]
    [InlineData("Lave")]
    [InlineData("Hive")]
    [InlineData("serve")]
    public void ASilentEDoesNotLeaveAMarkOnAOneSyllableWord(string word) =>
        Assert.DoesNotContain("ˈ", LetterToSound.Pronounce(word)!, StringComparison.Ordinal);
}
