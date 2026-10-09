using D47.Core.Audio;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>Any file Windows' Media Foundation codecs read, downmixed to mono at the file's own sample rate.</summary>
public sealed class MediaFoundationFileDecoder : IAudioFileDecoder
{
    public DecodedSamples Decode(string path)
    {
        try
        {
            using var reader = new MediaFoundationReader(path);

            if (reader.TotalTime > IAudioFileDecoder.MaxLength)
            {
                throw TooLong();
            }

            var source = reader.ToSampleProvider();
            var mono = source.WaveFormat.Channels > 1 ? new MediaFoundationDecoder.Downmix(source) : source;
            var rate = mono.WaveFormat.SampleRate;
            var limit = (long)(IAudioFileDecoder.MaxLength.TotalSeconds * rate);
            var samples = new List<float>();
            var chunk = new float[rate];
            int read;

            while ((read = mono.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (samples.Count + read > limit)
                {
                    throw TooLong();
                }

                samples.AddRange(chunk.AsSpan(0, read));
            }

            return new DecodedSamples([.. samples], rate);
        }
        catch (Exception ex) when (MediaFoundationDecoder.IsCodecFailure(ex))
        {
            throw MediaFoundationDecoder.Undecodable(ex);
        }
    }

    private static AudioDecodeException TooLong() =>
        new($"The file is longer than {IAudioFileDecoder.MaxLength.TotalMinutes:0} minutes. Use a shorter recording.");
}
