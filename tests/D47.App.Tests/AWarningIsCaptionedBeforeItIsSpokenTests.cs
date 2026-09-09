using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The non-speech half of a warning reaches a Commander who is reading rather than hearing.
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

    /// <summary>Every distinct caption the arbiter reported, in the order they became audible.</summary>
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

        // The cue sits on the queue in front of the sentence, so the sentence only becomes audible once the
        // cue has finished — the queue doing its job, and the reason this has to be driven rather than merely
        // awaited.
        sink.FinishEverything();

        var captions = CaptionsFrom(heard);

        Assert.Contains(AlertCues.Caption(AlertCue.Interdiction), captions);
        Assert.Contains("Interdiction detected.", captions);
    }

    /// <summary>
    /// Marker first, then the words, which is the whole of what a cue buys: the median warning in the
    /// corpus is six to eight seconds ahead of the shooting, and the first second of that is spent on
    /// the word "interdiction".
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
    /// And the layer shows them as a pair, the marker above the line it is marking — which is what the
    /// roll-up window does with two one-line events and is why the marker is kept short.
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
    /// A callout with no cue is unchanged: nothing bracketed appears in front of an ordinary remark.
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
    /// The carrier is somebody else, and the caption says so — once, at the top of what they say, which
    /// is where a caption track puts a speaker ID.
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

    /// <summary>And d47's own lines carry no label.</summary>
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

    /// <summary>Plays nothing, and finishes what it was given only when asked to.</summary>
    private sealed class SilentSink : IAudioSink
    {
        private readonly List<long> _playing = [];

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new NoTap();

        public void Play(PlaybackRequest request) => _playing.Add(request.Id);

        /// <summary>Completes everything in flight, and everything that starts because of it.</summary>
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
