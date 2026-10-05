using D47.Core.Audio;
using NAudio.Wave;

namespace D47.Tts;

/// <summary>
/// Decodes an MP3 that arrives in arbitrary pieces, through the same OS codec as <c>Mp3FileReader</c>. A
/// partial frame is held until the bytes that complete it have arrived.
/// </summary>
internal sealed class Mp3StreamDecoder(int sampleRate, int channels) : IDisposable
{
    private readonly byte[] _decoded = new byte[1152 * 4];

    private byte[] _pending = [];
    private bool _tagChecked;
    private bool _firstFrameSeen;
    private AcmMp3FrameDecompressor? _decompressor;

    /// <summary>The PCM for every frame the bytes so far complete.</summary>
    public byte[] Push(ReadOnlySpan<byte> mp3)
    {
        _pending = [.. _pending, .. mp3];

        if (!_tagChecked && !SkipTag())
        {
            return [];
        }

        var output = new MemoryStream();
        using var stream = new MemoryStream(_pending, writable: false);
        var consumed = 0L;

        while (true)
        {
            Mp3Frame? frame;

            try
            {
                frame = Mp3Frame.LoadFromStream(stream, readData: true);
            }
            catch (EndOfStreamException)
            {
                break;
            }

            if (frame is null)
            {
                break;
            }

            consumed = stream.Position;

            if (!_firstFrameSeen)
            {
                _firstFrameSeen = true;

                if (XingHeader.LoadXingHeader(frame) is not null)
                {
                    continue;
                }
            }

            var decompressor = _decompressor ??= Open(frame);
            var count = decompressor.DecompressFrame(frame, _decoded, 0);
            output.Write(_decoded, 0, count);
        }

        _pending = _pending[(int)consumed..];
        return output.ToArray();
    }

    /// <summary>Skips an ID3v2 tag at the start; false while the tag has not fully arrived.</summary>
    private bool SkipTag()
    {
        if (_pending.Length < 3)
        {
            return false;
        }

        if (_pending[0] != 'I' || _pending[1] != 'D' || _pending[2] != '3')
        {
            _tagChecked = true;
            return true;
        }

        if (_pending.Length < 10)
        {
            return false;
        }

        var size = (_pending[6] << 21) | (_pending[7] << 14) | (_pending[8] << 7) | _pending[9];
        var total = 10 + size + ((_pending[5] & 0x10) != 0 ? 10 : 0);

        if (_pending.Length < total)
        {
            return false;
        }

        _pending = _pending[total..];
        _tagChecked = true;
        return true;
    }

    private AcmMp3FrameDecompressor Open(Mp3Frame frame)
    {
        var decompressor = new AcmMp3FrameDecompressor(new Mp3WaveFormat(
            frame.SampleRate,
            frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
            frame.FrameLength,
            frame.BitRate));

        var format = decompressor.OutputFormat;

        if (format.SampleRate != sampleRate || format.Channels != channels)
        {
            decompressor.Dispose();

            throw new TtsException(
                $"Edge Neural sent {format.SampleRate} Hz / {format.Channels}ch audio where 24 kHz mono was requested.");
        }

        return decompressor;
    }

    public void Dispose() => _decompressor?.Dispose();
}
