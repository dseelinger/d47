namespace D47.Core.Audio;

/// <summary>Mono float samples at the file's own sample rate.</summary>
public sealed record DecodedSamples(float[] Samples, int SampleRate);

/// <summary>Reads an audio file into mono samples.</summary>
/// <remarks>Failures to decode, and a file longer than <see cref="MaxLength"/>, are thrown as <see cref="AudioDecodeException"/>.</remarks>
public interface IAudioFileDecoder
{
    /// <summary>A longer file is refused before it is decoded.</summary>
    public static readonly TimeSpan MaxLength = TimeSpan.FromMinutes(10);

    DecodedSamples Decode(string path);
}
