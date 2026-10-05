namespace D47.Core.Audio;

/// <summary>A treatment run on <see cref="AudioFormat.Standard"/> PCM in chunks as it arrives.</summary>
/// <remarks>The output does not depend on where the chunks are cut.</remarks>
public interface IPcmFilter
{
    /// <summary>Treats the next PCM and returns what is ready; a trailing part-frame is held for the next push.</summary>
    byte[] Push(ReadOnlySpan<byte> pcm);

    /// <summary>The rest, including any tail. Nothing may be pushed after it.</summary>
    byte[] Finish();
}
