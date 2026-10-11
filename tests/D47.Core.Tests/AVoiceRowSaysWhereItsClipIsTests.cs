using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests;

public class AVoiceRowSaysWhereItsClipIsTests
{
    [Theory]
    [InlineData(ChatterboxClip.Shipped, "Shipped", ChoiceTone.Yellow, null, true, false)]
    [InlineData(ChatterboxClip.Fetched, "Fetched", ChoiceTone.Yellow, null, true, false)]
    [InlineData(ChatterboxClip.Custom, "Custom", ChoiceTone.Yellow, null, true, false)]
    [InlineData(ChatterboxClip.NotFetched, "Not fetched", ChoiceTone.Grey, "247 KB", false, false)]
    [InlineData(ChatterboxClip.Fetching, "Fetching", ChoiceTone.White, "247 KB", false, false)]
    [InlineData(ChatterboxClip.Failed, "Fetch failed", ChoiceTone.Red, "247 KB", false, true)]
    public void EachStateDrawsItsWordInItsTone(
        ChatterboxClip clip, string word, ChoiceTone tone, string? size, bool onThisPc, bool retry)
    {
        var line = ChoiceClip.Of(new ChatterboxClipState(clip, 247 * 1024));

        Assert.Equal(new ChoiceStatus(word, tone), line.State);
        Assert.Equal(size, line.Size);
        Assert.Equal(onThisPc, line.OnThisPc);
        Assert.Equal(retry, line.CanRetry);
    }

    [Fact]
    public void ASizeTheCatalogueDoesNotGiveIsLeftOut() =>
        Assert.Null(ChoiceClip.Of(new ChatterboxClipState(ChatterboxClip.NotFetched, 0)).Size);

    [Theory]
    [InlineData("female", "mid", "even", "Female · mid pitch · even pace")]
    [InlineData("male", null, "brisk", "Male · brisk pace")]
    [InlineData(null, null, null, null)]
    public void TheDetailLineNamesWhatTheVoiceIsTaggedWith(string? gender, string? pitch, string? pace, string? line) =>
        Assert.Equal(line, SpeechCapability.VoiceDetail(gender, pitch, pace));
}
