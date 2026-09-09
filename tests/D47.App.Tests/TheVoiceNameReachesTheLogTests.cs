using D47.App.Voice;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The App half of naming a voice.</summary>
public class TheVoiceNameReachesTheLogTests
{
    private static VoicePipeline Pipeline(Func<string?, string?>? names) =>
        new(
            new AudioArbiter(new Quiet(), NullLogger<AudioArbiter>.Instance),
            CueLibrary.Load,
            NullLoggerFactory.Instance)
        {
            VoiceName = names,
        };

    [Fact]
    public void ThePipelineAttachesWhatTheHostCallsTheVoice()
    {
        var pipeline = Pipeline(id => id == "JBFqnCBsd6RMkjVDRZzb" ? "George" : null);

        Assert.Equal("George", pipeline.Introduce(new VoiceSelection("JBFqnCBsd6RMkjVDRZzb")).Name);
    }

    /// <summary>
    /// A voice the catalogue does not know — an id the Commander typed themselves, or any id at all
    /// before the provider's list has arrived — is left exactly as it was.
    /// </summary>
    [Fact]
    public void AnUnknownVoiceIsLeftAlone()
    {
        var pipeline = Pipeline(_ => null);

        Assert.Null(pipeline.Introduce(new VoiceSelection("something-hand-typed")).Name);
    }

    [Fact]
    public void NoVoiceIsNotGivenAName()
    {
        var pipeline = Pipeline(_ => "George");

        Assert.Null(pipeline.Introduce(VoiceSelection.Default).Name);
    }

    /// <summary>And a caller that already said what the voice is called keeps its own answer.</summary>
    [Fact]
    public void ANameTheCallerAlreadySuppliedIsKept()
    {
        var pipeline = Pipeline(_ => "Somebody else");

        Assert.Equal(
            "George",
            pipeline.Introduce(new VoiceSelection("JBFqnCBsd6RMkjVDRZzb") { Name = "George" }).Name);
    }

    /// <summary>A sink that accepts everything and finishes nothing.</summary>
    private sealed class Quiet : IAudioSink
    {
        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request) => _ = Finished;

        public void Stop(long playbackId)
        {
        }

        public void StopAll()
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        private sealed class NoTap : IRenderReferenceTap
        {
            public event Action<RenderReferenceFrame>? Rendered;

            public void Dispose() => _ = Rendered;
        }
    }
}
