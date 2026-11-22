namespace D47.Core.Audio;

/// <summary>48 kHz stereo 16-bit PCM read on demand; Read returns 0 at the end.</summary>
public interface IPcmStream : IDisposable
{
    int Read(Span<byte> buffer);

    /// <summary>The track's length, read when it opened; null where the file does not say.</summary>
    TimeSpan? Length { get; }

    /// <summary>False for a source that can only be read from the start.</summary>
    bool CanSeek => true;

    /// <summary>
    /// Moves to <paramref name="to"/>, clamped to the length; does nothing when <see cref="CanSeek"/> is false.
    /// Called on the thread that opened the stream.
    /// </summary>
    void Seek(TimeSpan to);
}

/// <summary>
/// An ambience track, opened afresh each time it plays and streamed rather than held. A track read from the
/// downloaded library carries its folder and file name; one from the Commander's own folder carries its full path.
/// <see cref="ReadAhead"/>, when set, is how much is kept decoded ahead of playback in place of the default.
/// </summary>
public sealed record MusicTrack(
    string Name,
    Func<IPcmStream> Open,
    string? PackFolder = null,
    string? PackFile = null,
    string? OwnPath = null,
    TimeSpan? ReadAhead = null);
