using System.Buffers.Binary;
using D47.Core.Audio;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Xunit;

namespace D47.Audio.Tests;

/// <summary>An MP3 or FLAC opened through Media Foundation reports its length and reads from where it is sought to.</summary>
[Trait("Category", "Integration")]
public class AnEncodedTrackSeeksToItsTargetTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-seek-tests",
        Guid.NewGuid().ToString("n"));

    public AnEncodedTrackSeeksToItsTargetTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".flac")]
    public void ASeekReadsFromTheTarget(string extension)
    {
        var path = Encode(extension);
        using var stream = new MediaFoundationDecoder().Open(path);

        // An MP3 carries encoder padding at each end.
        Assert.NotNull(stream.Length);
        Assert.InRange(stream.Length.Value.TotalMilliseconds, 1990, 2100);

        stream.Seek(TimeSpan.FromSeconds(1.5));
        Assert.True(Peak(stream) > 5000, "the read after a seek into the tone was not the tone");

        stream.Seek(TimeSpan.FromSeconds(0.25));
        Assert.True(Peak(stream) < 500, "the read after a seek back into the silence was not silent");
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".flac")]
    public void ASeekReadsOnToTheEndAndNoFurther(string extension)
    {
        var path = Encode(extension);
        using var stream = new MediaFoundationDecoder().Open(path);

        stream.Seek(TimeSpan.FromSeconds(1.5));
        var seconds = (double)ReadAll(stream) / (AudioFormat.Music.SampleRate * AudioFormat.Music.BytesPerFrame);

        Assert.InRange(seconds, 0.45, 0.6);
    }

    /// <summary>A second of silence, then a second of 440 Hz, at 44.1 kHz stereo.</summary>
    private string Encode(string extension)
    {
        const int rate = 44_100;
        var samples = new short[rate * 2];

        for (var frame = rate; frame < samples.Length; frame++)
        {
            samples[frame] = (short)(Math.Sin(2 * Math.PI * 440 * frame / rate) * 16_000);
        }

        var path = Path.Combine(_root, "track" + extension);

        if (extension == ".flac")
        {
            File.WriteAllBytes(path, FlacWriter.Verbatim(samples, rate));
            return path;
        }

        var wav = Path.Combine(_root, "track.wav");

        using (var writer = new WaveFileWriter(wav, new WaveFormat(rate, 16, 2)))
        {
            foreach (var sample in samples)
            {
                writer.WriteSample(sample / 32768f);
                writer.WriteSample(sample / 32768f);
            }
        }

        MediaFoundationApi.Startup();

        using var reader = new WaveFileReader(wav);
        MediaFoundationEncoder.EncodeToMp3(reader, path);
        return path;
    }

    /// <summary>The loudest sample in the next tenth of a second.</summary>
    private static int Peak(IPcmStream stream)
    {
        var buffer = new byte[AudioFormat.Music.SampleRate / 10 * AudioFormat.Music.BytesPerFrame];
        var total = 0;
        int read;

        while (total < buffer.Length && (read = stream.Read(buffer.AsSpan(total))) > 0)
        {
            total += read;
        }

        var peak = 0;

        for (var offset = 0; offset + 1 < total; offset += 2)
        {
            peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(buffer, offset)));
        }

        return peak;
    }

    private static int ReadAll(IPcmStream stream)
    {
        var buffer = new byte[16_384];
        var total = 0;
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            total += read;
        }

        return total;
    }

    /// <summary>FLAC with every subframe stored verbatim, which needs no encoder.</summary>
    private static class FlacWriter
    {
        private const int BlockSize = 4096;

        /// <summary>16-bit stereo at 44.1 kHz with the same sample on both channels.</summary>
        public static byte[] Verbatim(short[] samples, int rate)
        {
            var frames = new List<byte[]>();

            for (int start = 0, number = 0; start < samples.Length; start += BlockSize, number++)
            {
                var count = Math.Min(BlockSize, samples.Length - start);
                var frame = new List<byte>
                {
                    0xFF, 0xF8, // Sync, fixed block size.
                    0x79, // Block size in 16 bits after the header; 44.1 kHz.
                    0x18, // Left and right independent; 16 bits a sample.
                    (byte)number,
                    (byte)((count - 1) >> 8),
                    (byte)(count - 1),
                };
                frame.Add(Crc8(frame));

                for (var channel = 0; channel < 2; channel++)
                {
                    frame.Add(0x02); // A verbatim subframe.

                    for (var i = start; i < start + count; i++)
                    {
                        frame.Add((byte)(samples[i] >> 8));
                        frame.Add((byte)samples[i]);
                    }
                }

                var crc = Crc16(frame);
                frame.Add((byte)(crc >> 8));
                frame.Add((byte)crc);
                frames.Add([.. frame]);
            }

            using var file = new MemoryStream();

            file.Write("fLaC"u8);
            file.Write([0x00, 0, 0, 34]); // STREAMINFO, 34 bytes.

            var info = new byte[34];
            BinaryPrimitives.WriteUInt16BigEndian(info, 16);
            BinaryPrimitives.WriteUInt16BigEndian(info.AsSpan(2), BlockSize);
            var sizes = frames.Select(frame => frame.Length).ToList();
            Write24(info.AsSpan(4), sizes.Min());
            Write24(info.AsSpan(7), sizes.Max());
            BinaryPrimitives.WriteUInt64BigEndian(
                info.AsSpan(10),
                ((ulong)(uint)rate << 44) | (1UL << 41) | (15UL << 36) | (uint)samples.Length);
            file.Write(info);

            // The last metadata block: a SEEKTABLE with a point at every frame, as an encoder writes one.
            var table = new byte[frames.Count * 18];
            long offset = 0;

            for (var i = 0; i < frames.Count; i++)
            {
                var point = table.AsSpan(i * 18);
                BinaryPrimitives.WriteUInt64BigEndian(point, (ulong)(i * BlockSize));
                BinaryPrimitives.WriteUInt64BigEndian(point[8..], (ulong)offset);
                BinaryPrimitives.WriteUInt16BigEndian(point[16..], (ushort)Math.Min(BlockSize, samples.Length - (i * BlockSize)));
                offset += frames[i].Length;
            }

            file.WriteByte(0x83);
            var length = new byte[3];
            Write24(length, table.Length);
            file.Write(length);
            file.Write(table);

            foreach (var frame in frames)
            {
                file.Write(frame);
            }

            return file.ToArray();
        }

        private static void Write24(Span<byte> at, int value)
        {
            at[0] = (byte)(value >> 16);
            at[1] = (byte)(value >> 8);
            at[2] = (byte)value;
        }

        private static byte Crc8(List<byte> bytes)
        {
            var crc = 0;

            foreach (var b in bytes)
            {
                crc ^= b;

                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x80) != 0 ? ((crc << 1) ^ 0x07) & 0xFF : (crc << 1) & 0xFF;
                }
            }

            return (byte)crc;
        }

        private static ushort Crc16(List<byte> bytes)
        {
            var crc = 0;

            foreach (var b in bytes)
            {
                crc ^= b << 8;

                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x8005) & 0xFFFF : (crc << 1) & 0xFFFF;
                }
            }

            return (ushort)crc;
        }
    }
}
