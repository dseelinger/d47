using D47.Core.Audio;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

[Trait("Category", "Integration")]
public class ChatterboxSpeaksInYourOwnVoiceTests : IDisposable
{
    private readonly ChatterboxTestFolder _folder = new();
    private readonly Engine _engine = new();
    private readonly OwnVoice _own;

    public ChatterboxSpeaksInYourOwnVoiceTests()
    {
        _own = new OwnVoice(Path.Combine(_folder.Root, "data"), new DpapiSecretProtector());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _folder.Dispose();
    }

    private sealed class Engine : IChatterboxEngine
    {
        public List<int> Encoded { get; } = [];

        public int Released { get; private set; }

        public IDisposable Encode(float[] reference)
        {
            Encoded.Add(reference.Length);
            return new Handle(this);
        }

        public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken) =>
            new float[ChatterboxPipeline.SampleRate / 10];

        public void Dispose()
        {
        }

        private sealed class Handle(Engine engine) : IDisposable
        {
            public void Dispose() => engine.Released++;
        }
    }

    private ChatterboxTtsProvider Provider() =>
        new(_folder.Models, _folder.Voices, _folder.Fetched, NullLogger<ChatterboxTtsProvider>.Instance, () => _engine, () => true, ChatterboxTestFolder.NoDownload, _own);

    private void Record() =>
        Assert.Null(_own.Save(
            [.. Enumerable.Range(0, 6 * 48_000).Select(i => 0.5f * (float)Math.Sin(i / 20.0))],
            48_000));

    private static VoiceSelection Own => new(OwnVoice.VoiceId);

    [Fact]
    public async Task TheVoiceListOffersItAsYourVoice()
    {
        Record();
        using var provider = Provider();

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        var offered = Assert.Single(listed.Voices, voice => voice.Id == OwnVoice.VoiceId);

        Assert.Equal("Your voice", offered.Name);
        Assert.True(offered.Custom);
    }

    [Fact]
    public async Task WithARecordingItSpeaksFromIt()
    {
        Record();
        using var provider = Provider();

        var clip = await provider.SynthesizeAsync("hi", Own, TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("hi hi", Own, TestContext.Current.CancellationToken);

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.Equal([6 * 24_000], _engine.Encoded);
    }

    [Fact]
    public async Task WithoutARecordingItThrowsANamedError()
    {
        using var provider = Provider();

        var thrown = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("hi", Own, TestContext.Current.CancellationToken));

        Assert.Equal(
            "No recording of your voice is saved. Record one in Settings, under Your voice.",
            thrown.Message);
    }

    [Fact]
    public async Task AfterDeleteTheNextLineThrowsAndTheEncoderOutputIsDropped()
    {
        Record();
        using var provider = Provider();

        await provider.SynthesizeAsync("hi", Own, TestContext.Current.CancellationToken);
        _own.Delete();

        await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("hi", Own, TestContext.Current.CancellationToken));

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (_engine.Released == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, _engine.Released);
    }

    [Fact]
    public async Task ANewRecordingIsEncodedAgain()
    {
        Record();
        using var provider = Provider();

        await provider.SynthesizeAsync("hi", Own, TestContext.Current.CancellationToken);
        Record();
        await provider.SynthesizeAsync("hi", Own, TestContext.Current.CancellationToken);

        Assert.Equal(2, _engine.Encoded.Count);
    }
}
