using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

public class FourIsSaidWithItsFullVowelTests
{
    private static readonly Phonemiser Rules = new();

    [Theory]
    [InlineData("four", "fˈɔɹ")]
    [InlineData("fourteen", "fˈɔɹtiːn")]
    public void KokoroSaysFourWithTheVowelItWasTrainedOn(string text, string said) =>
        Assert.Equal(said, Rules.ToPhonemes(text));

    [Theory]
    [InlineData("four", "fˈoːɹ", "fˈɔɹ")]
    [InlineData("Fourteen", "fˈoːɹtiːn", "fˈɔɹtiːn")]
    public void TheDictionaryReadingWithoutTheFullVowelIsReplaced(string text, string listed, string said) =>
        Assert.Equal(said, new Phonemiser(new OneWord(text, listed)).ToPhonemes(text));

    [Fact]
    public void ASpelledDigitFourUsesTheSameVowel() =>
        Assert.Contains("fˈɔɹ", SpokenLetters.Say('4', SpeechAccent.American));

    private sealed class OneWord(string word, string ipa) : IPronunciationDictionary
    {
        public string? Lookup(string looked) =>
            string.Equals(looked, word, StringComparison.OrdinalIgnoreCase) ? ipa : null;
    }
}
