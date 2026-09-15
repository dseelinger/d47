using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A turn answered by someone other than the ship's AI is heard as them (#186).</summary>
public class AnAddressedSpeakerUsesTheirVoiceTests
{
    private static VoicePipeline Build(
        AudioArbiter arbiter, ITtsProvider aboard, ITtsProvider carrier, VoiceRole speakingAs) =>
        new(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = aboard,
            SpeakerFor = group => group == VoiceGroup.Carrier ? carrier : null,
            SpeakingAs = speakingAs,
            CuesEnabled = false,
            BedEnabled = false,
        };

    private static async IAsyncEnumerable<TurnEvent> Reply(string text)
    {
        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(TurnOutcome.Answered, TurnRoute.Model, text, null, null));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task TheCarrierCaptainSynthesisesWithTheCarrierSlotNotTheAboardOne()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var aboard = new TaggedProvider("aboard");
        var carrier = new TaggedProvider("carrier");

        var voice = Build(arbiter, aboard, carrier, VoiceRole.CarrierCaptain);

        await voice.RunAsync(Reply("Standing by."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(aboard.Asked);
        Assert.Equal(["Standing by."], carrier.Asked);
    }

    [Fact]
    public async Task TheCarrierCaptainsClipIsRadioTreated()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var voice = Build(arbiter, new TaggedProvider("aboard"), new TaggedProvider("carrier"), VoiceRole.CarrierCaptain);

        await voice.RunAsync(Reply("Standing by."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.EndsWith("(radio)", Assert.Single(sink.Played).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCarrierCaptainsCaptionNamesThem()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var seen = new List<AudioActivity>();
        arbiter.ActivityChanged += seen.Add;

        var voice = Build(arbiter, new TaggedProvider("aboard"), new TaggedProvider("carrier"), VoiceRole.CarrierCaptain);
        voice.CaptionSpeaker = "Captain";

        await voice.RunAsync(Reply("Standing by."), cancellationToken: TestContext.Current.CancellationToken);

        var speaking = Assert.Single(seen, activity => activity.Channel == AudioChannel.Speech);
        Assert.Equal("[Captain] Standing by.", speaking.Caption);
    }

    [Fact]
    public async Task ACrewMemberSynthesisesWithTheAboardProviderAndNoRadioLink()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var aboard = new TaggedProvider("aboard");
        var carrier = new TaggedProvider("carrier");

        var voice = Build(arbiter, aboard, carrier, VoiceRole.Crew);

        await voice.RunAsync(Reply("On it."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["On it."], aboard.Asked);
        Assert.Empty(carrier.Asked);
        Assert.DoesNotContain("(radio)", Assert.Single(sink.Played).Name, StringComparison.Ordinal);
    }

    /// <summary>Records what it was asked to say, under a name that identifies which slot called it.</summary>
    private sealed class TaggedProvider(string tag) : ITtsProvider
    {
        public List<string> Asked { get; } = [];

        public string Id => tag;

        public string Name => tag;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Silent);

        public Task<AudioClip> SynthesizeAsync(
            string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            Asked.Add(text);

            return Task.FromResult(new AudioClip(text, new byte[4_800 * 2], AudioFormat.Standard));
        }
    }

    /// <summary>Keeps what it was asked to play.</summary>
    private sealed class CollectingSink : IAudioSink
    {
        public List<AudioClip> Played { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request)
        {
            Played.Add(request.Clip);
            _ = Finished;
        }

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
