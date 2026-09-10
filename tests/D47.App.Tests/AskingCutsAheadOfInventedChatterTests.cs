using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>What talking to the ship may silence to reach the Commander, and what it must leave alone (#61).</summary>
public class AskingCutsAheadOfInventedChatterTests
{
    private const string Answer = "Fuel is at ninety percent.";

    private static (VoicePipeline Voice, HoldingSink Sink) Build()
    {
        var sink = new HoldingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new NamedClipProvider(),
            CuesEnabled = false,
            BedEnabled = false,
        };

        return (voice, sink);
    }

    private static Announcement Chatter(string text) =>
        new(NpcChatter.LineKey, text) { Voice = VoiceRole.Comms, Speaker = "Dock Hand Reyes" };

    private static async IAsyncEnumerable<TurnEvent> Reply()
    {
        yield return new TurnEvent.TextDelta(Answer);
        yield return new TurnEvent.Completed(
            new TurnResult(TurnOutcome.Answered, TurnRoute.Model, Answer, null, null));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task AChatterLineBeingSpokenIsCutOffAndTheReplyIsHeard()
    {
        var (voice, sink) = Build();

        await voice.AnnounceAsync(Chatter("Pad seven is yours when you want it."));
        var chatter = sink.Started[0].Id;

        await voice.RunAsync(Reply(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(chatter, sink.Stopped);
        Assert.True(sink.Played(Answer));
    }

    /// <summary>Before the reply, and not once it has finished either.</summary>
    [Fact]
    public async Task ChatterStillWaitingItsTurnIsNeverSpoken()
    {
        var (voice, sink) = Build();

        await voice.AnnounceAsync(Chatter("Pad seven is yours when you want it."));
        await voice.AnnounceAsync(Chatter("Understood, on approach."));
        await voice.AnnounceAsync(Chatter("Mind the scaffold on your left."));

        await voice.RunAsync(Reply(), cancellationToken: TestContext.Current.CancellationToken);

        sink.FinishEverything();

        Assert.False(sink.Played("Understood, on approach."));
        Assert.False(sink.Played("Mind the scaffold on your left."));
    }

    /// <summary>Words the game actually sent, and a callout the Commander switched on.</summary>
    [Fact]
    public async Task RelayedCommsAndAnOrdinaryCalloutAreLeftWhereTheyAre()
    {
        var (voice, sink) = Build();

        await voice.AnnounceAsync(
            new Announcement("message.npc", "Docking request granted.") { Voice = VoiceRole.Comms });

        await voice.AnnounceAsync(new Announcement("route.progress", "One jump remaining."));

        await voice.RunAsync(Reply(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(sink.Stopped);

        sink.FinishEverything();

        Assert.True(sink.Played("Docking request granted."));
        Assert.True(sink.Played("One jump remaining."));
    }

    /// <summary>Nobody asked for it, so it waits its turn like everything else.</summary>
    [Fact]
    public async Task AnUnpromptedLineOfItsOwnDoesNotCutChatter()
    {
        var (voice, sink) = Build();

        await voice.AnnounceAsync(Chatter("Pad seven is yours when you want it."));
        var chatter = sink.Started[0].Id;

        await voice.AnnounceAsync(new Announcement("route.progress", "One jump remaining."));

        Assert.DoesNotContain(chatter, sink.Stopped);
    }

    /// <summary>
    /// The case the arbiter's own comment names: the cue playing immediately in front of a sentence is a
    /// fifth of a second long, and the reply arriving behind it must not truncate it.
    /// </summary>
    [Fact]
    public async Task TheLoopStateCueInFrontOfAReplyIsNotTruncated()
    {
        var (voice, sink) = Build();

        voice.CuesEnabled = true;

        await voice.RunAsync(Reply(), cancellationToken: TestContext.Current.CancellationToken);

        // The thinking cue, queued by entering the state and still playing when the words arrived.
        var cue = sink.Started[0];

        Assert.False(cue.Clip.Name.StartsWith(Answer, StringComparison.Ordinal));
        Assert.DoesNotContain(cue.Id, sink.Stopped);
    }

    /// <summary>
    /// Transcription and the model together run to several seconds, and a four-line exchange finishes
    /// inside them, so the microphone opening is what has to stop it.
    /// </summary>
    [Fact]
    public async Task OpeningTheMicrophoneCutsChatterBeforeAWordHasBeenTranscribed()
    {
        var (voice, sink) = Build();

        await voice.AnnounceAsync(Chatter("Pad seven is yours when you want it."));
        var chatter = sink.Started[0].Id;

        voice.EnterState(LoopState.Listening);

        Assert.Contains(chatter, sink.Stopped);
    }

    /// <summary>
    /// The flag the chatter loop reads to abandon the rest of an exchange, since the lines after the one
    /// that was cut are synthesised later and would otherwise queue up behind the answer.
    /// </summary>
    [Fact]
    public async Task ItIsEngagedFromTheMicrophoneOpeningUntilTheLoopSettles()
    {
        var (voice, _) = Build();

        Assert.False(voice.Engaged);

        voice.EnterState(LoopState.Listening);
        Assert.True(voice.Engaged);

        await voice.RunAsync(Reply(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(voice.Engaged);

        voice.Settle(new AudioActivity(Channel: null, Caption: null, BedPlaying: false));
        Assert.False(voice.Engaged);
    }

    /// <summary>A sink that starts everything, stops nothing on its own, and finishes only when told.</summary>
    private sealed class HoldingSink : IAudioSink
    {
        public List<PlaybackRequest> Started { get; } = [];

        public List<long> Stopped { get; } = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request) => Started.Add(request);

        public void Stop(long playbackId) => Stopped.Add(playbackId);

        public void StopAll()
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        /// <summary>
        /// Whether a line was ever started. Matched by prefix, because a line arriving over a link is
        /// renamed by the treatment applied to it.
        /// </summary>
        public bool Played(string text) =>
            Started.Exists(request => request.Clip.Name.StartsWith(text, StringComparison.Ordinal));

        /// <summary>Drains the queue, so what was never started can be told apart from what was.</summary>
        public void FinishEverything()
        {
            for (var index = 0; index < Started.Count; index++)
            {
                var id = Started[index].Id;

                if (!Stopped.Contains(id))
                {
                    Finished?.Invoke(id);
                }
            }
        }

        private sealed class NoTap : IRenderReferenceTap
        {
            public event Action<RenderReferenceFrame>? Rendered;

            public void Dispose() => _ = Rendered;
        }
    }

    /// <summary>Synthesis with the network taken out: one clip per sentence, named by what it says.</summary>
    private sealed class NamedClipProvider : ITtsProvider
    {
        public string Id => "named-clip";

        public string Name => "Named clip";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("named-clip", "Named Clip", "en-GB")]));

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioClip(text, new byte[9_600], AudioFormat.Standard));
    }
}
