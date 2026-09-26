using D47.Core.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace D47.Audio;

/// <summary>
/// Any file Windows' Media Foundation codecs read, downmixed to mono and resampled to
/// <see cref="AudioFormat.Standard"/>.
/// </summary>
public sealed class MediaFoundationDecoder : IAudioDecoder
{
    private const int UnsupportedByteStream = unchecked((int)0xC00D36C4);
    private const int CodecNotFound = unchecked((int)0xC00D5212);
    private const int ClassNotRegistered = unchecked((int)0x80040154);

    public IReadOnlySet<string> Extensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".m4a", ".aac", ".wma", ".flac", ".wav" };

    public AudioClip Decode(string path, string name)
    {
        using var stream = Open(path);
        using var pcm = new MemoryStream();
        var chunk = new byte[65_536];
        int read;

        while ((read = stream.Read(chunk)) > 0)
        {
            pcm.Write(chunk, 0, read);
        }

        return new AudioClip(name, pcm.ToArray(), AudioFormat.Standard);
    }

    public IPcmStream Open(string path)
    {
        MediaFoundationReader? reader = null;

        try
        {
            reader = new MediaFoundationReader(path);
            return new PcmStream(reader, Standardise(reader));
        }
        catch (Exception ex) when (IsCodecFailure(ex))
        {
            reader?.Dispose();
            throw Undecodable(ex);
        }
    }

    private static IWaveProvider Standardise(WaveStream reader)
    {
        var samples = reader.ToSampleProvider();

        if (samples.WaveFormat.Channels > 1)
        {
            samples = new Downmix(samples);
        }

        if (samples.WaveFormat.SampleRate != AudioFormat.Standard.SampleRate)
        {
            samples = new WdlResamplingSampleProvider(samples, AudioFormat.Standard.SampleRate);
        }

        return new SampleToWaveProvider16(samples);
    }

    /// <summary>
    /// Anything the codec path throws, so a file that will not decode is a skip line rather than a
    /// failed start. File-system failures are left to the caller, which already reports them.
    /// </summary>
    private static bool IsCodecFailure(Exception ex) =>
        ex is not (IOException or UnauthorizedAccessException or OutOfMemoryException or AudioDecodeException);

    private static AudioDecodeException Undecodable(Exception ex) =>
        ex is DllNotFoundException || ex.HResult is UnsupportedByteStream or CodecNotFound or ClassNotRegistered
            ? new("Windows has no decoder for this file. On a Windows N edition, install the Media Feature Pack.", ex)
            : new($"Windows cannot decode it: {ex.Message}", ex);

    private sealed class PcmStream(WaveStream reader, IWaveProvider pcm) : IPcmStream
    {
        private byte[] _chunk = [];

        /// <summary>Reads whole samples only.</summary>
        public int Read(Span<byte> buffer)
        {
            var wanted = buffer.Length - (buffer.Length % 2);

            if (_chunk.Length < wanted)
            {
                _chunk = new byte[wanted];
            }

            int read;

            try
            {
                read = pcm.Read(_chunk, 0, wanted);
            }
            catch (Exception ex) when (IsCodecFailure(ex))
            {
                throw Undecodable(ex);
            }

            read -= read % 2;
            _chunk.AsSpan(0, read).CopyTo(buffer);
            return read;
        }

        public void Dispose() => reader.Dispose();
    }

    /// <summary>Averages every channel into one.</summary>
    private sealed class Downmix(ISampleProvider source) : ISampleProvider
    {
        private readonly int _channels = source.WaveFormat.Channels;
        private float[] _frames = [];

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            var wanted = count * _channels;

            if (_frames.Length < wanted)
            {
                _frames = new float[wanted];
            }

            var frames = source.Read(_frames, 0, wanted) / _channels;

            for (var frame = 0; frame < frames; frame++)
            {
                var sum = 0f;

                for (var channel = 0; channel < _channels; channel++)
                {
                    sum += _frames[(frame * _channels) + channel];
                }

                buffer[offset + frame] = sum / _channels;
            }

            return frames;
        }
    }
}
