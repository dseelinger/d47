using D47.Core.Listening;
using D47.Core.Vr;

namespace D47.Core.Viewing;

/// <summary>Writes subtitle cues from a stretch of a video's soundtrack, on this computer.</summary>
public interface ISoundtrackCaptioner
{
    /// <summary>The sample rate a stretch is given at: mono, as Whisper takes it.</summary>
    const int SampleRate = 16000;

    /// <summary>Why captions cannot be written, or null when they can.</summary>
    string? Unavailable { get; }

    /// <summary>
    /// The cues spoken in <paramref name="sound"/>, timed from its first sample. Push-to-talk goes first: the stretch
    /// waits while the Listener is busy, and is written again from its start when the Listener starts during it.
    /// </summary>
    Task<IReadOnlyList<SubtitleCue>> CaptionAsync(Utterance sound, CancellationToken cancellationToken);

    /// <summary>Frees what writing loaded; the next stretch loads it again.</summary>
    void Release();
}
