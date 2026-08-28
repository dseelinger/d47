using D47.Core.Audio;
using D47.Core.Speech;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// How a local voice is named in the picker (#145).
/// <para>
/// <b>Every Kokoro row said the same two facts twice.</b> <c>KokoroAssets</c> answered the whole
/// label — <em>Jessica — female, American</em> — and it is handed to <see cref="VoiceInfo.Name"/>,
/// which composes a label by adding the gender and the locale it was given separately. So the
/// picker read <em>Jessica — female, American — Female, en-US</em>, on a row long enough to help
/// widen a window that was already too wide.
/// </para>
/// </summary>
public class KokoroVoiceLabelTests
{
    /// <summary>The name alone, which is the one thing <see cref="VoiceInfo"/> cannot derive.</summary>
    [Theory]
    [InlineData("af_jessica", "Jessica")]
    [InlineData("bm_george", "George")]
    [InlineData("af_heart", "Heart")]
    public void TheNameIsTheNameAndNothingElse(string id, string expected) =>
        Assert.Equal(expected, KokoroAssets.Name(id));

    /// <summary>
    /// And composed, it says each fact once — the same shape every other provider's rows have.
    /// </summary>
    [Fact]
    public void TheComposedLabelSaysEachThingOnce()
    {
        var voice = new VoiceInfo("af_jessica", KokoroAssets.Name("af_jessica"), "en-US", "Female");

        Assert.Equal("Jessica — Female, en-US", voice.Label);
    }

    /// <summary>An id that is not shaped like one is answered unchanged rather than sliced.</summary>
    [Fact]
    public void AnIdThatIsNotShapedLikeOneIsLeftAlone() =>
        Assert.Equal("odd", KokoroAssets.Name("odd"));
}
