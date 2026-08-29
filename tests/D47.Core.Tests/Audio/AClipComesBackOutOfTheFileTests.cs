using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// A retained clip has to be a file anything can open
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
/// <para>
/// Asserted by reading it back through <see cref="WavReader"/>, which is the strictest reader in
/// the repository — it refuses anything that is not 16-bit uncompressed PCM with its chunks in
/// order — so a header this writer got wrong fails here rather than in a media player weeks later.
/// </para>
/// </summary>
public class AClipComesBackOutOfTheFileTests
{
    [Fact]
    public void The_bytes_survive_the_round_trip()
    {
        var pcm = new byte[] { 1, 0, 0xFF, 0x7F, 0x00, 0x80, 9, 0 };

        var clip = WavReader.Read(
            new MemoryStream(WavWriter.ToBytes(pcm, new AudioFormat(16_000, 1))),
            "written");

        Assert.Equal(16_000, clip.Format.SampleRate);
        Assert.Equal(1, clip.Format.Channels);
        Assert.Equal(pcm, clip.Pcm.ToArray());
    }

    [Fact]
    public void Stereo_states_its_own_channel_count()
    {
        var clip = WavReader.Read(
            new MemoryStream(WavWriter.ToBytes(new byte[8], new AudioFormat(48_000, 2))),
            "written");

        Assert.Equal(48_000, clip.Format.SampleRate);
        Assert.Equal(2, clip.Format.Channels);
    }

    /// <summary>
    /// Float samples land where the capture path says they do. The transcriber is handed floats,
    /// so this is the conversion every heard row goes through.
    /// </summary>
    [Fact]
    public void Float_samples_become_sixteen_bit()
    {
        var clip = WavReader.Read(
            new MemoryStream(WavWriter.ToBytes([0f, 1f, -1f], 16_000)),
            "written");

        var samples = new short[3];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(clip.Pcm.Span[(i * 2)..]);
        }

        Assert.Equal(0, samples[0]);
        Assert.Equal(32767, samples[1]);
        Assert.Equal(-32767, samples[2]);
    }

    /// <summary>
    /// A sample past full scale is clamped rather than wrapped. Wrapping would turn a clipped
    /// peak into a loud click at the opposite polarity, which is a recorder inventing a fault
    /// that was not in the audio.
    /// </summary>
    [Fact]
    public void A_sample_past_full_scale_is_clamped()
    {
        var clip = WavReader.Read(
            new MemoryStream(WavWriter.ToBytes([4f, -4f], 16_000)),
            "written");

        Assert.Equal(short.MaxValue, BitConverter.ToInt16(clip.Pcm.Span));
        Assert.Equal(short.MinValue, BitConverter.ToInt16(clip.Pcm.Span[2..]));
    }
}
