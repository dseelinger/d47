using D47.Core.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace D47.Tts;

/// <summary>
/// Decodes a voice sample to <see cref="AudioFormat.Standard"/>: a WAV, or an MP3 whose frames may change sample
/// rate or channel mode. Each run of MP3 frames with one format is decoded and resampled on its own.
/// </summary>
internal static class SampleAudioDecoder
{
    public static byte[] Decode(byte[] audio) =>
        IsWave(audio) ? DecodeWave(audio) : DecodeMp3(audio);

    private static bool IsWave(byte[] audio) =>
        audio.Length >= 12
        && audio.AsSpan(0, 4).SequenceEqual("RIFF"u8)
        && audio.AsSpan(8, 4).SequenceEqual("WAVE"u8);

    private static byte[] DecodeWave(byte[] wav)
    {
        using var stream = new MemoryStream(wav, writable: false);
        using var reader = new WaveFileReader(stream);
        using var output = new MemoryStream();

        AppendStandard(reader.ToSampleProvider(), output);

        return output.Length == 0
            ? throw new InvalidDataException("the sample holds no audio")
            : output.ToArray();
    }

    private static byte[] DecodeMp3(byte[] mp3)
    {
        using var stream = new MemoryStream(mp3, writable: false);
        SkipTag(stream);

        using var output = new MemoryStream();
        using var run = new MemoryStream();
        var decoded = new byte[1152 * 4];
        AcmMp3FrameDecompressor? decompressor = null;
        var key = (Rate: 0, Mono: false);
        var first = true;

        try
        {
            while (ReadFrame(stream) is { } frame)
            {
                if (first)
                {
                    first = false;

                    if (XingHeader.LoadXingHeader(frame) is not null)
                    {
                        continue;
                    }
                }

                var frameKey = (frame.SampleRate, frame.ChannelMode == ChannelMode.Mono);

                if (decompressor is null || frameKey != key)
                {
                    Flush(run, decompressor, output);
                    decompressor?.Dispose();
                    decompressor = new AcmMp3FrameDecompressor(new Mp3WaveFormat(
                        frame.SampleRate, frameKey.Item2 ? 1 : 2, frame.FrameLength, frame.BitRate));
                    key = frameKey;
                }

                var count = decompressor.DecompressFrame(frame, decoded, 0);
                run.Write(decoded, 0, count);
            }

            Flush(run, decompressor, output);
        }
        finally
        {
            decompressor?.Dispose();
        }

        return output.Length == 0
            ? throw new InvalidDataException("the sample holds no audio")
            : output.ToArray();
    }

    private static Mp3Frame? ReadFrame(Stream stream)
    {
        try
        {
            return Mp3Frame.LoadFromStream(stream, readData: true);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    private static void SkipTag(Stream stream)
    {
        Span<byte> header = stackalloc byte[10];

        if (stream.Read(header) == 10 && header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            var size = (header[6] << 21) | (header[7] << 14) | (header[8] << 7) | header[9];
            stream.Position = 10 + size + ((header[5] & 0x10) != 0 ? 10 : 0);
            return;
        }

        stream.Position = 0;
    }

    /// <summary>Appends the run's PCM to <paramref name="output"/> as 48 kHz mono 16-bit, then empties the run.</summary>
    private static void Flush(MemoryStream run, AcmMp3FrameDecompressor? decompressor, MemoryStream output)
    {
        if (decompressor is null || run.Length == 0)
        {
            return;
        }

        run.Position = 0;
        AppendStandard(new RawSourceWaveStream(run, decompressor.OutputFormat).ToSampleProvider(), output);
        run.SetLength(0);
    }

    /// <summary>Appends <paramref name="samples"/> to <paramref name="output"/> as 48 kHz mono 16-bit.</summary>
    private static void AppendStandard(ISampleProvider samples, MemoryStream output)
    {
        samples = samples.WaveFormat.Channels switch
        {
            1 => samples,
            2 => new StereoToMonoSampleProvider(samples),
            var channels => throw new TtsException($"ElevenLabs sent a sample with {channels} channels."),
        };

        if (samples.WaveFormat.SampleRate != AudioFormat.Standard.SampleRate)
        {
            samples = new WdlResamplingSampleProvider(samples, AudioFormat.Standard.SampleRate);
        }

        var pcm16 = new SampleToWaveProvider16(samples);
        var buffer = new byte[pcm16.WaveFormat.AverageBytesPerSecond];
        int read;

        while ((read = pcm16.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
        }
    }
}
