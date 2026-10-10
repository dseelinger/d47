using System.Runtime.CompilerServices;
using D47.App.Input;
using D47.App.Panel;
using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Listening;
using D47.Core.Storage;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// An utterance that arrives with no speech model loaded is answered with a sentence saying so, at once
/// and on the thread that captured it, and goes no further.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnUtteranceWithNoModelSaysSoTests : IDisposable
{
    private const string Cannot = "I heard you, but I have no speech model loaded to understand it.";

    private readonly List<(string Text, int Thread)> _said = [];
    private readonly List<LoopState> _entered = [];
    private readonly List<string> _heard = [];
    private int _prompted;

    private readonly HttpModelStore _models;
    private readonly Listener _listener;

    public AnUtteranceWithNoModelSaysSoTests()
    {
        var (settings, _, paths, _, secrets) = TestSurface.CreateFull();

        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);
        voice.StateEntered += _entered.Add;

        var gate = new ListenGate(WasapiMicrophone.SampleRate, NullLogger<ListenGate>.Instance);
        var echo = new EchoCanceller(gate, new NoTap(), WasapiMicrophone.SampleRate, NullLogger<EchoCanceller>.Instance);

        _models = new HttpModelStore(paths, NullLogger<HttpModelStore>.Instance);

        _listener = new Listener(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            voice,
            said: text => _said.Add((text, Environment.CurrentManagedThreadId)),
            prompted: _ =>
            {
                _prompted++;
                return false;
            },
            properNouns: () => [],
            recorder: () => null,
            shipName: () => "Test",
            panel: new PanelViewModel(),
            gate,
            echo,
            new WasapiMicrophone(echo, NullLogger<WasapiMicrophone>.Instance),
            new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance),
            _models,
            new BindsWatch(new DiskFileSystem(), TestSurface.BindingsFolder(paths), [], NullLogger.Instance),
            new PushToTalkKey(NullLogger<PushToTalkKey>.Instance),
            new BoundButton(),
            new WakeWordGate(),
            new StrongBox<DateTimeOffset?>(null));

        _listener.Heard += _heard.Add;
    }

    [Fact]
    public void TheSentenceIsSaidBeforeTheCallReturns()
    {
        _listener.TranscribeAsync(Speech());

        Assert.Equal([(Cannot, Environment.CurrentManagedThreadId)], _said);
    }

    [Fact]
    public void TheLoopGoesBackToIdle()
    {
        _listener.TranscribeAsync(Speech());

        Assert.Equal(LoopState.Idle, _entered[^1]);
    }

    [Fact]
    public void NothingIsHandedOn()
    {
        _listener.TranscribeAsync(Speech());

        Assert.Empty(_heard);
        Assert.Equal(0, _prompted);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _models.Dispose();
    }

    /// <summary>A second of sound that is not digital silence.</summary>
    private static Utterance Speech() =>
        new([.. Enumerable.Repeat(0.1f, WasapiMicrophone.SampleRate)], WasapiMicrophone.SampleRate);

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
