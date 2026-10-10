using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

[Trait("Category", "Integration")]
public sealed class ChatterboxSpeaksInACustomVoiceTests : IDisposable
{
    private const string Name = "Secret Ally";

    private readonly ChatterboxTestFolder _folder = new();
    private readonly Engine _engine = new();
    private readonly CustomVoices _custom;

    public ChatterboxSpeaksInACustomVoiceTests()
    {
        File.WriteAllText(
            Path.Combine(_folder.Voices, ChatterboxVoices.TableName),
            "id\tname\tgender\tlocale\trole\tsource\n"
            + "marlow\tMarlow\tfemale\ten\t\ta test clip\n"
            + "orson\tOrson\tmale\ten\t\ta test clip\n");
        File.WriteAllBytes(
            Path.Combine(_folder.Voices, "orson.wav"),
            WavWriter.ToBytes([.. Enumerable.Repeat(0.25f, ChatterboxVoices.SampleRate * 6)], ChatterboxVoices.SampleRate));

        _custom = new CustomVoices(Path.Combine(_folder.Root, "data"), new DpapiSecretProtector());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _folder.Dispose();
    }

    /// <summary>Records the first sample of each reference it encodes, which tells the clips apart.</summary>
    private sealed class Engine : IChatterboxEngine
    {
        public List<float> Encoded { get; } = [];

        public IDisposable Encode(float[] reference)
        {
            Encoded.Add(MathF.Round(reference[0], 1));
            return new Handle();
        }

        public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken) =>
            new float[ChatterboxPipeline.SampleRate / 10];

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

    private ChatterboxTtsProvider Provider(ILogger<ChatterboxTtsProvider>? logger = null) =>
        new(
            new DiskFileSystem(),
            _folder.Models,
            _folder.Voices,
            _folder.Fetched,
            logger ?? NullLogger<ChatterboxTtsProvider>.Instance,
            () => _engine,
            () => true,
            ChatterboxTestFolder.NoDownload,
            custom: _custom);

    private string Save(string gender, string name = Name)
    {
        Assert.Null(_custom.Save(
            name,
            gender,
            null,
            null,
            [.. Enumerable.Repeat(0.5f, ChatterboxVoices.SampleRate * 6)],
            ChatterboxVoices.SampleRate,
            out var id));

        return id!;
    }

    [Fact]
    public async Task TheVoiceListHasEveryCustomVoiceAfterTheCatalogue()
    {
        var id = Save("male");
        using var provider = Provider();

        var listed = (await provider.ListVoicesAsync(TestContext.Current.CancellationToken)).Voices;

        Assert.Equal(["marlow", "orson", id], listed.Select(voice => voice.Id));
        Assert.Equal([false, false, true], listed.Select(voice => voice.Custom));
        Assert.Equal(Name, listed[^1].Name);
    }

    [Fact]
    public async Task ACustomVoiceIsEncodedOncePerVersion()
    {
        var id = Save("male");
        var other = Save("female", "Another");
        using var provider = Provider();

        await provider.SynthesizeAsync("hi", new VoiceSelection(id), TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("hi hi", new VoiceSelection(id), TestContext.Current.CancellationToken);

        Assert.Equal([0.5f], _engine.Encoded);

        await provider.SynthesizeAsync("hi", new VoiceSelection(other), TestContext.Current.CancellationToken);

        Assert.Equal([0.5f, 0.5f], _engine.Encoded);
    }

    [Theory]
    [InlineData("male", 0.2f)]
    [InlineData("female", 0f)]
    public async Task ADeletedCustomVoiceIsSpokenInAStandInOfItsGender(string gender, float standInFirstSample)
    {
        var id = Save(gender);
        using var provider = Provider();

        await provider.SynthesizeAsync("hi", new VoiceSelection(id), TestContext.Current.CancellationToken);
        _custom.Delete(id);
        await provider.SynthesizeAsync("hi", new VoiceSelection(id), TestContext.Current.CancellationToken);

        Assert.Equal([0.5f, standInFirstSample], _engine.Encoded);
    }

    [Fact]
    public async Task ACustomVoiceThatWasNeverSavedIsSpokenInAStandIn()
    {
        using var provider = Provider();

        var clip = await provider.SynthesizeAsync("hi", new VoiceSelection("my-0badf00d"), TestContext.Current.CancellationToken);

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.Single(_engine.Encoded);
    }
}
