namespace D47.Core.Audio;

public sealed class WavFormatException(string message) : Exception(message);

/// <summary>Just enough RIFF to read the shipped cues: 16-bit PCM, any rate, any channel count.</summary>
public static class WavReader
{
    public static AudioClip Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, Path.GetFileNameWithoutExtension(path));
    }

    public static AudioClip Read(Stream stream, string name)
    {
        var (format, length) = ReadHeader(stream, name);
        var pcm = new byte[length];
        stream.ReadExactly(pcm);

        return new AudioClip(name, pcm, format);
    }

    /// <summary>
    /// Parses the header and returns the sample data as a stream read on demand. Takes ownership of
    /// <paramref name="stream"/>, which must be seekable.
    /// </summary>
    public static WavPcmStream Open(Stream stream, string name)
    {
        try
        {
            var (format, length) = ReadHeader(stream, name);
            return new WavPcmStream(stream, format, length);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>The format and data length, leaving the stream at the first sample.</summary>
    private static (AudioFormat Format, int Length) ReadHeader(Stream stream, string name)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new WavFormatException($"{name} is not a RIFF file.");
        }

        reader.ReadUInt32(); // Total size, which we do not need and will not trust.

        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new WavFormatException($"{name} is RIFF but not WAVE.");
        }

        AudioFormat? format = null;

        // Chunk order is not guaranteed by the spec, and real encoders put LIST and fact chunks between fmt
        // and data.
        while (stream.Position < stream.Length - 8)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();

            switch (chunkId)
            {
                case "fmt ":
                {
                    var encoding = reader.ReadUInt16();
                    var channels = reader.ReadUInt16();
                    var sampleRate = (int)reader.ReadUInt32();
                    reader.ReadUInt32(); // Byte rate, derivable.
                    reader.ReadUInt16(); // Block align, derivable.
                    var bitsPerSample = reader.ReadUInt16();

                    if (encoding != 1)
                    {
                        throw new WavFormatException(
                            $"{name} is encoding {encoding}; only uncompressed PCM is supported.");
                    }

                    if (bitsPerSample != 16)
                    {
                        throw new WavFormatException(
                            $"{name} is {bitsPerSample}-bit; only 16-bit is supported.");
                    }

                    format = new AudioFormat(sampleRate, channels);

                    // fmt is 16 bytes for PCM but may carry an extension.
                    Skip(stream, (int)chunkSize - 16);
                    break;
                }

                case "data":
                {
                    if (format is null)
                    {
                        throw new WavFormatException($"{name} has a data chunk before its fmt chunk.");
                    }

                    // A data size larger than the file is a writer that never went back to fill it in.
                    return (format, (int)Math.Min(chunkSize, stream.Length - stream.Position));
                }

                default:
                    Skip(stream, (int)chunkSize);
                    break;
            }

            // Chunks are word-aligned; an odd size carries a pad byte that is not counted.
            if (chunkSize % 2 == 1)
            {
                Skip(stream, 1);
            }
        }

        throw new WavFormatException($"{name} has no data chunk.");
    }

    private static void Skip(Stream stream, int count)
    {
        if (count > 0)
        {
            stream.Seek(count, SeekOrigin.Current);
        }
    }
}

/// <summary>The sample data of one WAV file, read as it is asked for.</summary>
public sealed class WavPcmStream : IPcmStream
{
    private readonly Stream _stream;
    private int _remaining;

    internal WavPcmStream(Stream stream, AudioFormat format, int length)
    {
        _stream = stream;
        _remaining = length;
        Format = format;
    }

    public AudioFormat Format { get; }

    /// <summary>Reads whole frames only.</summary>
    public int Read(Span<byte> buffer)
    {
        var wanted = Math.Min(buffer.Length, _remaining);
        wanted -= wanted % Format.BytesPerFrame;

        if (wanted <= 0)
        {
            return 0;
        }

        var read = _stream.ReadAtLeast(buffer[..wanted], wanted, throwOnEndOfStream: false);
        read -= read % Format.BytesPerFrame;
        _remaining = read < wanted ? 0 : _remaining - read;

        return read;
    }

    public void Dispose() => _stream.Dispose();
}
