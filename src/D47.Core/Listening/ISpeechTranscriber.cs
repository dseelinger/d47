namespace D47.Core.Listening;

/// <summary>What a transcriber produced, and how sure it was.</summary>
/// <param name="Text">The words.</param>
public sealed record Transcription(string Text)
{
    /// <summary>How long the transcription itself took, for the diagnostics surface.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>The model that produced it, for the same reason.</summary>
    public string? Model { get; init; }

    /// <summary>How sure the model was, 0 to 1 (Phase 25, "Say it, or type it").</summary>
    public double Confidence { get; init; } = 1;

    /// <summary>Whether nothing was said.</summary>
    public bool IsEmpty => SpeechNoise.IsNothingSaid(Text);
}

/// <summary>Turning an utterance into words.</summary>
public interface ISpeechTranscriber : IDisposable
{
    /// <summary>Which model is loaded, for reporting.</summary>
    string? Model { get; }

    /// <summary>Whether it can transcribe right now.</summary>
    bool IsReady { get; }

    /// <summary>Transcribes one utterance.</summary>
    /// <param name="utterance">Mono float PCM at the rate the utterance declares.</param>
    /// <param name="properNouns">
    /// Names to bias towards — systems, stations and ships drawn from the journal (Phase 6, "Bias
    /// transcription with proper nouns from the journal").
    /// </param>
    Task<Transcription> TranscribeAsync(
        Utterance utterance,
        IReadOnlyList<string> properNouns,
        CancellationToken cancellationToken = default);
}
