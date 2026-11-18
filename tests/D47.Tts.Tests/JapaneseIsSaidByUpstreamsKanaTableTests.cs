using Xunit;

namespace D47.Tts.Tests;

/// <summary>Japanese words become Kokoro phonemes by upstream Kokoro's kana table and its rules, with no dictionary (#2534).</summary>
public sealed class JapaneseIsSaidByUpstreamsKanaTableTests
{
    private static string Say(params (string Surface, string? Pronunciation)[] words) =>
        new JapanesePhonemiser().ToPhonemes([.. words.Select(word => new JapaneseWord(word.Surface, word.Pronunciation))]);

    [Fact]
    public void ASentenceIsSaidWordByWordWithItsFullStop() =>
        Assert.Equal(
            "kʲoː βa ii teŋkʲi desɨ.",
            Say(("今日", "キョー"), ("は", "ワ"), ("いい", "イイ"), ("天気", "テンキ"), ("です", "デス"), ("。", "。")));

    [Theory]
    [InlineData("チョット", "ʨoʔto")]
    [InlineData("コンピューター", "kompʲɨːtaː")]
    [InlineData("シンブン", "ɕimbɯɴ")]
    [InlineData("ギンガ", "ɡʲiŋɡa")]
    [InlineData("サンネン", "sanneɴ")]
    [InlineData("トァ", "ta")]
    [InlineData("ヴァ", "va")]
    [InlineData("ヷ", "va")]
    public void OneWordIsSaidByTheTableAndItsRules(string pronunciation, string phonemes) =>
        Assert.Equal(phonemes, Say(("x", pronunciation)));

    [Fact]
    public void NoSpaceIsLeftBesideAGlottalStop() =>
        Assert.Equal("aʔsoː", Say(("あっ", "アッ"), ("そう", "ソー")));

    [Fact]
    public void QuotesCloseOnTheWordTheyHold() =>
        Assert.Equal("“hai”", Say(("「", "「"), ("はい", "ハイ"), ("」", "」")));

    [Fact]
    public void AWordWithNoReadingIsReadFromItsKana() =>
        Assert.Equal("ɡaɾɯda", Say(("ガルダ", null)));

    [Fact]
    public void AWordWithNoReadingAndNoKanaIsLeftOut()
    {
        var left = new List<string>();

        var phonemes = new JapanesePhonemiser(left.Add).ToPhonemes(
            [new JapaneseWord("Sol", null), new JapaneseWord("へ", "エ"), new JapaneseWord("12", null)]);

        Assert.Equal("e", phonemes);
        Assert.Equal(["Sol", "12"], left);
    }

    [Fact]
    public void HalfWidthKatakanaIsReadAsFullWidth() =>
        Assert.Equal("ガイド", JapanesePhonemiser.Normalise("ｶﾞｲﾄﾞ"));
}
