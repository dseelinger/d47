using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
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

    public TheShipSpeaksInTheFirstListedVoiceUntilOneIsStoredTests()
    {
        var (settings, _, paths, _, secrets) = TestSurface.CreateFull();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();

        _voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);
        _settings = settings;
        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            new DiskFileSystem(),
            _personas,
            _voice,
            arbiter,
            new OwnVoice(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()),
            new CustomVoices(paths.Data, new DiskFileSystem(), new DpapiSecretProtector()));
    }

    public void Dispose() => _speech.Dispose();

    [Fact]
    public void AHeldListGivesTheShipAndTheCarrierItsFirstVoiceAndSavesNothing()
    {
        Select(TtsProviderCatalog.KokoroId);
        _speech.HoldVoicesForTest(TtsProviderCatalog.KokoroId, Offering());

        _speech.Apply();

        Assert.Equal("voice1", _speech.Cast.For(VoiceRole.ShipAi).VoiceId);
        Assert.Equal("voice1", _speech.Cast.For(VoiceRole.CarrierCaptain).VoiceId);
        Assert.Equal("voice1", _voice.Voice.VoiceId);
        Assert.DoesNotContain(_personas.Current.Id, _settings.Current.Persona.Voices.Keys);
        Assert.Null(_settings.Current.Speech.Voice);
    }

    [Fact]
    public void AStoredVoiceWins()
    {
        Select(TtsProviderCatalog.KokoroId);
        _speech.HoldVoicesForTest(TtsProviderCatalog.KokoroId, Offering());
        _speech.Apply();

        _settings.Replace(
            SpeechCapability.VoiceKey,
            current => current with
            {
                Persona = current.Persona with
                {
                    Voices = new Dictionary<string, string>(current.Persona.Voices) { [_personas.Current.Id] = "voice2" },
                },
            });
        _speech.Apply();

        Assert.Equal("voice2", _speech.Cast.For(VoiceRole.ShipAi).VoiceId);
        Assert.Equal("voice2", _voice.Voice.VoiceId);
    }

    [Fact]
    public async Task AListArrivingAfterTheSettingsWereAppliedGivesTheShipItsFirstVoice()
    {
        var listed = new SemaphoreSlim(0);
        _speech.VoicesReady += _ => listed.Release();

        Select(TtsProviderCatalog.ChatterboxId);
        _speech.Apply();
        Assert.True(await listed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Null(_speech.Cast.For(VoiceRole.ShipAi).VoiceId);

        _speech.HoldVoicesForTest(TtsProviderCatalog.ChatterboxId, Offering());
        _speech.RelistChatterboxVoices();
        Assert.True(await listed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Equal("voice1", _speech.Cast.For(VoiceRole.ShipAi).VoiceId);
        Assert.Equal("voice1", _voice.Voice.VoiceId);
        Assert.DoesNotContain(_personas.Current.Id, _settings.Current.Persona.Voices.Keys);
    }

    private void Select(string providerId) =>
        _settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with { Speech = current.Speech with { Provider = providerId } });

    private static VoiceCatalogue Offering() =>
        VoiceCatalogue.Of([.. Enumerable.Range(1, 3).Select(number => new VoiceInfo($"voice{number}", $"Voice {number}", "en-US"))]);

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
