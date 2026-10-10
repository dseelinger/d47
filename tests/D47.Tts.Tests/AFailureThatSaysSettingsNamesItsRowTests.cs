using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// Every refusal that tells the Commander to change something in Settings names the row it means, and the row
/// is one the settings page places (#952).
/// </summary>
public class AFailureThatSaysSettingsNamesItsRowTests
{
    public enum Entry
    {
        Synthesize,
        Stream,
    }

    public static TheoryData<string, Entry> Refusals()
    {
        var data = new TheoryData<string, Entry>();

        foreach (var refusal in Cases.Keys)
        {
            data.Add(refusal, Entry.Synthesize);
            data.Add(refusal, Entry.Stream);
        }

        return data;
    }

    private static readonly Dictionary<string, (Func<ITtsProvider> Provider, VoiceSelection Voice, string Row)> Cases = new()
    {
        ["edge no voice"] = (
            () => new EdgeNeuralTtsProvider(NullLogger<EdgeNeuralTtsProvider>.Instance),
            VoiceSelection.Default,
            SpeechCapability.VoiceKey),
        ["openai no key"] = (
            () => new OpenAiTtsProvider(() => null, NullLogger<OpenAiTtsProvider>.Instance),
            new VoiceSelection("alloy"),
            "speech.openai.apiKey"),
        ["openai no voice"] = (
            () => new OpenAiTtsProvider(() => "sk-test", NullLogger<OpenAiTtsProvider>.Instance),
            VoiceSelection.Default,
            SpeechCapability.VoiceKey),
        ["cartesia no key"] = (
            () => new CartesiaTtsProvider(() => null, NullLogger<CartesiaTtsProvider>.Instance),
            new VoiceSelection("a-voice"),
            "speech.cartesia.apiKey"),
        ["cartesia no voice"] = (
            () => new CartesiaTtsProvider(() => "sk-test", NullLogger<CartesiaTtsProvider>.Instance),
            VoiceSelection.Default,
            SpeechCapability.VoiceKey),
        ["elevenlabs no key"] = (
            () => new ElevenLabsTtsProvider(() => null, NullLogger<ElevenLabsTtsProvider>.Instance),
            new VoiceSelection("a-voice"),
            "speech.elevenlabs.apiKey"),
        ["elevenlabs no voice"] = (
            () => new ElevenLabsTtsProvider(() => "sk-test", NullLogger<ElevenLabsTtsProvider>.Instance),
            VoiceSelection.Default,
            SpeechCapability.VoiceKey),
        ["kokoro no voice"] = (
            () => new KokoroTtsProvider(new MemoryFileSystem(), Path.GetTempPath(), NullLogger<KokoroTtsProvider>.Instance),
            VoiceSelection.Default,
            SpeechCapability.LocalVoiceKey),
        ["chatterbox not downloaded"] = (
            () => Chatterbox(installed: false),
            new VoiceSelection("marlow"),
            SpeechCapability.ChatterboxVoiceKey),
        ["chatterbox no voice"] = (
            () => Chatterbox(installed: true),
            VoiceSelection.Default,
            SpeechCapability.ChatterboxVoiceKey),
        ["chatterbox no recording"] = (
            () => Chatterbox(installed: true),
            new VoiceSelection(OwnVoice.VoiceId),
            SpeechCapability.OwnVoiceKey),
    };

    private static ChatterboxTtsProvider Chatterbox(bool installed)
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-chatterbox");

        return new ChatterboxTtsProvider(
            new MemoryFileSystem(),
            folder,
            folder,
            folder,
            NullLogger<ChatterboxTtsProvider>.Instance,
            () => throw new InvalidOperationException("Nothing is spoken here."),
            () => installed,
            ChatterboxTestFolder.NoDownload);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task TheRefusalNamesItsRow(string refusal, Entry entry)
    {
        var (build, voice, row) = Cases[refusal];
        var provider = build();

        try
        {
            var failure = await Assert.ThrowsAsync<TtsException>(() => entry == Entry.Synthesize
                ? provider.SynthesizeAsync("test", voice, TestContext.Current.CancellationToken)
                : provider.StreamAsync("test", voice, TestContext.Current.CancellationToken));

            Assert.Contains("in Settings", failure.Message, StringComparison.Ordinal);
            Assert.Equal(row, failure.SettingKey);
            Assert.True(Placed(row), $"{row} is not placed on the settings page");
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    [Theory]
    [InlineData("openai no voice")]
    [InlineData("chatterbox no voice")]
    [InlineData("kokoro no voice")]
    public async Task ANoVoiceRefusalSaysTheVoiceIsWhatIsMissing(string refusal)
    {
        var (build, voice, _) = Cases[refusal];
        var provider = build();

        try
        {
            var failure = await Assert.ThrowsAsync<TtsException>(
                () => provider.SynthesizeAsync("test", voice, TestContext.Current.CancellationToken));

            Assert.Equal(TtsFault.NoVoice, failure.Fault);
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    private static bool Placed(string key) =>
        SettingsLayout.Areas
            .SelectMany(area => area.Places)
            .SelectMany(place => place.Groups)
            .SelectMany(group => group.Entries)
            .Any(entry => entry.Key == key || entry.Family?.Invoke(key) == true);
}
