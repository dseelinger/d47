using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Applying the speech settings wires the selected provider to its slot and announces its voices.</summary>
[Trait("Category", "Integration")]
public class AnAppliedProviderSpeaksForItsSlotTests : IDisposable
{
    private readonly SpeechClients _speech;
    private readonly SettingsService _settings;

    public AnAppliedProviderSpeaksForItsSlotTests()
    {
        var (settings, _, paths, _, secrets) = TestSurface.CreateFull();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var voice = new VoicePipeline(arbiter, CueLibrary.Load, NullLoggerFactory.Instance);

        _settings = settings;
        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            new PersonaHost(),
            voice,
            arbiter,
            new OwnVoice(paths.Data, new DpapiSecretProtector()),
            new CustomVoices(paths.Data, new DpapiSecretProtector()),
            () => throw new InvalidOperationException("No crew seat is looked up here."));
    }

    public void Dispose() => _speech.Dispose();

    private void SelectProvider(string providerId) =>
        _settings.Replace(
            SpeechCapability.ProviderKey,
            current => current with { Speech = current.Speech with { Provider = providerId } });

    [Fact]
    public async Task TheShipSpeaksThroughTheProviderItWasMovedTo()
    {
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _speech.VoicesReady += id => ready.TrySetResult(id);

        SelectProvider(TtsProviderCatalog.KokoroId);
        _speech.Apply();

        Assert.Equal(TtsProviderCatalog.KokoroId, _speech.Speaker(VoiceGroup.Aboard)?.Id);
        Assert.False(_speech.DirectableIn(VoiceGroup.Aboard));
        Assert.Equal(TtsProviderCatalog.KokoroId, await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheShipFallsSilentWhenItsProviderIsNone()
    {
        SelectProvider(TtsProviderCatalog.KokoroId);
        _speech.Apply();
        Assert.NotNull(_speech.Speaker(VoiceGroup.Aboard));

        SelectProvider(TtsProviderCatalog.NoneId);
        _speech.Apply();

        Assert.Null(_speech.Speaker(VoiceGroup.Aboard));
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
