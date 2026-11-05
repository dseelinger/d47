using System.Runtime.InteropServices;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>Compresses clips to 48 kbps AAC in an MPEG-4 container with Windows' Media Foundation encoder, in memory only.</summary>
public sealed class MediaFoundationClipCompressor : IClipCompressor
{
    private const int BitRate = 48_000;

    private readonly Guid _subtype;
    private readonly Guid _container;
    private readonly string _extension;
    private readonly ILogger<MediaFoundationClipCompressor> _logger;

    public MediaFoundationClipCompressor(ILogger<MediaFoundationClipCompressor> logger)
        : this(AudioSubtypes.MFAudioFormat_AAC, TranscodeContainerTypes.MFTranscodeContainerType_MPEG4, ".m4a", logger)
    {
    }

    internal MediaFoundationClipCompressor(Guid subtype, Guid container, string extension, ILogger<MediaFoundationClipCompressor> logger)
    {
        _subtype = subtype;
        _container = container;
        _extension = extension;
        _logger = logger;
    }

    public CompressedClip? Compress(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (clip.Pcm.IsEmpty)
        {
            return null;
        }

        try
        {
            var standard = clip.Format == AudioFormat.Standard ? clip : PcmConverter.ToStandard(clip);
            var format = new WaveFormat(standard.Format.SampleRate, 16, standard.Format.Channels);

            MediaFoundationApi.Startup();

            var mediaType = MediaFoundationEncoder.SelectMediaType(_subtype, format, BitRate)
                ?? throw new InvalidOperationException("Windows has no encoder for this format.");

            using var input = new RawSourceWaveStream(Readable(standard.Pcm), format);
            using var output = new MemoryStream();
            using (var encoder = new MediaFoundationEncoder(mediaType))
            {
                encoder.Encode(output, input, _container);
            }

            return new CompressedClip(output.ToArray(), _extension);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning("Could not compress the clip {Name}: {Reason}", clip.Name, ex.Message);
            return null;
        }
    }

    public AudioClip? Decompress(byte[] bytes, string name)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            using var reader = new StreamMediaFoundationReader(new MemoryStream(bytes, writable: false));
            var pcm = MediaFoundationDecoder.Standardise(reader, AudioFormat.Standard.Channels);
            using var decoded = new MemoryStream();
            var chunk = new byte[65_536];
            int read;

            while ((read = pcm.Read(chunk, 0, chunk.Length)) > 0)
            {
                decoded.Write(chunk, 0, read);
            }

            return new AudioClip(name, decoded.ToArray(), AudioFormat.Standard);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning("Could not decode the clip {Name}: {Reason}", name, ex.Message);
            return null;
        }
    }

    private static MemoryStream Readable(ReadOnlyMemory<byte> pcm) =>
        MemoryMarshal.TryGetArray(pcm, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(pcm.ToArray(), writable: false);
}
