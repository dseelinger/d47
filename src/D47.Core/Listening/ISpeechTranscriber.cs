namespace D47.Core.Listening;

/// <summary>What a transcriber produced, and how sure it was.</summary>
/// <param name="Text">The words. Empty when nothing intelligible was said.</param>
public sealed record Transcription(string Text)
{
    /// <summary>How long the transcription itself took, for the diagnostics surface.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>The model that produced it, for the same reason.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// How sure the model was, 0 to 1 (list.md Phase 25, "Say it, or type it").
    /// <para>
    /// One is the default and means "not reported", which is what every path that does not
    /// produce a figure gets — a test double, a replay, a transcriber that has none. That is the
    /// safe default here rather than zero: the one caller that reads this treats a low figure as
    /// a failure and puts a keyboard back, so a transcriber with nothing to say about its own
    /// confidence must not be read as certain it was wrong.
    /// </para>
    /// <para>
    /// Read by the panel's text-entry loop and by nothing else. The turn path deliberately does
    /// not gate on it: a turn that refused to run because a model was unsure would be a
    /// Commander repeating themselves at a companion that heard them perfectly well, where the
    /// worst case is one wrong answer they can correct. A value being written into a plan is the
    /// case where being unsure is worth acting on.
    /// </para>
    /// </summary>
    public double Confidence { get; init; } = 1;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// Turning an utterance into words. The seam exists in Core so the gate, the settings rows and
/// the turn path can all be written and tested against it, while the implementation lives with
/// its native dependency outside Core (architecture.md §3, D3).
/// </summary>
public interface ISpeechTranscriber : IDisposable
{
    /// <summary>Which model is loaded, for reporting. Null when none is.</summary>
    string? Model { get; }

    /// <summary>
    /// Whether it can transcribe right now. False is a state to report, not a failure — no
    /// model downloaded is the normal condition on a fresh install.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Transcribes one utterance.
    /// </summary>
    /// <param name="utterance">Mono float PCM at the rate the utterance declares.</param>
    /// <param name="properNouns">
    /// Names to bias towards — systems, stations and ships drawn from the journal (list.md
    /// Phase 6, "Bias transcription with proper nouns from the journal"). Journal-derived and
    /// network-free. Proper nouns are where speech recognition fails hardest and most silently:
    /// a misheard system name comes back as a plausible English phrase rather than as an error.
    /// </param>
    Task<Transcription> TranscribeAsync(
        Utterance utterance,
        IReadOnlyList<string> properNouns,
        CancellationToken cancellationToken = default);
}
