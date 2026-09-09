using D47.Core.Audio;
using D47.Core.Speech;
using Xunit;

namespace D47.Tts.Tests;

public class KokoroVoiceLabelTests
{
    [Theory]
    [InlineData("af_jessica", "Jessica")]
    [InlineData("bm_george", "George")]
    [InlineData("af_heart", "Heart")]
    public void TheNameIsTheNameAndNothingElse(string id, string expected) =>
        Assert.Equal(expected, KokoroAssets.Name(id));

    [Fact]
    public void TheComposedLabelSaysEachThingOnce()
    {
        var voice = new VoiceInfo("af_jessica", KokoroAssets.Name("af_jessica"), "en-US", "Female");

        Assert.Equal("Jessica — Female, en-US", voice.Label);
    }

    [Fact]
    public void AnIdThatIsNotShapedLikeOneIsLeftAlone() =>
        Assert.Equal("odd", KokoroAssets.Name("odd"));
}
