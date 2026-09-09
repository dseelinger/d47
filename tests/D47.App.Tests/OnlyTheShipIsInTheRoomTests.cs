using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary> Which voices arrive from the next seat and which arrive over a link. </summary>
public class OnlyTheShipIsInTheRoomTests
{
    private static (VoicePipeline Voice, List<AudioClip> Played) Build()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new OneToneProvider(),
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
    public async Task AMessageFromOutsideTheShipArrivesOverALink()
    {
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.Comms), StringComparison.Ordinal);
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.CarrierCaptain), StringComparison.Ordinal);
        Assert.EndsWith("(radio)", await NameOfWhatWasPlayed(VoiceRole.TowerControl), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheShipsAiAndItsCrewAreInTheRoomWithTheCommander()
    {
        // The requirement names these two and only these two.
        Assert.Equal("Scanning.", await NameOfWhatWasPlayed(VoiceRole.ShipAi));
        Assert.Equal("Scanning.", await NameOfWhatWasPlayed(VoiceRole.Crew));
    }

    /// <summary>
    /// A treated line is still one line on the queue, still on its own channel, and still carrying its
    /// caption.
    /// </summary>
    [Fact]
    public async Task TheLinkChangesTheSamplesAndNothingElseAboutTheLine()
    {
        var sink = new CollectingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        var seen = new List<AudioActivity>();
        arbiter.ActivityChanged += seen.Add;

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new OneToneProvider(),
        };

        await voice.AnnounceAsync(new Announcement("message.npc", "Scanning.") { Voice = VoiceRole.Comms });

        var speaking = Assert.Single(seen, activity => activity.Channel == AudioChannel.Speech);
        Assert.Equal("[Comms] Scanning.", speaking.Caption);

        // Same format, and longer by the carrier that stays open after the last word — 200 ms at 48 kHz.
        var clip = Assert.Single(sink.Played);
        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.Equal((OneToneProvider.Samples + 9_600) * 2, clip.Pcm.Length);
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
