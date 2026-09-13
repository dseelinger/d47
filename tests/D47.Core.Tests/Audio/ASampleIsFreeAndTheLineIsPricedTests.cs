using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>On a provider with free samples, playing a voice is not priced and hearing its line is (#106).</summary>
public class ASampleIsFreeAndTheLineIsPricedTests
{
    private static SettingAudition Audition() =>
        SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Beds = () => [],
            Audition = (_, _, _) => Task.CompletedTask,
            Preview = (_, _, _) => Task.CompletedTask,
            HasPreview = (_, id) => id == "sampled",
        }).Settings.Single(row => row.Key == SpeechCapability.VoiceKey).Audition!;

    private static D47Settings On(string provider) =>
        D47Settings.Defaults with { Speech = D47Settings.Defaults.Speech with { Provider = provider } };

    [Fact]
    public void OnElevenLabsPlayingIsTheFreeSampleAndTheLineIsWhatCosts()
    {
        var audition = Audition();
        var settings = On(SpeechCapability.ElevenLabsId);

        Assert.Contains("free sample", audition.Cost(settings), StringComparison.Ordinal);
        Assert.StartsWith("Hear it say its own line.", audition.LineCost!(settings), StringComparison.Ordinal);
        Assert.DoesNotContain("costs nothing", audition.LineCost!(settings), StringComparison.Ordinal);

        Assert.NotNull(audition.Preview);
        Assert.True(audition.HasPreview!("sampled"));
        Assert.False(audition.HasPreview!("unsampled"));
    }

    [Fact]
    public void AFreeProviderIsWordedAsItWas() =>
        Assert.Equal(
            "Play a voice to hear it. This provider costs nothing.",
            Audition().Cost(On(SpeechCapability.EdgeId)));
}
