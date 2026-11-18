using D47.Core.Audio;

namespace D47.Tts;

/// <summary>The catalogue a voice-list request that failed returns.</summary>
internal static class VoiceListFailure
{
    /// <summary>
    /// Whether <paramref name="ex"/> is a failure to report. A cancellation the caller asked for is not; an
    /// HttpClient timeout also arrives as a cancellation, and is.
    /// </summary>
    public static bool IsFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    public static VoiceCatalogue From(Exception ex) => VoiceCatalogue.Unreachable(
        ex is OperationCanceledException ? "it did not answer in time" : ex.Message);
}
