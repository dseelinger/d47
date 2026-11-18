using System.Security.Cryptography;
using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>Reads with the IPAdic files the MeCab.DotNet package copies to <c>dic\</c> beside the tests.</summary>
[Trait("Category", "Integration")]
public sealed class JapaneseIsReadAsKanaTests : IDisposable
{
    private static readonly string Dictionary = Path.Combine(AppContext.BaseDirectory, "dic");

    private readonly JapaneseReader _reader = new(new DiskFileSystem(), Dictionary);

    [Fact]
    public void KanjiHiraganaKatakanaDigitsAndPunctuationAreReadInOrder() =>
        Reads(
            "私は東京駅で3時に、カタカナとｶﾀｶﾅを読みました。",
            ("私", "ワタシ"), ("は", "ワ"), ("東京", "トーキョー"), ("駅", "エキ"), ("で", "デ"),
            ("3", null), ("時", "ジ"), ("に", "ニ"), ("、", "、"), ("カタカナ", "カタカナ"),
            ("と", "ト"), ("ｶﾀｶﾅ", null), ("を", "ヲ"), ("読み", "ヨミ"), ("まし", "マシ"),
            ("た", "タ"), ("。", "。"));

    [Fact]
    public void TwoSentencesAreReadAsOneLine() =>
        Reads(
            "今日は良い天気ですね。宇宙船が星系に到着しました！",
            ("今日", "キョー"), ("は", "ワ"), ("良い", "ヨイ"), ("天気", "テンキ"), ("です", "デス"),
            ("ね", "ネ"), ("。", "。"), ("宇宙船", "ウチュウセン"), ("が", "ガ"), ("星", "ホシ"),
            ("系", "ケイ"), ("に", "ニ"), ("到着", "トーチャク"), ("し", "シ"), ("まし", "マシ"),
            ("た", "タ"), ("！", "！"));

    [Fact]
    public void AWordTheDictionaryDoesNotHoldHasNoPronunciation() =>
        Reads("XYZZYは？", ("XYZZY", null), ("は", "ワ"), ("？", "？"));

    [Fact]
    public void ThePinnedFilesAreThePackagesOwn()
    {
        Assert.True(JapaneseDictionaryAssets.IsInstalled(new DiskFileSystem(), Dictionary));

        foreach (var asset in JapaneseDictionaryAssets.All)
        {
            var path = JapaneseDictionaryAssets.Destination(Dictionary, asset);

            Assert.Equal(asset.Bytes, new FileInfo(path).Length);
            Assert.Equal(asset.Sha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
        }
    }

    public void Dispose() => _reader.Dispose();

    private void Reads(string line, params (string Surface, string? Pronunciation)[] expected)
    {
        var words = _reader.Read(line);

        Assert.Equal(expected, words.Select(word => (word.Surface, word.Pronunciation)));
        Assert.Equal(line, string.Concat(words.Select(word => word.Surface)));
    }
}
