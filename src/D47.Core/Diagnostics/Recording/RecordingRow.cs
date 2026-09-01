namespace D47.Core.Diagnostics.Recording;

/// <summary>Which way across the audio boundary one row went.</summary>
public enum RecordingDirection
{
    /// <summary>The exact buffer handed to the transcriber, beside what it claimed it heard.</summary>
    Heard,

    /// <summary>What actually left the speakers, taken from the arbiter's render reference tap.</summary>
    Spoken,
}

/// <summary>What a kept row was kept as, which decides which corpus it joins.</summary>
public enum RecordingKeepKind
{
    /// <summary>A mishear: (WAV, the words that were actually said). Corpus replay, for audio.</summary>
    Mishear,

    /// <summary>A mispronunciation: (text, the IPA it should have been). The pronunciation guard's data.</summary>
    Pronunciation,
}

/// <summary>
/// The Commander's hand on a row (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>'s
/// adoption gate, applied to test cases). Nothing becomes a test without one of these.
/// </summary>
/// <param name="Kind">Which corpus it joined.</param>
/// <param name="When">When it was adopted, which is not when it was recorded.</param>
/// <param name="Expected">
/// What it should have been — the words for a mishear, the IPA for a mispronunciation. Typed by
/// the Commander: it is the half of a test case that the recording cannot supply.
/// </param>
public sealed record RecordingKeep(RecordingKeepKind Kind, DateTimeOffset When, string Expected);

/// <summary>
/// One utterance in one direction: the clip, the text, and everything that would otherwise have
/// to be reconstructed backwards from a memory of how it sounded
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
/// <para>
/// <see cref="Phonemes"/> is the column that earns the row. Every pronunciation fix on the
/// kokoro-era-polish branch started from something sounding wrong in the headset and worked
/// backwards to the phonemes; holding them beside the audio turns <em>observ-eh</em> from an
/// anecdote into a diagnosis. It is null for every provider that takes text — only the local
/// voice has phonemes to state, because only the local voice is given them by d47.
/// </para>
/// </summary>
public sealed record RecordingRow
{
    /// <summary>Sortable, unique, and the clip's file name. Oldest sorts first by construction.</summary>
    public required string Id { get; init; }

    public required RecordingDirection Direction { get; init; }

    public required DateTimeOffset When { get; init; }

    /// <summary>The words: what the transcriber returned, or what was being said.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The phoneme string the phonemiser emitted, for a local voice. Null otherwise.</summary>
    public string? Phonemes { get; init; }

    /// <summary>Which service spoke it, named by the client rather than by settings.</summary>
    public string? Provider { get; init; }

    /// <summary>The voice, as a person would say it — the name with the id beside it.</summary>
    public string? Voice { get; init; }

    /// <summary>The transcription model, for a heard row.</summary>
    public string? Model { get; init; }

    /// <summary>How long the transcription or the synthesis itself took.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>How long the audio is.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>What the clip costs on disk, so the cap can be enforced without stat-ing files.</summary>
    public long Bytes { get; init; }

    /// <summary>
    /// Set when the Commander kept this row as a test case. A kept row outlives the rolling
    /// window on purpose — that is what keeping means — so it is exempt from the cap and is
    /// copied out of the ring rather than referenced inside it.
    /// </summary>
    public RecordingKeep? Kept { get; init; }

    /// <summary>The clip's file name inside the recorder's folder.</summary>
    public string Clip => $"{Id}.wav";

    /// <summary>One line, for a list. The direction reads as an arrow because that is the fact.</summary>
    public string Line =>
        $"{(Direction == RecordingDirection.Heard ? "heard" : "said")}  "
        + $"{When:HH:mm:ss}  {Duration.TotalSeconds:0.0}s  "
        + (Text is { Length: > 0 } said ? said : "(nothing)");
}
