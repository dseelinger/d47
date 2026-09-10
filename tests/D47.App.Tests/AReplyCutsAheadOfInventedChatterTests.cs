using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>What a turn reply may silence to reach the Commander, and what it must leave alone (#61).</summary>
public class AReplyCutsAheadOfInventedChatterTests
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
    /// The flag the chatter loop reads to abandon the rest of an exchange, since the lines after the one
    /// that was cut are synthesised later and would otherwise queue up behind the reply.
    /// </summary>
    [Fact]
    public async Task ItSaysItIsReplyingWhileTheReplyIsBeingSpokenAndNotAfterwards()
    {
        var (voice, _) = Build();
        var provider = (NamedClipProvider)voice.Tts!;
        var midReply = false;

        provider.Synthesising = () => midReply = voice.Replying;

        Assert.False(voice.Replying);

        await voice.RunAsync(Reply(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(midReply);
        Assert.False(voice.Replying);
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

        /// <summary>Called as each sentence is rendered, for asserting what was true at that moment.</summary>
        public Action? Synthesising { get; set; }

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("named-clip", "Named Clip", "en-GB")]));

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default)
        {
            Synthesising?.Invoke();

            return Task.FromResult(new AudioClip(text, new byte[9_600], AudioFormat.Standard));
        }
    }
}
