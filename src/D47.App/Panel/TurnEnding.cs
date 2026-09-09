namespace D47.App.Panel;

/// <summary>
/// What the transcript says when a response does not finish normally: one line in the conversation, and
/// optionally one for the log.
/// </summary>
/// <param name="Conversation">
/// The line the Commander reads, in the same register as everything else d47 says.
/// </param>
/// <param name="Technical">
/// Whether there is anything worth recording, or null when there is not — a cancel is not a defect to
/// file.
/// </param>
internal readonly record struct TurnEnding(string Conversation, string? Technical)
{
    /// <summary>A turn the Commander called off is not a turn that failed (#222).</summary>
    /// <param name="thrown">What came out of the turn.</param>
    /// <param name="calledOff">
    /// Whether this turn's own token was cancelled, rather than only that something threw a
    /// cancellation.
    /// </param>
    public static TurnEnding For(Exception thrown, bool calledOff) =>
        calledOff && thrown is OperationCanceledException
            ? new TurnEnding("\n[cancelled]", null)
            : new TurnEnding(
                "\nI couldn't answer that. The details are on the Log File reading.",
                $"\n[response failed: {thrown.Message}]");
}
