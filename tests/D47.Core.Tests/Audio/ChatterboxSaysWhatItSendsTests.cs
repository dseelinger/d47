using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ChatterboxSaysWhatItSendsTests
{
    private static D47Settings With(string provider) =>
        D47Settings.Defaults with { Speech = D47Settings.Defaults.Speech with { Provider = provider } };

    [Fact]
    public void SpokenRepliesSayNothingLeavesTheMachine()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.TextToSpeech, With(TtsProviderCatalog.ChatterboxId), llmKeyPresent: false);

        Assert.Equal("nothing sent", entry.Destination);
        Assert.Contains("Nothing is sent anywhere", entry.What, StringComparison.Ordinal);
        Assert.Contains("Chatterbox", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void TheModelDownloadNamesTheHostAndTheRepository()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechModels, With(TtsProviderCatalog.ChatterboxId), llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Contains("huggingface.co/ResembleAI/chatterbox-turbo-ONNX", entry.Destination, StringComparison.Ordinal);
        Assert.Contains("huggingface.co/ResembleAI/chatterbox-turbo-ONNX", entry.What, StringComparison.Ordinal);
        Assert.Contains("691 MB", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void AVoiceClipIsAskedForByIdFromGitHub()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.ChatterboxVoices, With(TtsProviderCatalog.ChatterboxId), llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal("github.com", entry.Destination);
        Assert.Contains("chatterbox-voices-1", entry.What, StringComparison.Ordinal);
        Assert.Contains("naming the voice's id and nothing else", entry.What, StringComparison.Ordinal);
        Assert.Contains(EgressDisclosure.ChatterboxVoices, EgressDisclosure.Ids);
    }

    [Fact]
    public void WithoutChatterboxNoVoiceClipIsAskedFor()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.ChatterboxVoices, With(TtsProviderCatalog.KokoroId), llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Equal("nothing sent", entry.Destination);
    }

    [Fact]
    public void WithoutChatterboxTheModelDownloadDoesNotMentionIt()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.SpeechModels, With(TtsProviderCatalog.KokoroId), llmKeyPresent: false);

        Assert.DoesNotContain("Chatterbox", entry.What, StringComparison.Ordinal);
        Assert.DoesNotContain(ChatterboxAssets.Repository, entry.Destination, StringComparison.Ordinal);
    }

    [Fact]
    public void ItIsOfferedWhereverKokoroIsWithNoKey()
    {
        foreach (var slot in VoiceGroups.All)
        {
            var offered = TtsProviderCatalog.For(slot);

            Assert.Equal(
                offered.Any(p => p.Id == TtsProviderCatalog.KokoroId),
                offered.Any(p => p.Id == TtsProviderCatalog.ChatterboxId));
        }

        Assert.False(TtsProviderCatalog.Chatterbox.NeedsKey);
        Assert.Equal(TtsProviderCatalog.KokoroId, TtsProviderCatalog.All[1].Id);
    }

    [Fact]
    public void TheDownloadRowAppearsOnlyWhileASlotUsesChatterbox()
    {
        var row = SpeechCapability.Create(new SpeechCapability.SpeechSurface { Silence = () => { } }).Settings
            .Single(r => r.Key == SpeechCapability.ChatterboxVoiceKey);

        Assert.True(row.AppliesWhen!(With(TtsProviderCatalog.ChatterboxId)));
        Assert.False(row.AppliesWhen!(With(TtsProviderCatalog.KokoroId)));
    }

    [Fact]
    public void ThePinnedSetIsTheQ4GraphsAndTheTokenizer()
    {
        Assert.Equal(9, ChatterboxAssets.All.Count);
        Assert.All(ChatterboxAssets.Graphs, asset => Assert.Contains("_q4.onnx", asset.Path, StringComparison.Ordinal));
        Assert.All(ChatterboxAssets.All, asset => Assert.Equal(64, asset.Sha256.Length));
        Assert.Equal(691, Math.Round(ChatterboxAssets.TotalMegabytes));
    }
}
