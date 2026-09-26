namespace D47.Core.Audio;

/// <summary>48 kHz mono 16-bit PCM read on demand; Read returns 0 at the end.</summary>
public interface IPcmStream : IDisposable
{
    int Read(Span<byte> buffer);
}

/// <summary>An ambience track, opened afresh each time it plays and streamed rather than held.</summary>
public sealed record MusicTrack(string Name, Func<IPcmStream> Open);
