using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A voice list arriving for the ship's provider gives the core aboard and the current story's unpinned cast a voice; one
/// for any other provider gives nothing.
/// </summary>
[Trait("Category", "Integration")]
public class AListArrivingPairsTheShipsProviderTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly SpeechClients _speech;
    private readonly SettingsService _settings;
    private readonly PersonaHost _personas = new();
    private readonly MemoryFileSystem _files = new();
    private readonly Func<VoicePairer> _startPairing;
    private VoicePairer? _paired;

    private VoicePairer Pairer => _paired ?? throw new InvalidOperationException("Call StartPairing first.");

    public AListArrivingPairsTheShipsProviderTests()
    {
        var (settings, _, paths, registry, secrets) = TestSurface.CreateFull();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);

        SeededKokoro.Install(_files, Path.Combine(paths.Data, "models", "kokoro"));

        _settings = settings;
        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            _files,
            _personas,
            voice,
            arbiter,
            new OwnVoice(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()),
            new CustomVoices(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()));

        _settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with { Speech = current.Speech with { Provider = TtsProviderCatalog.KokoroId } });

        var spend = new SpendTracker();

        _startPairing = () => _paired = new VoicePairer(
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
    public void AListForAProviderNeitherSlotIsOnIsNotPaired()
    {
        _startPairing();

        Assert.True(_paired!.IsAboardOrCarrier(TtsProviderCatalog.KokoroId));
        Assert.False(_paired.IsAboardOrCarrier(TtsProviderCatalog.CartesiaId));
    }

    [Fact]
    public async Task AListForTheShipsProviderGivesTheCoreAboardAVoice()
    {
        _startPairing();

        _speech.Apply();

        await WaitForAsync(() => _settings.Current.Persona.Voices.ContainsKey(_personas.Current.Id));
    }

    [Fact]
    public async Task AListForTheShipsProviderPairsTheCurrentStorysUnpinnedCast()
    {
        var secret = new StorySecret
        {
            Id = "a-story",
            Secret = "Hidden.",
            End = "An end.",
            Cast =
            [
                new StorySpeaker { Id = "hand", Name = "Ren", Who = "A dock hand.", Provider = StorySpeaker.Kokoro, Voice = "bm_george" },
                new StorySpeaker { Id = "kept", Name = "Ada", Who = "A pilot.", Provider = StorySpeaker.Kokoro, Voice = "af_heart", Pinned = true },
            ],
        };

        _startPairing();
        Pairer.CurrentStory = () => (secret, "A Story");

        _speech.Apply();

        await WaitForAsync(() => StoryCastPairing.On(_settings.Current, TtsProviderCatalog.KokoroId)?.ContainsKey("a-story.hand") == true);
        Assert.False(StoryCastPairing.On(_settings.Current, TtsProviderCatalog.KokoroId)!.ContainsKey("a-story.kept"));
    }

    [Fact]
    public void AChatterboxVoicesGenderIsReadFromTheShippedCatalogue()
    {
        SeededKokoro.CopyShippedChatterboxClips(_files);

        Assert.Equal(
            D47.Core.Persona.VoiceGender.Female,
            StoryCastPairing.GenderOf(StorySpeaker.Chatterbox, "isolde", _speech.ChatterboxCatalogue()));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "No voice was written within five seconds.");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
