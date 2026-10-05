using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>
/// A clip still arriving renders silence until its pre-roll is in, then what has arrived, silence where
/// playback catches up, and ends only once the clip is complete and read to its last byte.
/// </summary>
public class AnArrivingClipPlaysAsItArrivesTests
{
    private static readonly int PreRollFrames =
        (int)(AudioFormat.Standard.SampleRate * ArrivingClipSampleProvider.PreRoll.TotalSeconds);

    [Fact]
    public void ItIsSilentUntilThePreRollHasArrived()
    {
        var clip = new ArrivingClip("speech");
        var provider = new ArrivingClipSampleProvider(clip, NullLogger.Instance);

        clip.Append(Tone(PreRollFrames - 1));

        var buffer = Enumerable.Repeat(1f, 480).ToArray();

        Assert.Equal(480, provider.Read(buffer, 0, buffer.Length));
        Assert.All(buffer, sample => Assert.Equal(0f, sample));
        Assert.Equal(0, provider.Underruns);
    }

    [Fact]
    public void ItPlaysOnceThePreRollHasArrived()
    {
        var clip = new ArrivingClip("speech");
        var provider = new ArrivingClipSampleProvider(clip, NullLogger.Instance);

        clip.Append(Tone(PreRollFrames));

        var buffer = new float[480];

        Assert.Equal(480, provider.Read(buffer, 0, buffer.Length));
        Assert.All(buffer, sample => Assert.Equal(0.5f, sample));
    }

    [Fact]
    public void AShortClipPlaysOnceItIsCompleteWithoutWaitingForThePreRoll()
    {
        var clip = new ArrivingClip("ok");
        var provider = new ArrivingClipSampleProvider(clip, NullLogger.Instance);

        clip.Append(Tone(100));
        clip.Complete();

        var buffer = new float[480];

        Assert.Equal(100, provider.Read(buffer, 0, buffer.Length));
        Assert.All(buffer[..100], sample => Assert.Equal(0.5f, sample));
    }

    [Fact]
    public void CatchingUpWithWhatHasArrivedIsSilenceAndCountsAsAnUnderrun()
    {
        var clip = new ArrivingClip("speech");
        var provider = new ArrivingClipSampleProvider(clip, NullLogger.Instance);

        clip.Append(Tone(PreRollFrames));

        var buffer = new float[PreRollFrames + 480];

        Assert.Equal(buffer.Length, provider.Read(buffer, 0, buffer.Length));
        Assert.All(buffer[..PreRollFrames], sample => Assert.Equal(0.5f, sample));
        Assert.All(buffer[PreRollFrames..], sample => Assert.Equal(0f, sample));

        // Still starved: the same underrun, not a second one.
        Assert.Equal(480, provider.Read(buffer, 0, 480));
        Assert.Equal(1, provider.Underruns);

        clip.Append(Tone(480));
        Assert.Equal(480, provider.Read(buffer, 0, 480));
        Assert.All(buffer[..480], sample => Assert.Equal(0.5f, sample));

        Assert.Equal(480, provider.Read(buffer, 0, 480));
        Assert.Equal(2, provider.Underruns);
    }

    [Fact]
    public void ItEndsOnlyOnceTheClipIsCompleteAndItsLastByteIsRead()
    {
        var clip = new ArrivingClip("speech");
        var provider = new ArrivingClipSampleProvider(clip, NullLogger.Instance);

        clip.Append(Tone(PreRollFrames + 240));

        var buffer = new float[PreRollFrames];

        Assert.Equal(PreRollFrames, provider.Read(buffer, 0, buffer.Length));

        clip.Complete();

        Assert.Equal(240, provider.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, provider.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, provider.Underruns);
    }

    /// <summary>Mono 16-bit PCM at half scale.</summary>
    private static byte[] Tone(int frames)
    {
        var pcm = new byte[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            pcm[(i * 2) + 1] = 0x40;
        }

        return pcm;
    }
}
