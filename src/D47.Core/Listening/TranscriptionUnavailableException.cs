namespace D47.Core.Listening;

/// <summary>Why a cloud transcriber gave no words.</summary>
public enum TranscriptionFailure
{
    /// <summary>Anything not named below.</summary>
    Failed,

    /// <summary>No answer: DNS, connect or timeout.</summary>
    Unreachable,

    /// <summary>The key was refused, or none is stored.</summary>
    KeyRejected,

    /// <summary>The service answered 429.</summary>
    RateLimited,
}

/// <summary>A transcription service could not transcribe an utterance.</summary>
public sealed class TranscriptionUnavailableException(
    string provider,
    TranscriptionFailure reason,
    string message,
    Exception? inner = null)
    : Exception(message, inner)
{
    public string Provider { get; } = provider;

    public TranscriptionFailure Reason { get; } = reason;

    /// <summary>What the service itself said went wrong, where it said anything.</summary>
    public string? Detail { get; init; }
}
