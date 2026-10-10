using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public class ChatterboxSpeaksOnThisMachineTests
{
    private sealed class CountingEngine : IChatterboxEngine
    {
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

    private static ChatterboxTtsProvider Provider(ChatterboxTestFolder folder, CountingEngine engine) =>
        new(folder.Files, folder.Models, folder.Voices, folder.Fetched, NullLogger<ChatterboxTtsProvider>.Instance, () => engine, () => true, ChatterboxTestFolder.NoDownload);

    [Fact]
    public async Task ALineComesBackInTheStandardFormatWithNoKey()
    {
        var folder = new ChatterboxTestFolder();
        var engine = new CountingEngine();
        using var provider = Provider(folder, engine);

        var clip = await provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);

        Assert.Equal(AudioFormat.Standard, clip.Format);

        // 2,400 samples at 24 kHz, doubled to 48 kHz, two bytes each.
        Assert.Equal(2_400 * 2 * 2, clip.Pcm.Length);
        Assert.Null(TtsProviderCatalog.Chatterbox.KeySecretName);
    }

    [Fact]
    public async Task TheEncoderRunsOncePerVoiceAcrossTwoLines()
    {
        var folder = new ChatterboxTestFolder();
        var engine = new CountingEngine();
        using var provider = Provider(folder, engine);

        await provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("hi hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);

        Assert.Equal(1, engine.Encodes);
        Assert.Equal(2, engine.Spoken.Count);
    }

    [Fact]
    public async Task NotInstalledItListsNoVoicesAndNamesTheSize()
    {
        var folder = new ChatterboxTestFolder();
        using var provider = new ChatterboxTtsProvider(
            folder.Files,
            folder.Models, folder.Voices, folder.Fetched, NullLogger<ChatterboxTtsProvider>.Instance);

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VoiceListing.Unreachable, listed.Listing);
        Assert.Empty(listed.Voices);
        Assert.Contains("691 MB", listed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstalledItListsTheShippedVoices()
    {
        var folder = new ChatterboxTestFolder();
        using var provider = Provider(folder, new CountingEngine());

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("marlow", Assert.Single(listed.Voices).Id);
    }

    [Fact]
    public void ItPerformsTheTagsInTheTokenizerAndNoOthers()
    {
        var folder = new ChatterboxTestFolder();
        using var provider = Provider(folder, new CountingEngine());

        Assert.True(provider.Performs("laugh"));
        Assert.True(provider.Performs("sigh"));
        Assert.False(provider.Performs("whispers"));
        Assert.False(((ITtsProvider)provider).ReadsAudioTags);
    }

    [Fact]
    public void ItHasNoPhonemes()
    {
        var folder = new ChatterboxTestFolder();
        ITtsProvider provider = Provider(folder, new CountingEngine());

        Assert.Null(provider.Phonemes("hi", new VoiceSelection("marlow")));
        ((IDisposable)provider).Dispose();
    }

    [Fact]
    public async Task AnUnknownVoiceIsRefused()
    {
        var folder = new ChatterboxTestFolder();
        using var provider = Provider(folder, new CountingEngine());

        await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync("hi", new VoiceSelection("nobody"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ATagIsOneTokenAndTheTemplateEndsTheLine()
    {
        var folder = new ChatterboxTestFolder();
        var tokeniser = ChatterboxTokeniser.Load(folder.Files, Path.Combine(folder.Models, "tokenizer.json"));

        Assert.Equal([5, 4, 50275, 6, 50256, 50256], tokeniser.Encode("hi [Laugh] hi"));
        Assert.Equal(["sigh", "laugh"], tokeniser.Tags);
    }
}
