using System.Runtime.CompilerServices;
using D47.App.Input;
using D47.App.Panel;
using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Listening;
using D47.Core.Storage;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A capture opened by the comms push-to-talk is transcribed, handed on as Comms and run as a turn for
/// Comms; one opened by the primary key is run for the core.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ACommsCaptureReachesTheTurnAsCommsTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly HttpModelStore _models;
    private readonly ListenGate _gate;
    private readonly Listener _listener;
    private readonly TurnLoop _turns;
    private readonly TaskCompletionSource<(string Text, PttTarget Target)> _heard =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ACommsCaptureReachesTheTurnAsCommsTests()
    {
        var (settings, _, paths, registry, secrets) = TestSurface.CreateFull();

        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);

        _gate = new ListenGate(WasapiMicrophone.SampleRate, NullLogger<ListenGate>.Instance);
        var echo = new EchoCanceller(_gate, new NoTap(), WasapiMicrophone.SampleRate, NullLogger<EchoCanceller>.Instance);

        _models = new HttpModelStore(paths, NullLogger<HttpModelStore>.Instance);

        _listener = new Listener(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            voice,
            said: _ => { },
            prompted: _ => false,
            properNouns: () => [],
            recorder: () => null,
            shipName: () => "Test",
            withheld: _ => false,
            panel: new PanelViewModel(),
            _gate,
            echo,
            new WasapiMicrophone(echo, NullLogger<WasapiMicrophone>.Instance),
            new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance),
            _models,
            new BindsWatch(new DiskFileSystem(), TestSurface.BindingsFolder(paths), [], NullLogger.Instance),
            new PushToTalkKey(NullLogger<PushToTalkKey>.Instance),
            new BoundButton(),
            new WakeWordGate(),
            new StrongBox<DateTimeOffset?>(null))
        {
            TranscriberOverride = new SaysWhereAmI(),
        };

        // As the host wires them.
        _gate.Captured += _listener.TranscribeAsync;
        _listener.Heard += (text, target) => _heard.TrySetResult((text, target));

        var spend = new SpendTracker();

        _turns = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: false),
            spend,
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance);
    }

    [Fact]
    public async Task TheCommsKeyRunsATurnForComms()
    {
        _listener.CommsSources.KeyPressed();
        _gate.Write([.. Enumerable.Repeat(0.1f, WasapiMicrophone.SampleRate)]);
        _listener.CommsSources.KeyReleased();

        var (text, target) = await _heard.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Equal(PttTarget.Comms, target);

        await foreach (var _ in _turns.RunAsync(text, InputSource.Spoken, target, TestContext.Current.CancellationToken))
        {
        }

        Assert.Equal(PttTarget.Comms, _turns.Target);
        Assert.Equal(InputSource.Spoken, _turns.Source);
    }

    [Fact]
    public async Task ACaptureNoCommsKeyOpenedIsForTheCore()
    {
        _gate.KeyDown(DateTimeOffset.Now, PttTarget.Core);
        _gate.Write([.. Enumerable.Repeat(0.1f, WasapiMicrophone.SampleRate)]);
        _gate.KeyUp(PttTarget.Core);

        var (_, target) = await _heard.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Equal(PttTarget.Core, target);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _models.Dispose();
    }

    private sealed class SaysWhereAmI : ISpeechTranscriber
    {
        public string? Model => "fake";

        public bool IsReady => true;

        public Task<Transcription> TranscribeAsync(
            Utterance utterance,
            IReadOnlyList<string> properNouns,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Transcription("where am I"));

        public void Dispose()
        {
        }
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
