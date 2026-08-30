using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The non-speech half of a warning reaches a Commander who is reading rather than hearing
/// (<a href="https://github.com/dseelinger/d47/issues/201">#201</a>).
/// <para>
/// <b>The wiring rather than the wording.</b> <c>AlertCues.Caption</c> owns what each cue is
/// called and is asserted in Core; what is asserted here is that a cue enqueued ahead of a
/// callout actually carries it onto the queue, and that the caption layer shows the marker before
/// the sentence. The two halves fail independently — a correct caption nothing attaches sounds
/// exactly like no caption at all, and reports as one.
/// </para>
/// </summary>
public class AWarningIsCaptionedBeforeItIsSpokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private static (VoicePipeline Voice, List<AudioActivity> Heard, SilentSink Sink) Build()
    {
        var sink = new SilentSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var heard = new List<AudioActivity>();
        arbiter.ActivityChanged += heard.Add;

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new OneToneProvider(),
        };

        return (voice, heard, sink);
    }

    /// <summary>
    /// Every distinct caption the arbiter reported, in the order they became audible.
    /// <para>
    /// Distinct because the arbiter re-raises a whole snapshot for every change to anything
    /// audible, so the current clip's caption arrives again each time — which is the reason the
    /// caption layer takes an utterance id at all.
    /// </para>
    /// </summary>
    private static List<string> CaptionsFrom(List<AudioActivity> heard) =>
    [
        .. heard
            .Select(activity => activity.Caption)
            .Where(caption => caption is { Length: > 0 })
            .Select(caption => caption!)
            .Distinct(StringComparer.Ordinal),
    ];

    [Fact]
    public async Task TheCueGoesOnTheQueueWithItsOwnCaption()
    {
        var (voice, heard, sink) = Build();

        await voice.AnnounceAsync(new Announcement("danger.interdiction", "Interdiction detected.")
        {
            Urgency = CalloutUrgency.Urgent,
            Cue = AlertCue.Interdiction,
        });

        // The cue sits on the queue in front of the sentence, so the sentence only becomes
        // audible once the cue has finished — the queue doing its job, and the reason this has to
        // be driven rather than merely awaited.
        sink.FinishEverything();

        var captions = CaptionsFrom(heard);

        Assert.Contains(AlertCues.Caption(AlertCue.Interdiction), captions);
        Assert.Contains("Interdiction detected.", captions);
    }

    /// <summary>
    /// <b>Marker first, then the words</b>, which is the whole of what a cue buys: the median
    /// warning in the corpus is six to eight seconds ahead of the shooting, and the first second
    /// of that is spent on the word "interdiction". A reader who got the marker after the sentence
    /// would have the information and not the head start.
    /// </summary>
    [Fact]
    public async Task TheMarkerArrivesAheadOfTheSentence()
    {
        var (voice, heard, sink) = Build();

        await voice.AnnounceAsync(new Announcement("danger.heat", "Taking heat damage.")
        {
            Urgency = CalloutUrgency.Urgent,
            Cue = AlertCue.Overheating,
        });

        sink.FinishEverything();

        Assert.Equal(
            [AlertCues.Caption(AlertCue.Overheating), "Taking heat damage."],
            CaptionsFrom(heard));
    }

    /// <summary>
    /// And the layer shows them as a pair, the marker above the line it is marking — which is
    /// what the roll-up window does with two one-line events and is why the marker is kept short.
    /// </summary>
    [Fact]
    public void TheMarkerAndTheLineShareTheWindow()
    {
        var layer = new CaptionLayer();

        layer.Say(AlertCues.Caption(AlertCue.UnderFire), Now, utterance: 1);
        layer.Say("We are under attack.", Now, utterance: 2);

        Assert.Equal([AlertCues.Caption(AlertCue.UnderFire), "We are under attack."], layer.Lines);
    }

    /// <summary>
    /// A callout with no cue is unchanged: nothing bracketed appears in front of an ordinary
    /// remark. Captioning every sound is captioning noise, and a band that flashes before every
    /// utterance is one nobody reads.
    /// </summary>
    [Fact]
    public async Task ARemarkWithNoCueGetsNoMarker()
    {
        var (voice, heard, sink) = Build();

        await voice.AnnounceAsync(new Announcement("routine.fuel", "Fuel is at nineteen per cent."));
        sink.FinishEverything();

        Assert.DoesNotContain(
            heard,
            activity => activity.Caption is { } caption && caption.StartsWith('['));
    }

    /// <summary>
    /// <b>The carrier is somebody else, and the caption says so</b> — once, at the top of what
    /// they say, which is where a caption track puts a speaker ID. Repeating it on every sentence
    /// of one transmission would be announcing a change of speaker that did not happen.
    /// </summary>
    [Theory]
    [InlineData(VoiceRole.TowerControl, "[Tower] ")]
    [InlineData(VoiceRole.CarrierCaptain, "[Carrier] ")]
    public async Task SomebodyWhoIsNotD47IsNamedOnTheirFirstLine(VoiceRole role, string named)
    {
        var (voice, heard, sink) = Build();

        await voice.AnnounceAsync(new Announcement($"carrier.{role}", "Jump plotted. One more thing.")
        {
            Voice = role,
        });

        sink.FinishEverything();

        var captions = CaptionsFrom(heard);

        Assert.StartsWith(named, captions[0], StringComparison.Ordinal);

        foreach (var later in captions.Skip(1))
        {
            Assert.DoesNotContain(named, later, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// And d47's own lines carry no label. It is the voice a caption band beside a cockpit is
    /// understood to belong to, so naming it on every line is the noise the standard's "only when
    /// they cannot be visually identified" exists to keep out.
    /// </summary>
    [Fact]
    public async Task TheShipsAiIsNeverNamedOnItsOwnCaptions()
    {
        var (voice, heard, sink) = Build();

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));
        sink.FinishEverything();

        Assert.DoesNotContain(heard, activity => activity.Caption is { } said && said.Contains('['));
    }

    /// <summary>Synthesis with the network taken out; a short tone is enough to carry a caption.</summary>
    private sealed class OneToneProvider : ITtsProvider
    {
        public string Id => "one-tone";

        public string Name => "One tone";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("one-tone", "One Tone", "en-GB")]));

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioClip(text, new byte[4_800], AudioFormat.Standard));
    }

    /// <summary>
    /// Plays nothing, and finishes what it was given only when asked to.
    /// <para>
    /// <b>The queue has to be driven, not merely awaited.</b> A cue is enqueued ahead of the
    /// sentence it marks, so the sentence does not become audible — and its caption is not
    /// reported — until the cue completes. A sink that never finishes anything reports the marker
    /// and nothing else, which reads exactly like the sentence having lost its caption.
    /// </para>
    /// </summary>
    private sealed class SilentSink : IAudioSink
    {
        private readonly List<long> _playing = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request) => _playing.Add(request.Id);

        /// <summary>
        /// Completes everything in flight, and everything that starts because of it. A loop
        /// rather than a snapshot, because finishing one clip is what starts the next.
        /// </summary>
        public void FinishEverything()
        {
            while (_playing.Count > 0)
            {
                var playing = _playing[0];
                _playing.RemoveAt(0);
                Finished?.Invoke(playing);
            }
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
