namespace D47.Core.Audio;

/// <summary>A clip's compressed bytes and the file extension they are kept under.</summary>
public sealed record CompressedClip(byte[] Bytes, string Extension);

/// <summary>Compresses spoken clips for keeping and reads them back from memory. Both calls block, so neither runs on the tick.</summary>
public interface IClipCompressor
{
    /// <summary>The clip compressed, or null when the encoder is missing or fails, so the caller keeps the WAV.</summary>
    CompressedClip? Compress(AudioClip clip);

    /// <summary>The clip in <see cref="AudioFormat.Standard"/>, or null when the bytes do not decode.</summary>
    AudioClip? Decompress(byte[] bytes, string name);
}
