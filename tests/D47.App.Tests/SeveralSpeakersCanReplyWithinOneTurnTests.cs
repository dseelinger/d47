using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A turn answered by several speakers draws each reply under its own speaker with its own provenance, and speaks
/// each in its own voice, in order, with the clip it was spoken in kept for it.
/// </summary>
public sealed class SeveralSpeakersCanReplyWithinOneTurnTests
{
    private static TurnResult Answered(string text) =>
        new(TurnOutcome.Answered, TurnRoute.KeywordRouter, text, Effort: null, Cost: null);

    private static readonly TurnEvent[] Replies =
    [
        new TurnEvent.Addressed(VoiceRole.Crew, "Juno", 1, Cast: "juno"),
        new TurnEvent.TextDelta("First."),
        new TurnEvent.Completed(Answered("First.")),
        new TurnEvent.Addressed(VoiceRole.Crew, "Harrow", 1, Cast: "harrow"),
        new TurnEvent.TextDelta("Second."),
        new TurnEvent.Completed(Answered("Second.")),
    ];

    private static async IAsyncEnumerable<TurnEvent> TwoReplies()
    {
        await Task.CompletedTask;

        foreach (var turnEvent in Replies)
        {
            yield return turnEvent;
        }
    }

    [Fact]
    public void EachReplyIsDrawnUnderItsOwnSpeakerAndPictureWithItsOwnProvenance()
    {
        var model = new PanelViewModel();
        model.Append("Hello", voice: TranscriptVoice.Commander);

        var presenter = new TurnPresenter(model, "core.warden", addressed => $"picture.{addressed.Name}");

        foreach (var turnEvent in Replies)
        {
            presenter.On(turnEvent);
        }

        var replies = model.Segments(TranscriptPage.Conversation).Skip(1).ToList();

        Assert.Equal(["Juno", "Harrow"], replies.Select(reply => reply.Speaker));
        Assert.Equal(["picture.Juno", "picture.Harrow"], replies.Select(reply => reply.Picture));
        Assert.All(replies, reply => Assert.NotNull(reply.Provenance));
    }

    [Fact]
    public void ATurnWithOneAddressedSpeakerKeepsOneProvenanceLine()
    {
        var model = new PanelViewModel();
        model.Append("Hello", voice: TranscriptVoice.Commander);

        var presenter = new TurnPresenter(model, "core.warden");
        presenter.On(new TurnEvent.Addressed(VoiceRole.Crew, "Juno", 1));
        presenter.On(new TurnEvent.TextDelta("Only."));
        presenter.On(new TurnEvent.Completed(Answered("Only.")));

        var reply = Assert.Single(model.Segments(TranscriptPage.Conversation).Skip(1));

        Assert.Equal("Juno", reply.Speaker);
        Assert.NotNull(reply.Provenance);
    }

    [Fact]
    public async Task EachReplyIsSpokenInItsOwnVoiceInOrderAndKeepsItsOwnClip()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var kokoro = new Recording();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = kokoro,
            PinnedFor = id => id == TtsProviderCatalog.KokoroId ? kokoro : null,
            Voice = new VoiceSelection("ship"),
            CuesEnabled = false,
            BedEnabled = false,
        };

        var voices = new Dictionary<string, string> { ["Juno"] = "am_michael", ["Harrow"] = "bf_emma" };

        await voice.RunAsync(
            TwoReplies(),
            onEvent: turnEvent =>
            {
                if (turnEvent is TurnEvent.Addressed addressed)
                {
                    voice.Pinned = new PinnedVoice(StorySpeaker.Kokoro, voices[addressed.Name]);
                    voice.SpeakingAs = VoiceRole.Crew;
                    voice.CaptionSpeaker = addressed.Name;
                }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([("First.", "am_michael"), ("Second.", "bf_emma")], kokoro.Asked);
        Assert.Equal(["am_michael", "bf_emma"], voice.KeptReplies.Select(clip => clip!.VoiceId));
    }

    private sealed class Recording : ITtsProvider
    {
        public List<(string Text, string? Voice)> Asked { get; } = [];

        public string Id => TtsProviderCatalog.KokoroId;

        public string Name => Id;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
        {
            lock (Asked)
            {
                Asked.Add((text, voice.VoiceId));
            }

            return Task.FromResult(new AudioClip(text, TestTone.Pcm(4_800), AudioFormat.Standard));
        }
    }
}
