using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A voice list arriving for the ship's provider gives the core aboard a voice; one for any other provider gives nothing.</summary>
public class AListArrivingPairsTheShipsProviderTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly SpeechClients _speech;
    private readonly SettingsService _settings;
    private readonly PersonaHost _personas = new();
    private readonly VoicePairer _pairer;

    public AListArrivingPairsTheShipsProviderTests()
    {
        var (settings, _, paths, registry, secrets) = TestSurface.CreateFull();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);

        _settings = settings;
        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            _personas,
            voice,
            arbiter,
            new OwnVoice(paths.Data, new DpapiSecretProtector()),
            new CustomVoices(paths.Data, new DpapiSecretProtector()),
            () => throw new InvalidOperationException("No crew seat is looked up here."));

        _settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with { Speech = current.Speech with { Provider = TtsProviderCatalog.KokoroId } });

        var spend = new SpendTracker();

        _pairer = new VoicePairer(
            _speech,
            settings,
            _personas,
            new TurnLoop(
                registry,
                new KeywordRouter(registry),
                new LlmAvailabilityState(providerConfigured: false),
                spend,
                PriceTable.Default,
                NullLogger<TurnLoop>.Instance),
            spend,
            NullLogger.Instance);
    }

    public void Dispose() => _speech.Dispose();

    [Fact]
    public async Task AListForAProviderNeitherSlotIsOnWritesNoVoice()
    {
        _speech.HoldVoicesForTest(TtsProviderCatalog.KokoroId, Offering());
        _speech.HoldVoicesForTest(TtsProviderCatalog.CartesiaId, Offering());

        _speech.AnnounceVoicesForTest(TtsProviderCatalog.CartesiaId);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(_personas.Current.Id, _settings.Current.Persona.Voices.Keys);
    }

    [Fact]
    public async Task AListForTheShipsProviderGivesTheCoreAboardAVoice()
    {
        _speech.HoldVoicesForTest(TtsProviderCatalog.KokoroId, Offering());

        _speech.AnnounceVoicesForTest(TtsProviderCatalog.KokoroId);

        await WaitForAsync(() => _settings.Current.Persona.Voices.ContainsKey(_personas.Current.Id));
    }

    private static VoiceCatalogue Offering() =>
        VoiceCatalogue.Of([.. Enumerable.Range(1, 40).Select(number => new VoiceInfo($"voice{number}", $"Voice {number}", "en-US"))]);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "No voice was written within five seconds.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
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
