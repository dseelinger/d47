using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// Words English says weak inside a sentence are not emphasised (2026-08-28).
/// <para>
/// <b>The dictionary stores citation forms</b> — how a word sounds said on its own — which is right
/// for a lookup and wrong for a sentence. Read back, it put the emphasis on the preposition in
/// <em>"JOHN DEPARAGON is <b>in</b> Kamitra"</em> and on the auxiliary in <em>"you <b>have</b> 256
/// tonnes"</em>.
/// </para>
/// <para>
/// <b>Measured before it was built, and the measurement is why it is described as small:</b> over
/// ten lines d47 actually said, 9 of 84 stress marks land on a function word. It is not a fix for
/// how many marks there are — the dictionary already leaves most of these weak and the rest sit on
/// content words where they belong. It removes nine wrong ones.
/// </para>
/// </summary>
public class TheFunctionWordsAreSaidWeakTests
{
    /// <summary>A dictionary that stresses everything, which is the shape the real one has.</summary>
    private sealed class Citation : IPronunciationDictionary
    {
        public string? Lookup(string word) => word switch
        {
            "in" => "ˈɪn",
            "have" => "hˈæv",
            "on" => "ˈɑːn",
            "them" => "ðˈɛm",
            "could" => "kˈʊd",
            "not" => "nˈɑːt",
            "one" => "wˈʌn",
            "this" => "ðˈɪs",
            "kamitra" => "kˈæmɪtɹə",
            "tonnes" => "tˈʌnz",
            _ => null,
        };
    }

    private static readonly Phonemiser Rules = new(new Citation());

    /// <summary>The words from the reported lines, each now said without emphasis.</summary>
    [Theory]
    [InlineData("in", "ɪn")]
    [InlineData("have", "hæv")]
    [InlineData("on", "ɑːn")]
    [InlineData("them", "ðɛm")]
    [InlineData("could", "kʊd")]
    public void AFunctionWordLosesItsCitationStress(string word, string expected) =>
        Assert.Equal(expected, Rules.ToPhonemes(word));

    /// <summary>
    /// <b>And a content word keeps every mark the dictionary gave it.</b> This is the half that
    /// matters: the change must take nothing away from the words carrying the meaning.
    /// </summary>
    [Theory]
    [InlineData("Kamitra", "kˈæmɪtɹə")]
    [InlineData("tonnes", "tˈʌnz")]
    public void AContentWordIsUntouched(string word, string expected) =>
        Assert.Equal(expected, Rules.ToPhonemes(word));

    /// <summary>
    /// <b>The deliberate exclusions, by name.</b> Negation is the one thing a sentence most needs to
    /// carry; <c>one</c> is a number far more often than a pronoun in anything d47 says; a
    /// demonstrative is usually pointing at something and pointing is emphasis. Each would be wrong
    /// more often than right, which is the test for being on the list at all.
    /// </summary>
    [Theory]
    [InlineData("not", "nˈɑːt")]
    [InlineData("one", "wˈʌn")]
    [InlineData("this", "ðˈɪs")]
    public void TheWordsLeftOffTheListKeepTheirStress(string word, string expected) =>
        Assert.Equal(expected, Rules.ToPhonemes(word));

    /// <summary>The reported phrase, with the emphasis off the preposition and on the name.</summary>
    [Fact]
    public void TheReportedPhraseEmphasisesTheNameAndNotThePreposition() =>
        Assert.Equal("ɪn kˈæmɪtɹə", Rules.ToPhonemes("in Kamitra"));
}
