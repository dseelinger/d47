using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Any speaker with a picture puts it in the avatar while their line plays, the Narrator included: the same picture
/// the Conversation page shows them with. The ship's AI puts none there, so the avatar shows the core.
/// </summary>
public sealed class EverySpeakerWithAPictureTakesTheAvatarTests
{
    private const string Core = "warden";

    private static string? AvatarPicture(Announcement announcement)
    {
        var cast = new VoiceCast
        {
            Voices = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["af_heart"] = new("af_heart", "Heart", "en-US", "Female"),
                ["am_michael"] = new("am_michael", "Michael", "en-US", "Male"),
            },
        };
        cast.Assign(VoiceRole.CarrierCaptain, "af_heart");
        cast.Assign(VoiceRole.TowerControl, "am_michael");

        var crew = new ShipCrew { Members = [new CrewMember("Ava Ross", 4521)] };

        return Announcer.AvatarPicture(
            Announcer.ConversationPicture(announcement, Core, role => cast.GenderOf(cast.For(role).VoiceId), crew),
            Core);
    }

    [Fact]
    public void TheNarratorShowsTheNarratorsPicture() =>
        Assert.Equal("narrator", AvatarPicture(new Announcement("narrator.lull", "The stars wait.") { Voice = VoiceRole.Narrator }));

    [Fact]
    public void TheCaptainShowsTheirPicture() =>
        Assert.Equal("captain.woman", AvatarPicture(new Announcement("carrier.jump", "Jump in fifteen minutes.") { Voice = VoiceRole.CarrierCaptain }));

    [Fact]
    public void TheTowerShowsTheirPicture() =>
        Assert.Equal("tower.man", AvatarPicture(new Announcement("carrier.dock", "Cleared to land.") { Voice = VoiceRole.TowerControl }));

    [Fact]
    public void AHiredPilotShowsTheirPicture() =>
        Assert.Equal("crew.4521", AvatarPicture(new Announcement("crew.remark", "Contact.") { Voice = VoiceRole.Crew, Speaker = "Ava Ross" }));

    [Fact]
    public void ACastMemberShowsTheirOwnPicture() =>
        Assert.Equal(
            "story.night-watch.vasquez",
            AvatarPicture(new Announcement("story.clue.night-watch.0", "Keep your head down.")
            {
                Voice = VoiceRole.Crew,
                Speaker = "Vasquez",
                Picture = "story.night-watch.vasquez",
            }));

    [Fact]
    public void TheShipsAiLeavesTheCoreInTheAvatar() =>
        Assert.Null(AvatarPicture(new Announcement("fuel.low", "Fuel at twenty percent.")));

    [Fact]
    public void AnUnnamedCommsSenderLeavesTheCoreInTheAvatar() =>
        Assert.Null(AvatarPicture(new Announcement("message.npc", "Halt.") { Voice = VoiceRole.Comms, Speaker = "Pirate" }));

    [Fact]
    public async Task AnAddressedSpeakersReplyCarriesTheirPictureToWhatIsAudible()
    {
        var (voice, heard) = Build();
        voice.Picture = "narrator";
        voice.SpeakingAs = VoiceRole.Narrator;

        await voice.RunAsync(Turn("It began at Shinrarta."), cancellationToken: TestContext.Current.CancellationToken);

        lock (heard)
        {
            Assert.Contains(heard, activity => activity.Channel == AudioChannel.Speech && activity.Picture == "narrator");
        }
    }

    [Fact]
    public async Task TheShipsOwnReplyCarriesNoPicture()
    {
        var (voice, heard) = Build();

        await voice.RunAsync(Turn("Standing by."), cancellationToken: TestContext.Current.CancellationToken);

        lock (heard)
        {
            Assert.Contains(heard, activity => activity.Channel == AudioChannel.Speech);
            Assert.All(heard, activity => Assert.Null(activity.Picture));
        }
    }

    private static (VoicePipeline Voice, List<AudioActivity> Heard) Build()
    {
        var arbiter = new AudioArbiter(new CollectingSink(), NullLogger<AudioArbiter>.Instance).Start();
        var heard = new List<AudioActivity>();

        arbiter.ActivityChanged += activity =>
        {
            lock (heard)
            {
                heard.Add(activity);
            }
        };

        var tts = new Silent();

        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
        {
            Tts = tts,
            SpeakerFor = _ => tts,
            Voice = new VoiceSelection("the-persona's-voice"),
            CuesEnabled = false,
            BedEnabled = false,
        };

        return (voice, heard);
    }

    private static async IAsyncEnumerable<TurnEvent> Turn(string text)
    {
        await Task.CompletedTask;

        yield return new TurnEvent.TextDelta(text);
        yield return new TurnEvent.Completed(new TurnResult(TurnOutcome.Answered, TurnRoute.KeywordRouter, text, Effort: null, Cost: null));
    }

    /// <summary>Synthesis with the network taken out: a tenth of a second of silence for any line.</summary>
    private sealed class Silent : ITtsProvider
    {
        public string Id => TtsProviderCatalog.EdgeId;

        public string Name => TtsProviderCatalog.EdgeId;

        public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(VoiceCatalogue.Of([]));

        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioClip(text, new byte[4_800 * 2], AudioFormat.Standard));
    }

    /// <summary>Takes every clip and never finishes one, so the first line stays current.</summary>
    private sealed class CollectingSink : IAudioSink
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

        public void Pause(long playbackId)
        {
        }

        public void Resume(long playbackId)
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
