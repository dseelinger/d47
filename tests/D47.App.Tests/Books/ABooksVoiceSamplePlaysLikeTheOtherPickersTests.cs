using D47.App.Voice;
using D47.Audio;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>Pressing Play on a voice in Media > Books > Voice speaks the Narrator line the way the other voice pickers do (#2721).</summary>
public sealed class ABooksVoiceSamplePlaysLikeTheOtherPickersTests : IDisposable
{
    private static readonly string Line = AuditionLine.For(VoiceRole.Narrator);

    private readonly BookRig _rig = BookRig.InMemory();
    private readonly SpeechClients _speech;
    private readonly SettingsService _settings;
    private readonly PersonaHost _personas = new();
    private readonly VoiceAuditions _auditions;

    public ABooksVoiceSamplePlaysLikeTheOtherPickersTests()
    {
        var (settings, _, paths, _, secrets) = TestSurface.CreateFull();
        var files = new MemoryFileSystem();
        var voice = new VoicePipeline(_rig.Audio, CueLibrary.Load, NullLoggerFactory.Instance);

        _settings = settings;
        _speech = new SpeechClients(
            settings,
            secrets,
            NullLoggerFactory.Instance,
            paths,
            files,
            _personas,
            voice,
            _rig.Audio,
            new OwnVoice(paths.Data, files, new NoopProtector()),
            new CustomVoices(paths.Data, files, new NoopProtector()));

        _auditions = Auditions(id => _rig.Voices.GetValueOrDefault(id));
    }

    public void Dispose() => _speech.Dispose();

    private VoiceAuditions Auditions(Func<string, ITtsProvider?> localVoice) =>
        new(_speech, _rig.Audio, _settings, _personas, localVoice);

    [Theory]
    [InlineData("kokoro")]
    [InlineData("edge")]
    [InlineData("chatterbox")]
    public async Task AnAuditionSpeaksTheNarratorLineInTheChosenVoiceAtNoSetRate(string provider)
    {
        var voice = _rig.Voices[provider];

        await _auditions.AuditionBookVoiceAsync(provider, "chosen-voice", CancellationToken.None);
        _rig.Settle();

        Assert.Equal([Line], voice.Said);
        Assert.Equal([new VoiceSelection("chosen-voice")], voice.Selections);
        Assert.Equal(1.0, voice.Selections[0].Rate);
        Assert.Equal([Line], _rig.Sink.Names);
    }

    [Fact]
    public async Task AChatterboxVoiceThatCannotBeFetchedSaysSoAndIsNotSynthesised()
    {
        using var chatterbox = new ChatterboxTtsProvider(
            new MemoryFileSystem(),
            "C:/d47/chatterbox/model",
            "C:/d47/chatterbox/voices",
            "C:/d47/chatterbox/fetched",
            NullLogger<ChatterboxTtsProvider>.Instance);

        var auditions = Auditions(id => id == TtsProviderCatalog.ChatterboxId ? chatterbox : null);

        // Synthesis here would fail with "not downloaded yet", so this message shows the fetch came first.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => auditions.AuditionBookVoiceAsync(TtsProviderCatalog.ChatterboxId, "no-such-voice", CancellationToken.None));

        Assert.Equal(
            $"That Chatterbox voice could not be fetched from {ChatterboxCatalog.Host}. Press Play to try again.",
            failure.Message);
        Assert.Empty(_rig.Sink.Started);

        var again = await Assert.ThrowsAsync<InvalidOperationException>(
            () => auditions.AuditionBookVoiceAsync(TtsProviderCatalog.ChatterboxId, "no-such-voice", CancellationToken.None));

        Assert.Equal(failure.Message, again.Message);
    }

    [Fact]
    public async Task ASecondAuditionStopsTheFirst()
    {
        await _auditions.AuditionBookVoiceAsync("kokoro", "first-voice", CancellationToken.None);
        var first = Assert.Single(_rig.Sink.Started);

        await _auditions.AuditionBookVoiceAsync("kokoro", "second-voice", CancellationToken.None);

        Assert.Equal(2, _rig.Sink.Started.Count);
        Assert.Equal([first.Id], _rig.Sink.Stopped);
    }

    [Fact]
    public async Task AnAuditionWhileABookIsReadPausesItAndTheBookGoesOnAfterwards()
    {
        _rig.Play();

        Assert.Equal([BookRig.First], _rig.Sink.Names);
        Assert.False(_rig.Player.Now!.IsPaused);

        await _auditions.AuditionBookVoiceAsync("kokoro", "chosen-voice", CancellationToken.None);
        _rig.Settle();

        Assert.True(_rig.Player.Now!.IsPaused);
        Assert.Equal([BookRig.First, Line], _rig.Sink.Names);

        _rig.Finish(Line);

        Assert.False(_rig.Player.Now!.IsPaused);
        Assert.Equal(BookRig.First, _rig.Player.Now.Text);

        _rig.Sink.FinishUntil(BookRig.First);

        Assert.Equal([BookRig.First, Line, BookRig.First], _rig.Sink.Names);
    }

    [Fact]
    public async Task ASecondAuditionOfTheSameVoiceUsesTheCachedSample()
    {
        var voice = _rig.Voices["kokoro"];

        await _auditions.AuditionBookVoiceAsync("kokoro", "chosen-voice", CancellationToken.None);
        await _auditions.AuditionBookVoiceAsync("kokoro", "chosen-voice", CancellationToken.None);

        Assert.Single(voice.Said);
        Assert.Equal(2, _rig.Sink.Started.Count);
    }
}
