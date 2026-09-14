using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The bundled Kokoro clip Test falls back to when nothing free is available (#226).</summary>
public class TheStandInClipShipsWithTheBuildTests
{
    [Fact]
    public void TheClipLoadsAndIsPlayableFormat()
    {
        var clip = StandInVoice.Clip;

        Assert.Equal(AudioFormat.Standard, clip.Format);
        Assert.True(clip.Pcm.Length > 0);
    }
}
