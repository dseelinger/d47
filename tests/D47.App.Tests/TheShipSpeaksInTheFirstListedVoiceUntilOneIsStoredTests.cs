using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// With no voice stored for the core aboard, the ship speaks in its provider's first listed voice, and nothing is
/// saved; a stored voice replaces it (#1050).
/// </summary>
[Trait("Category", "Integration")]
public class TheShipSpeaksInTheFirstListedVoiceUntilOneIsStoredTests : IDisposable
{
    private readonly SpeechClients _speech;
    private readonly SettingsService _settings;
    private readonly PersonaHost _personas = new();
    private readonly VoicePipeline _voice;
    private readonly MemoryFileSystem _files = new();
    private readonly string _kokoroFolder;

    public TheShipSpeaksInTheFirstListedVoiceUntilOneIsStoredTests()
    {
        var (settings, _, paths, _, secrets) = TestSurface.CreateFull();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();

        _voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);
        _settings = settings;
        _kokoroFolder = Path.Combine(paths.Data, "models", "kokoro");
        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            _files,
            _personas,
            _voice,
            arbiter,
            new OwnVoice(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()),
            new CustomVoices(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()));
    }

    public void Dispose() => _speech.Dispose();

    [Fact]
    public async Task AListedVoiceGivesTheShipAndTheCarrierItsFirstVoiceAndSavesNothing()
    {
        SeededKokoro.Install(_files, _kokoroFolder);
        Select(TtsProviderCatalog.KokoroId);

        await ApplyAndAwaitListAsync();

        var first = KokoroAssets.VoiceIds[0];
        Assert.Equal(first, _speech.Cast.For(VoiceRole.ShipAi).VoiceId);
        Assert.Equal(first, _speech.Cast.For(VoiceRole.CarrierCaptain).VoiceId);
        Assert.Equal(first, _voice.Voice.VoiceId);
        Assert.DoesNotContain(_personas.Current.Id, _settings.Current.Persona.Voices.Keys);
        Assert.Null(_settings.Current.Speech.Voice);
    }

    [Fact]
    public async Task AStoredVoiceWins()
    {
        SeededKokoro.Install(_files, _kokoroFolder);
        Select(TtsProviderCatalog.KokoroId);
        await ApplyAndAwaitListAsync();

        var stored = KokoroAssets.VoiceIds[1];
        _settings.Replace(
            SpeechCapability.VoiceKey,
            current => current with
            {
                Persona = current.Persona with
                {
                    Voices = new Dictionary<string, string>(current.Persona.Voices) { [_personas.Current.Id] = stored },
                },
            });
        _speech.Apply();

        Assert.Equal(stored, _speech.Cast.For(VoiceRole.ShipAi).VoiceId);
        Assert.Equal(stored, _voice.Voice.VoiceId);
    }

    [Fact]
    public async Task AListArrivingAfterTheSettingsWereAppliedGivesTheShipItsFirstVoice()
    {
        Select(TtsProviderCatalog.KokoroId);
        await ApplyAndAwaitListAsync();
        Assert.Null(_speech.Cast.For(VoiceRole.ShipAi).VoiceId);

        Select(TtsProviderCatalog.NoneId);
        _speech.Apply();
        SeededKokoro.Install(_files, _kokoroFolder);
        Select(TtsProviderCatalog.KokoroId);
        await ApplyAndAwaitListAsync();

        var first = KokoroAssets.VoiceIds[0];
        Assert.Equal(first, _speech.Cast.For(VoiceRole.ShipAi).VoiceId);
        Assert.Equal(first, _voice.Voice.VoiceId);
        Assert.DoesNotContain(_personas.Current.Id, _settings.Current.Persona.Voices.Keys);
    }

    private async Task ApplyAndAwaitListAsync()
    {
        var listed = new SemaphoreSlim(0);
        _speech.VoicesReady += _ => listed.Release();

        _speech.Apply();

        Assert.True(await listed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    private void Select(string providerId) =>
        _settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with { Speech = current.Speech with { Provider = providerId } });
}
