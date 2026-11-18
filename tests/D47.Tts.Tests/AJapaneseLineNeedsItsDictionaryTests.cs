using D47.Core.Audio;
using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.Tts.Tests;

public sealed class AJapaneseLineNeedsItsDictionaryTests
{
    private const string Folder = @"C:\d47\data\models\japanese";

    [Fact]
    public void ReadingWithNoDictionaryNamesIt()
    {
        using var reader = new JapaneseReader(new MemoryFileSystem(), Folder);

        var refusal = Assert.Throws<TtsException>(() => reader.Read("私は東京に行きます。"));

        Assert.Contains("Japanese dictionary (IPAdic)", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(Folder, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADictionaryMissingOneFileIsNotInstalled()
    {
        var files = new MemoryFileSystem();

        foreach (var asset in JapaneseDictionaryAssets.All.Where(asset => asset.Path != "sys.dic"))
        {
            files.WriteBytes(JapaneseDictionaryAssets.Destination(Folder, asset), new byte[asset.Bytes]);
        }

        files.WriteBytes(Path.Combine(Folder, "sys.dic"), new byte[16]);

        Assert.False(JapaneseDictionaryAssets.IsInstalled(files, Folder));

        using var reader = new JapaneseReader(files, Folder);

        Assert.Throws<TtsException>(() => reader.Read("私"));
    }

    [Fact]
    public void EveryFileComesFromTheDictionaryRelease()
    {
        Assert.Equal(
            ["dicrc", "unk.dic", "char.bin", "matrix.bin", "sys.dic"],
            JapaneseDictionaryAssets.All.Select(asset => asset.Path));

        Assert.All(JapaneseDictionaryAssets.All, asset => Assert.Equal(
            $"https://github.com/dseelinger/d47/releases/download/japanese-dictionary-1/{asset.Path}",
            asset.Url));

        Assert.All(JapaneseDictionaryAssets.All, asset => Assert.Equal(64, asset.Sha256.Length));
    }
}
