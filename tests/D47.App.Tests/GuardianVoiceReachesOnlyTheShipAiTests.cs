using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Which roles the Guardian voice treatment reaches, and which it never does (#225).</summary>
public class GuardianVoiceReachesOnlyTheShipAiTests
{
    private static (VoicePipeline Voice, List<AudioClip> Played) Build()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new OneToneProvider(),
            CuesEnabled = false,
            BedEnabled = false,

            // A marker rather than the real chain, since the chain itself is GuardianVoiceTests' job.
            GuardianColour = clip => clip with { Name = $"{clip.Name} (guardian)" },
        };

        return (voice, sink.Played);
    }

    private static async Task<string> NameOfWhatWasPlayed(VoiceRole role)
    {
        var (voice, played) = Build();

        await voice.AnnounceAsync(new Announcement($"line.{role}", "Scanning.") { Voice = role });

        return Assert.Single(played).Name;
    }

    [Fact]
    public async Task TheShipsAiIsGuardianTreatedWhenAnyTreatmentIsOn() =>
        Assert.EndsWith("(guardian)", await NameOfWhatWasPlayed(VoiceRole.ShipAi), StringComparison.Ordinal);

    [Fact]
    public async Task TheCrewIsNeverGuardianTreated() =>
        Assert.Equal("Scanning.", await NameOfWhatWasPlayed(VoiceRole.Crew));

    [Fact]
    public async Task TheOverTheAirRolesGetTheRadioNotTheGuardian()
    {
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.Comms), StringComparison.Ordinal);
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.CarrierCaptain), StringComparison.Ordinal);
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.TowerControl), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoTreatmentSwitchedOnLeavesTheShipsAiUnchanged()
    {
        var (voice, played) = Build();
        voice.GuardianColour = null;

        await voice.AnnounceAsync(new Announcement("routine.scan", "Scanning."));

        Assert.Equal("Scanning.", Assert.Single(played).Name);
    }

    /// <summary>A turn reply is the ship AI too, unless it answers as a crew member.</summary>
    [Fact]
    public async Task ATurnReplyIsGuardianTreatedUnlessItIsSpokenAsCrew()
    {
        var (voice, played) = Build();

        await voice.RunAsync(Reply("Scanning."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.EndsWith("(guardian)", Assert.Single(played).Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATurnReplySpokenAsCrewIsNot()
    {
        var (voice, played) = Build();
        voice.SpeakingAs = VoiceRole.Crew;

        await voice.RunAsync(Reply("Scanning."), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Scanning.", Assert.Single(played).Name);
    }

    private static async IAsyncEnumerable<TurnEvent> Reply(string text)
    {
        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(TurnOutcome.Answered, TurnRoute.Model, text, null, null));

        await Task.CompletedTask;
    }

    /// <summary>Synthesis with the network taken out.</summary>
    private sealed class OneToneProvider : ITtsProvider
    {
        public const int Samples = 4_800;

        public string Id => "one-tone";

        public string Name => "One tone";

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([new VoiceInfo("one-tone", "One Tone", "en-GB")]));

        public Task<AudioClip> SynthesizeAsync(
            string text,
            VoiceSelection voice,
            CancellationToken cancellationToken = default)
        {
            var pcm = new byte[Samples * 2];

            for (var index = 0; index < Samples; index++)
            {
                var value = (short)(Math.Sin(2 * Math.PI * 900 * index / 48_000.0) * 0.4 * short.MaxValue);

                pcm[index * 2] = (byte)(value & 0xFF);
                pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            return Task.FromResult(new AudioClip(text, pcm, AudioFormat.Standard));
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
