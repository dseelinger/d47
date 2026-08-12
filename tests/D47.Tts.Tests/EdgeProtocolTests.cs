using System.Text;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>
/// The parts of the Edge path that can be wrong with no network involved. Every one of these
/// is a failure that would otherwise present as "the voice sounds odd" or "one reply came out
/// mangled" — symptoms nobody traces back to a format string.
/// </summary>
public class EdgeProtocolTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AShipNameFullOfMarkupCannotEscapeIntoTheSsml()
    {
        // Journal content is untrusted (architecture.md §7), and a Commander can name a ship
        // whatever they like.
        var hostile = "</prosody></voice><voice name='en-US-GuyNeural'>owned";

        var ssml = EdgeProtocol.SsmlRequest(hostile, "en-GB-SoniaNeural", 1.0, "req", Noon);

        // Exactly one voice element, and it is the one we chose.
        Assert.Equal(1, CountOccurrences(ssml, "<voice name="));
        Assert.Contains("en-GB-SoniaNeural", ssml, StringComparison.Ordinal);
        Assert.DoesNotContain("en-US-GuyNeural'>", ssml, StringComparison.Ordinal);
        Assert.Contains("&lt;/prosody&gt;", ssml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Hall & Oates", "&amp;")]
    [InlineData("the \"Anaconda\"", "&quot;")]
    [InlineData("Jameson's ship", "&apos;")]
    public void OrdinaryPunctuationIsEscapedRatherThanBreakingTheRequest(string text, string expected)
    {
        var ssml = EdgeProtocol.SsmlRequest(text, "en-GB-SoniaNeural", 1.0, "req", Noon);

        Assert.Contains(expected, ssml, StringComparison.Ordinal);
    }

    [Fact]
    public void AmpersandsAreEscapedOnceRatherThanTwice()
    {
        Assert.Equal("&amp;lt;", EdgeProtocol.Escape("&lt;"));
    }

    [Theory]
    [InlineData(1.0, "+0%")]
    [InlineData(1.25, "+25%")]
    [InlineData(0.8, "-20%")]
    [InlineData(2.0, "+100%")]
    public void RateLeavesTheSeamInThePercentageFormTheServiceWants(double rate, string expected)
    {
        Assert.Equal(expected, EdgeProtocol.RateAttribute(rate));
    }

    [Fact]
    public void TheSecurityTokenIsStableWithinItsFiveMinuteWindowAndChangesAcrossIt()
    {
        var early = EdgeProtocol.SecurityQuery(Noon);
        var sameWindow = EdgeProtocol.SecurityQuery(Noon.AddMinutes(2));
        var nextWindow = EdgeProtocol.SecurityQuery(Noon.AddMinutes(6));

        Assert.Equal(early, sameWindow);
        Assert.NotEqual(early, nextWindow);
        Assert.Contains("Sec-MS-GEC-Version=1-", early, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConfigurationAsksForRawPcmSoNoDecoderIsNeeded()
    {
        var configuration = EdgeProtocol.Configuration(Noon);

        Assert.Contains("raw-24khz-16bit-mono-pcm", configuration, StringComparison.Ordinal);
        Assert.DoesNotContain("mp3", configuration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABinaryFrameYieldsTheAudioAfterItsHeader()
    {
        var header = Encoding.ASCII.GetBytes("Path:audio\r\n\r\n");
        var frame = new byte[2 + header.Length + 4];
        frame[0] = (byte)(header.Length >> 8);
        frame[1] = (byte)header.Length;
        header.CopyTo(frame, 2);
        frame[^4] = 1;
        frame[^3] = 2;
        frame[^2] = 3;
        frame[^1] = 4;

        Assert.Equal([1, 2, 3, 4], EdgeProtocol.AudioOf(frame).ToArray());
    }

    [Fact]
    public void ATruncatedFrameYieldsNothingRatherThanReadingPastItsEnd()
    {
        Assert.Empty(EdgeProtocol.AudioOf([]).ToArray());
        Assert.Empty(EdgeProtocol.AudioOf([0]).ToArray());
        Assert.Empty(EdgeProtocol.AudioOf([0, 40]).ToArray());
    }

    /// <summary>
    /// Getting this wrong halves or doubles the pitch of every word the app says, which is
    /// unmistakable when heard and invisible in code review.
    /// </summary>
    [Fact]
    public void UpsamplingDoublesTheLengthAndKeepsTheOriginalSamplesInPlace()
    {
        short[] source = [0, 1000, -1000, 32767];
        var pcm = new byte[source.Length * 2];

        for (var i = 0; i < source.Length; i++)
        {
            pcm[i * 2] = (byte)source[i];
            pcm[(i * 2) + 1] = (byte)(source[i] >> 8);
        }

        var upsampled = EdgeProtocol.Upsample(pcm);

        Assert.Equal(pcm.Length * 2, upsampled.Length);

        var result = new short[upsampled.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (short)(upsampled[i * 2] | (upsampled[(i * 2) + 1] << 8));
        }

        // Originals land on even indices; odd indices are the midpoints between them.
        Assert.Equal(0, result[0]);
        Assert.Equal(500, result[1]);
        Assert.Equal(1000, result[2]);
        Assert.Equal(0, result[3]);
        Assert.Equal(-1000, result[4]);

        // The final sample has nothing to interpolate towards and holds rather than ramping
        // to zero, which would be an audible click at the end of every sentence.
        Assert.Equal(32767, result[^1]);
        Assert.Equal(32767, result[^2]);
    }

    [Fact]
    public void UpsamplingAnEmptyBufferIsEmptyRatherThanAThrow()
    {
        Assert.Empty(EdgeProtocol.Upsample([]));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
