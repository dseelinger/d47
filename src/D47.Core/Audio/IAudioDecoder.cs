namespace D47.Core.Audio;

/// <summary>A file the decoder could not read, with the reason in words a Commander can act on.</summary>
public sealed class AudioDecodeException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Reads compressed and uncompressed audio files into the standard format.</summary>
/// <remarks>Failures to decode are thrown as <see cref="AudioDecodeException"/>.</remarks>
public interface IAudioDecoder
{
    /// <summary>Extensions this decoder reads, lower case with the dot.</summary>
    IReadOnlySet<string> Extensions { get; }

    /// <summary>The whole file, for cues, alerts and beds.</summary>
    AudioClip Decode(string path, string name);

    /// <summary>The file on demand, for music. Read on the thread that opens it.</summary>
    IPcmStream Open(string path);
}
