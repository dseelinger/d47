using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public class ADisposedVoiceFinishesItsLineTests
{
    private sealed class CountingEngine : IChatterboxEngine
    {
        public IDisposable Encode(float[] reference) => new Handle();

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

    [Fact]
    public void DisposeWhileALineRunsFreesOnlyWhenThatLineEnds()
    {
        var freed = 0;
        var runs = new RunsInFlight(() => freed++);

        Assert.True(runs.TryBegin());
        runs.Dispose();
        Assert.Equal(0, freed);

        runs.End();
        Assert.Equal(1, freed);
    }

    [Fact]
    public void DisposeWithNoLineRunningFreesAtOnce()
    {
        var freed = 0;
        var runs = new RunsInFlight(() => freed++);

        runs.Dispose();
        runs.Dispose();

        Assert.Equal(1, freed);
    }

    [Fact]
    public void ALineStartedAfterDisposeIsRefused()
    {
        var runs = new RunsInFlight(() => { });

        runs.Dispose();

        Assert.False(runs.TryBegin());
    }

    [Fact]
    public void TwoLinesRunningFreeOnceAfterTheSecondEnds()
    {
        var freed = 0;
        var runs = new RunsInFlight(() => freed++);

        Assert.True(runs.TryBegin());
        Assert.True(runs.TryBegin());
        runs.Dispose();
        runs.End();
        Assert.Equal(0, freed);

        runs.End();
        Assert.Equal(1, freed);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ADisposedChatterboxOpensNoEngine()
    {
        using var folder = new ChatterboxTestFolder();
        var opened = 0;
        var provider = new ChatterboxTtsProvider(
            folder.Models,
            folder.Voices,
            folder.Fetched,
            NullLogger<ChatterboxTtsProvider>.Instance,
            () =>
            {
                opened++;
                return new CountingEngine();
            },
            () => true,
            ChatterboxTestFolder.NoDownload);

        await provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken);
        provider.Dispose();

        var before = opened;

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            provider.SynthesizeAsync("hi", new VoiceSelection("marlow"), TestContext.Current.CancellationToken));
        Assert.Equal(before, opened);
    }
}
