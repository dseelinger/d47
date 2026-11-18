using System.Text;
using Xunit;

namespace D47.Tts.Tests;

public sealed class AJapaneseLineIsGivenToChatterboxAsKanaTests
{
    /// <summary>A reader that returns <paramref name="words"/> whatever it is given, and keeps what it was given.</summary>
    private sealed class Reads(params JapaneseWord[] words)
    {
        public List<string> Given { get; } = [];

        public IReadOnlyList<JapaneseWord> Read(string line)
        {
            Given.Add(line);
            return words;
        }
    }

    private static string Nfkd(string text) => text.Normalize(NormalizationForm.FormKD);

    [Fact]
    public void AKanjiWordIsReplacedByItsReadingInHiraganaNotItsPronunciation()
    {
        var reader = new Reads(new JapaneseWord("東京", "トーキョー", "トウキョウ"), new JapaneseWord("は", "ワ", "ハ"));

        Assert.Equal("[ja]とうきょうは", ChatterboxJapaneseText.Prepare("東京は", reader.Read));
    }

    [Fact]
    public void KanaDigitsAndPunctuationStayAsWritten()
    {
        var reader = new Reads(
            new JapaneseWord("カタカナ", "カタカナ", "カタカナ"),
            new JapaneseWord("で", "デ", "デ"),
            new JapaneseWord("3", null),
            new JapaneseWord("。", "。", "。"));

        Assert.Equal(Nfkd("[ja]カタカナで3。"), ChatterboxJapaneseText.Prepare("カタカナで3。", reader.Read));
    }

    [Fact]
    public void TheLineIsNormalisedToNfkdSoVoicingMarksAreSplitOff()
    {
        var reader = new Reads(new JapaneseWord("駅", "エキ", "エキ"), new JapaneseWord("が", "ガ", "ガ"), new JapaneseWord("３", null));

        var prepared = ChatterboxJapaneseText.Prepare("駅が３", reader.Read);

        Assert.Equal(Nfkd("[ja]えきが3"), prepared);
        Assert.Contains('゙', prepared);
    }

    [Fact]
    public void AKanjiWordWithNoReadingIsLeftOutAndNamed()
    {
        var left = new List<string>();
        var reader = new Reads(new JapaneseWord("龘", null), new JapaneseWord("へ", "エ", "ヘ"));

        Assert.Equal("[ja]へ", ChatterboxJapaneseText.Prepare("龘へ", reader.Read, left.Add));
        Assert.Equal(["龘"], left);
    }

    [Fact]
    public void EveryBracketedTagIsRemovedBeforeTheLineIsRead()
    {
        var reader = new Reads(new JapaneseWord("はい", "ハイ", "ハイ"));

        ChatterboxJapaneseText.Prepare("[sigh]はい[laugh]", reader.Read);

        Assert.DoesNotContain('[', reader.Given.Single());
        Assert.Contains("はい", reader.Given.Single(), StringComparison.Ordinal);
    }
}
