using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Invented chatter and Narrator lines are cut when Elite closes and stay refused until it runs again (#2012).</summary>
public class ChatterStopsWhenEliteClosesTests
{
    private static (VoicePipeline Voice, RecordingSink Sink) Build()
    {
        var sink = new RecordingSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = new NamedClipProvider(),
            CuesEnabled = false,
            BedEnabled = false,
        };

        arbiter.ActivityChanged += voice.Settle;
        return (voice, sink);
    }

    private static Announcement Chatter(string text) =>
        new(NpcChatter.LineKey, text) { Voice = VoiceRole.Comms, Speaker = "Dock Hand Reyes" };

    [Fact]
    public async Task AChatterLineBeingSpokenIsCutWhenEliteCloses()
    {
        var (voice, sink) = Build();

        await voice.AnnounceAsync(Chatter("Pad seven is yours when you want it."));
        var playing = sink.Started[0].Id;

        voice.GameRunning = false;

        Assert.Contains(playing, sink.Stopped);
    }

    [Fact]
    public async Task ChatterArrivingAfterEliteClosesIsRefusedEvenAfterAnIdleState()
    {
        var (voice, sink) = Build();

        voice.GameRunning = false;
        voice.EnterState(LoopState.Idle);
        voice.Settle(new AudioActivity(Channel: null, Caption: null, BedPlaying: false));

        await voice.AnnounceAsync(Chatter("Some people just like losing in style."));

        Assert.False(sink.Played("Some people just like losing in style."));
    }

    [Fact]
    public async Task SettlingFromAnAnswerDoesNotReopenChatterWhileEliteIsClosed()
    {
        var (voice, sink) = Build();

        voice.EnterState(LoopState.Listening);
        voice.GameRunning = false;
        voice.EnterState(LoopState.Answered);
        voice.Settle(new AudioActivity(Channel: null, Caption: null, BedPlaying: false));

        await voice.AnnounceAsync(Chatter("Some people just like losing in style."));

        Assert.False(sink.Played("Some people just like losing in style."));
    }

    [Fact]
    public async Task ChatterIsHeardAgainWhenEliteRuns()
    {
        var (voice, sink) = Build();

        voice.GameRunning = false;
        voice.GameRunning = true;

        await voice.AnnounceAsync(Chatter("Some people just like losing in style."));

        Assert.True(sink.Played("Some people just like losing in style."));
    }

    [Fact]
    public async Task OtherAnnouncementsAreHeardWhileEliteIsClosed()
    {
        var (voice, sink) = Build();

        voice.GameRunning = false;

        await voice.AnnounceAsync(new Announcement("route.progress", "One jump remaining."));

        Assert.True(sink.Played("One jump remaining."));
    }

    [Fact]
    public void NarratorAndChatterLinesAreChatterAndOthersAreNot()
    {
        Assert.True(Announcer.IsChatter(Chatter("Hello.") with { Origin = AnnouncementOrigin.InventedChatter }));
        Assert.True(Announcer.IsChatter(new Announcement("x", "y") { Voice = VoiceRole.Narrator }));
        Assert.True(Announcer.IsChatter(new Announcement("x", "y") { Chatter = TimeSpan.FromMinutes(1) }));
        Assert.True(Announcer.IsChatter(new Announcement("x", "y") { Origin = AnnouncementOrigin.InventedChatter }));
        Assert.False(Announcer.IsChatter(new Announcement("route.progress", "One jump remaining.")));
        Assert.False(Announcer.IsChatter(new Announcement("message.npc", "Docking granted.") { Voice = VoiceRole.Comms }));
    }

    private sealed class RecordingSink : IAudioSink
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

        public void Pause(long playbackId)
        {
        }

        public void Resume(long playbackId)
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        public void Finish(long id) => Finished?.Invoke(id);

        public bool Played(string text) =>
            Started.Exists(request => request.Name.StartsWith(text, StringComparison.Ordinal));

        private sealed class NoTap : IRenderReferenceTap
        {
            public event Action<RenderReferenceFrame>? Rendered;

            public void Dispose() => _ = Rendered;
        }
    }

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
