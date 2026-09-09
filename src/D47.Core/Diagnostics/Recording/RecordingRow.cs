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
    /// <summary>A mishear: (WAV, the words that were actually said).</summary>
    Mishear,

    /// <summary>A mispronunciation: (text, the IPA it should have been).</summary>
    Pronunciation,
}

/// <summary>The Commander's hand on a row (#162's adoption gate, applied to test cases).</summary>
/// <param name="Kind">Which corpus it joined.</param>
/// <param name="When">When it was adopted, which is not when it was recorded.</param>
/// <param name="Expected">
/// What it should have been — the words for a mishear, the IPA for a mispronunciation.
/// </param>
public sealed record RecordingKeep(RecordingKeepKind Kind, DateTimeOffset When, string Expected);

/// <summary>
/// One utterance in one direction: the clip, the text, and everything that would otherwise have to be
/// reconstructed backwards from a memory of how it sounded (#164).
/// </summary>
public sealed record RecordingRow
{
    /// <summary>Sortable, unique, and the clip's file name.</summary>
    public required string Id { get; init; }

    public required RecordingDirection Direction { get; init; }

    public required DateTimeOffset When { get; init; }

    /// <summary>The words: what the transcriber returned, or what was being said.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The phoneme string the phonemiser emitted, for a local voice.</summary>
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

    /// <summary>Set when the Commander kept this row as a test case.</summary>
    public RecordingKeep? Kept { get; init; }

    /// <summary>The clip's file name inside the recorder's folder.</summary>
    public string Clip => $"{Id}.wav";

    /// <summary>One line, for a list.</summary>
    public string Line =>
        $"{(Direction == RecordingDirection.Heard ? "heard" : "said")}  "
        + $"{When:HH:mm:ss}  {Duration.TotalSeconds:0.0}s  "
        + (Text is { Length: > 0 } said ? said : "(nothing)");
}
