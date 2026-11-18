using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public sealed class ChatterboxSpeaksJapaneseInItsSecondModelTests
{
    private sealed class CountingEngine : IChatterboxEngine
    {
        public int Opened { get; set; }

        public int Encodes { get; private set; }

        public List<long[]> Spoken { get; } = [];

        public IDisposable Encode(float[] reference)
        {
            Encodes++;
            return new Handle();
        }

        public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken)
        {
            Spoken.Add(textIds);
            return new float[ChatterboxPipeline.SampleRate / 10];
        }

        public void Dispose()
        {
        }

        private sealed class Handle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private readonly ChatterboxTestFolder _folder = new();
    private readonly CountingEngine _turbo = new();
    private readonly CountingEngine _multilingual = new();
    private readonly string _japaneseFolder;
    private bool _installed = true;

    public ChatterboxSpeaksJapaneseInItsSecondModelTests()
    {
        _japaneseFolder = Path.Combine(_folder.Root, "multilingual");
        _folder.Files.WriteText(
            ChatterboxMultilingualAssets.Destination(_japaneseFolder, ChatterboxMultilingualAssets.Tokenizer),
            ChatterboxMultilingualTokenisesAsItsTokenizerSaysTests.Tokenizer);
    }

    private static VoiceSelection Japanese => new("marlow") { Language = "ja" };

    private ChatterboxTtsProvider Provider() =>
        new(
            _folder.Files,
            _folder.Models,
            _folder.Voices,
            _folder.Fetched,
            NullLogger<ChatterboxTtsProvider>.Instance,
            () =>
            {
                _turbo.Opened++;
                return _turbo;
            },
            () => true,
            ChatterboxTestFolder.NoDownload,
            japanese: new ChatterboxJapaneseSetup(
                _japaneseFolder,
                () =>
                {
                    _multilingual.Opened++;
                    return _multilingual;
                },
                () => _installed,
                line => [new JapaneseWord(line, null)]));

    [Fact]
    public async Task AJapaneseLineGoesToMultilingualAndNeverOpensTurbo()
    {
        using var provider = Provider();

        await provider.SynthesizeAsync("かな", Japanese, TestContext.Current.CancellationToken);

        Assert.Equal(0, _turbo.Opened);
        Assert.Equal(1, _multilingual.Opened);
        Assert.Equal([6563, 255, 723, 13, 0, 6561, 6561], _multilingual.Spoken.Single());
    }

    [Fact]
    public async Task AnEnglishLineStillGoesToTurbo()
    {
        using var provider = Provider();

        await provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);

        Assert.Equal(1, _turbo.Opened);
        Assert.Equal(0, _multilingual.Opened);
    }

    [Fact]
    public async Task EachModelEncodesTheVoiceOnceForItself()
    {
        using var provider = Provider();

        await provider.SynthesizeAsync("かな", Japanese, TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("かな", Japanese, TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);

        Assert.Equal(1, _turbo.Encodes);
        Assert.Equal(1, _multilingual.Encodes);
        Assert.Equal(1, _multilingual.Opened);
        Assert.Equal(2, _multilingual.Spoken.Count);
    }

    [Fact]
    public async Task AJapaneseLineDropsItsTags()
    {
        using var provider = Provider();

        await provider.SynthesizeAsync("[sigh]かな", Japanese, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(611L, _multilingual.Spoken.Single());
    }

    [Fact]
    public async Task BeforeTheDownloadAJapaneseLineThrowsAndNamesIt()
    {
        _installed = false;
        using var provider = Provider();

        var thrown = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("かな", Japanese, TestContext.Current.CancellationToken));

        Assert.Equal(TtsFault.NotDownloaded, thrown.Fault);
        Assert.Equal(SpeechCapability.ChatterboxJapaneseKey, thrown.SettingKey);
        Assert.Contains("Download Chatterbox Japanese, about 1,483 MB", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(0, _multilingual.Opened);
    }
}
