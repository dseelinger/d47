namespace D47.App.Panel;

/// <summary>
/// What the transcript says when a response does not finish normally: one line in the
/// conversation, and optionally one for the log.
/// </summary>
/// <param name="Conversation">
/// The line the Commander reads, in the same register as everything else d47 says.
/// </param>
/// <param name="Technical">
/// Whether there is anything worth recording, or null when there is not — a cancel is not a defect
/// to file. It named a line for the Technical reading until
/// <a href="https://github.com/dseelinger/d47/issues/260">#260</a> deleted that page; what it now
/// decides is whether the exception is logged with its stack.
/// </param>
internal readonly record struct TurnEnding(string Conversation, string? Technical)
{
    /// <summary>
    /// <b>A turn the Commander called off is not a turn that failed</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/222">#222</a>).
    /// <para>
    /// Cancelling threw out of the await like anything else, so it landed in the same catch as a
    /// bug and was reported as one: <em>"I couldn't answer that. The details are on the Technical
    /// page."</em> There was nothing there, because nothing went wrong — the Commander pressed
    /// Cancel. Being sent to look for a fault that does not exist is worse than being told nothing.
    /// </para>
    /// <para>
    /// <b>And that sentence named a page a Commander could not open</b> (#260). The Technical
    /// reading was withdrawn in #231 and the line went on sending people to it for two releases —
    /// the same fault the paragraph above is about, arriving by a different road. It names the
    /// <em>Log File</em> reading now, which is where the exception is actually written.
    /// </para>
    /// <para>
    /// <b>The Commander's own words for it: <c>[cancelled]</c>.</b> Bracketed, because it is a
    /// note about the conversation rather than a thing d47 said — the same shape the crew-turn
    /// prefix and the technical lines already use. And it is written rather than spoken, which is
    /// not a detail: Cancel exists to stop the voice, so a cancel that answered out loud would be
    /// the one command in d47 that does the opposite of what it says.
    /// </para>
    /// </summary>
    /// <param name="thrown">What came out of the turn.</param>
    /// <param name="calledOff">
    /// Whether <em>this turn's own</em> token was cancelled, rather than only that something threw
    /// a cancellation. A provider abandoning its own request throws the same exception type and is
    /// a failure, so the token is what separates them rather than the type.
    /// </param>
    public static TurnEnding For(Exception thrown, bool calledOff) =>
        calledOff && thrown is OperationCanceledException
            ? new TurnEnding("\n[cancelled]", null)
            : new TurnEnding(
                "\nI couldn't answer that. The details are on the Log File reading.",
                $"\n[response failed: {thrown.Message}]");
}
