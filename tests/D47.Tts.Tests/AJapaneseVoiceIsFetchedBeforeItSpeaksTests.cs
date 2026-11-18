using D47.Core.Audio;
using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>A Kokoro Japanese voice asked to speak before its file and the dictionary are here refuses the line and asks for them (#2534).</summary>
public sealed class AJapaneseVoiceIsFetchedBeforeItSpeaksTests
{
    private const string Folder = @"C:\d47\models\kokoro";

    private const string Dictionary = @"C:\d47\models\ipadic";

    private readonly MemoryFileSystem _files = new();
    private readonly List<string> _asked = [];

    public AJapaneseVoiceIsFetchedBeforeItSpeaksTests()
    {
        _files.WriteText(Path.Combine(Folder, "model.onnx"), "model");
        _files.WriteText(Path.Combine(Folder, "tokenizer.json"), "{}");
        _files.WriteText(Path.Combine(Folder, "phoneme_dict.json"), "{}");

        foreach (var voice in KokoroAssets.VoiceIds)
        {
            _files.WriteText(Path.Combine(Folder, "voices", voice + ".bin"), "voice");
        }
    }

    private KokoroTtsProvider Provider(string? dictionary = Dictionary) =>
        new(_files, Folder, new RecordingLogger<KokoroTtsProvider>(), japaneseDictionary: dictionary, japaneseMissing: _asked.Add);

    [Fact]
    public async Task AJapaneseLineBeforeItsVoiceIsHereIsRefusedAndTheVoiceIsAskedFor()
    {
        using var kokoro = Provider();

        var refused = await Assert.ThrowsAsync<TtsException>(() =>
            kokoro.SynthesizeAsync("こんにちは。", new VoiceSelection("jf_alpha"), TestContext.Current.CancellationToken));

        Assert.Contains("story's own voice", refused.Message, StringComparison.Ordinal);
        Assert.Equal(["jf_alpha"], _asked);
    }

    [Fact]
    public async Task AJapaneseVoiceWithItsFileButNoDictionaryIsStillRefused()
    {
        _files.WriteBytes(Path.Combine(Folder, "voices", "jm_kumo.bin"), new byte[KokoroAssets.JapaneseVoice("jm_kumo")!.Bytes]);
        using var kokoro = Provider();

        await Assert.ThrowsAsync<TtsException>(() =>
            kokoro.SynthesizeAsync("こんにちは。", new VoiceSelection("jm_kumo"), TestContext.Current.CancellationToken));

        Assert.Equal(["jm_kumo"], _asked);
    }

    [Fact]
    public async Task AProviderThatReadsNoJapaneseHasNoJapaneseVoice()
    {
        using var kokoro = Provider(dictionary: null);

        var refused = await Assert.ThrowsAsync<TtsException>(() =>
            kokoro.SynthesizeAsync("こんにちは。", new VoiceSelection("jf_alpha"), TestContext.Current.CancellationToken));

        Assert.Equal(TtsFault.NoVoice, refused.Fault);
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task TheVoiceListIsTheTwentyEightEnglishVoices()
    {
        using var kokoro = Provider();

        var listed = await kokoro.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(KokoroAssets.VoiceIds, listed.Voices.Select(voice => voice.Id));
        Assert.Equal(28, listed.Count);
    }

    [Fact]
    public void AJapaneseCharactersListIsTheFiveJapaneseVoices()
    {
        var listed = KokoroTtsProvider.Catalogue(installed: true, "ja");

        Assert.Equal(["jf_alpha", "jf_gongitsune", "jf_nezumi", "jf_tebukuro", "jm_kumo"], listed.Voices.Select(voice => voice.Id));
        Assert.All(listed.Voices, voice => Assert.Equal("ja-JP", voice.Locale));
        Assert.Equal("Male", listed.Voices.Single(voice => voice.Id == "jm_kumo").Gender);
        Assert.Equal(0, KokoroTtsProvider.Catalogue(installed: false, "ja").Count);
    }

    [Fact]
    public void TheJapaneseVoicesAreNotPartOfTheBaseDownload()
    {
        Assert.True(KokoroAssets.IsInstalled(_files, Folder));
        Assert.DoesNotContain(KokoroAssets.JapaneseVoiceIds, KokoroAssets.VoiceIds.Contains);

        var english = (KokoroAssets.Model.Bytes + KokoroAssets.Dictionary.Bytes + KokoroAssets.Tokenizer.Bytes
                       + KokoroAssets.Voices.Sum(voice => voice.Bytes)) / 1024.0 / 1024.0;

        Assert.Equal(english, KokoroAssets.TotalMegabytes);
    }
}
