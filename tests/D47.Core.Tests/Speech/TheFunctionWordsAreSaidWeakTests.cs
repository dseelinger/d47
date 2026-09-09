using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>Words English says weak inside a sentence are not emphasised (2026-08-28).</summary>
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

    /// <summary>And a content word keeps every mark the dictionary gave it.</summary>
    [Theory]
    [InlineData("Kamitra", "kˈæmɪtɹə")]
    [InlineData("tonnes", "tˈʌnz")]
    public void AContentWordIsUntouched(string word, string expected) =>
        Assert.Equal(expected, Rules.ToPhonemes(word));

    /// <summary>The deliberate exclusions, by name.</summary>
    [Theory]
    [InlineData("not", "nˈɑːt")]
    [InlineData("one", "wˈʌn")]
    [InlineData("this", "ðˈɪs")]
    public void TheWordsLeftOffTheListKeepTheirStress(string word, string expected) =>
        Assert.Equal(expected, Rules.ToPhonemes(word));

    [Fact]
    public void TheReportedPhraseEmphasisesTheNameAndNotThePreposition() =>
        Assert.Equal("ɪn kˈæmɪtɹə", Rules.ToPhonemes("in Kamitra"));
}
