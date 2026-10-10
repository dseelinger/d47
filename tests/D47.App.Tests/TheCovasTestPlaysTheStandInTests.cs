using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The COVAS Test row plays the bundled stand-in in the audition group and says so.</summary>
[Trait("Category", "Integration")]
public class TheCovasTestPlaysTheStandInTests : IDisposable
{
    private readonly SpeechClients _speech;
    private readonly AudioArbiter _arbiter;
    private readonly VoiceAuditions _auditions;
    private readonly List<AudioActivity> _activity = [];

    public TheCovasTestPlaysTheStandInTests()
    {
        var (settings, _, paths, _, secrets) = TestSurface.CreateFull();
        var personas = new PersonaHost();

        _arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        _arbiter.ActivityChanged += _activity.Add;

        var voice = new VoicePipeline(_arbiter, CueLibrary.Load, NullLoggerFactory.Instance);

        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            new DiskFileSystem(),
            personas,
            voice,
            _arbiter,
            new OwnVoice(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()),
            new CustomVoices(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()),
            () => throw new InvalidOperationException("No crew seat is looked up here."));

        _auditions = new VoiceAuditions(_speech, _arbiter, settings, personas);
    }

    public void Dispose() => _speech.Dispose();

    [Fact]
    public void TheCovasTestSaysItPlayedTheStandIn()
    {
        Assert.Equal("That was a stand-in voice through the COVAS reverb.", _auditions.CovasTest());
    }

    [Fact]
    public void TheCovasTestPlaysOneClipInTheAuditionGroup()
    {
        _auditions.CovasTest();

        Assert.Contains(
            _activity,
            activity => activity.Channel == AudioChannel.Speech && activity.Caption == StandInVoice.Clip.Name);

        _activity.Clear();
        _arbiter.DropGroup(VoiceAuditions.Group);

        Assert.All(_activity, activity => Assert.Null(activity.Channel));
        Assert.NotEmpty(_activity);
    }

    private sealed class SilentSink : IAudioSink
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
    }

    private sealed class NoTap : IRenderReferenceTap
    {
        public event Action<RenderReferenceFrame>? Rendered;

        public void Dispose() => _ = Rendered;
    }
}
