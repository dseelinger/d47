using D47.Core.Audio;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>A provider with no voice chosen refuses, rather than speaking in a voice nobody chose.</summary>
public class ANoChosenVoiceIsRefusedTests
{
    [Fact]
    public async Task EdgeNeuralRefusesBeforeItContactsAnything()
    {
        using var provider = new EdgeNeuralTtsProvider(NullLogger<EdgeNeuralTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Equal("No Edge Neural voice has been chosen. Pick one in Settings.", failure.Message);
    }

    [Fact]
    public async Task OpenAiRefusesWithAKeyButNoVoice()
    {
        using var provider = new OpenAiTtsProvider(() => "sk-test", NullLogger<OpenAiTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Equal("No OpenAI voice has been chosen. Pick one in Settings.", failure.Message);
    }

    [Fact]
    public async Task KokoroRefusesWithNoVoice()
    {
        using var provider = new KokoroTtsProvider(Path.GetTempPath(), NullLogger<KokoroTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("test", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Equal("No Kokoro voice has been chosen. Pick one in Settings.", failure.Message);
    }

    [Fact]
    public async Task KokoroRefusesAVoiceItDoesNotHave()
    {
        using var provider = new KokoroTtsProvider(Path.GetTempPath(), NullLogger<KokoroTtsProvider>.Instance);

        var failure = await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "test", new VoiceSelection("xx_nobody"), TestContext.Current.CancellationToken));

        Assert.Contains("xx_nobody", failure.Message, StringComparison.Ordinal);
    }
}
