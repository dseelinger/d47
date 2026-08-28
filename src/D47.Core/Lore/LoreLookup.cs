namespace D47.Core.Lore;

/// <summary>
/// The second half of an arrival remark: what a web search turns up about the place, and the
/// rules for saying it (Phase 23, "Look it up, and say where the answer came from").
/// <para>
/// <b>This is the first unprompted untrusted prose d47 speaks.</b> Every other untrusted path is
/// either the Commander having asked — a turn — or a callout assembled from journal fields. So
/// the rule is stated here rather than inherited: <b>what the search found is spoken as a search
/// result</b>, and the attribution is structural. <see cref="Spoken"/> is the only way this text
/// reaches a voice, and there is no arm of it that produces the table's flat voice.
/// </para>
/// <para>
/// <b>No result is ever written down.</b> Web Search is the provider's own server-side tool, so
/// what reaches d47 is prose in a turn; nothing here returns a <see cref="LoreEntry"/> and no
/// caller could build one from what it does return. That is the standing rule enforced by there
/// being no code path rather than by a policy somebody has to remember. It is also why the
/// runtime cache was declined: a stored search result sits close to the edge of that rule, and
/// with the remark already limited to once per system per day a cache would almost never be hit.
/// </para>
/// <para>
/// Pure, and in Core, so the interesting parts are assertable against a string rather than
/// against a running app. What stays in the app is making the call and knowing whether the
/// endpoint can search at all.
/// </para>
/// </summary>
public static class LoreLookup
{
    /// <summary>
    /// How long the follow-up may take before it is abandoned. Generous next to the three seconds
    /// a carrier's line gets, because a server-side search is a round trip through somebody
    /// else's index rather than one sentence of generation — and cheap to be generous with, since
    /// nothing is waiting on it.
    /// </summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(45);

    /// <summary>
    /// What the model is asked. Authored by d47 and carrying only the system's name — untrusted
    /// content reaches these prompts as game state, below the cache breakpoint, never as the
    /// instruction itself (architecture.md §7).
    /// </summary>
    public static string Instruction(string systemName) =>
        $"Search the web for what is notable about the Elite Dangerous system \"{systemName}\" — "
        + "its history, what has been found there, or why Commanders go. Reply with one or two "
        + "sentences of what you actually found, and nothing else. If the search turns up nothing "
        + "specific to that system, reply with exactly: NOTHING. Do not guess, do not describe its "
        + "stars or planets, and do not mention searching.";

    /// <summary>
    /// The exact word a model is told to answer with when it found nothing, so "no result" is a
    /// value rather than a sentence somebody has to recognise. Compared case-insensitively and
    /// against a trimmed reply, because a model that adds a full stop still means it.
    /// </summary>
    public const string NothingFound = "NOTHING";

    /// <summary>
    /// The line to speak, or null when there is nothing worth saying. <b>The attribution is added
    /// here and cannot be opted out of</b>, which is the whole of the rule above.
    /// </summary>
    public static string? Spoken(string? found)
    {
        var line = found?.Trim();

        if (string.IsNullOrEmpty(line)
            || line.TrimEnd('.').Equals(NothingFound, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"From a search: {line}";
    }

    /// <summary>
    /// What is said instead when the Commander asked for a lookup and the endpoint cannot run
    /// one — appended to the bare fact rather than replacing it.
    /// <para>
    /// <b>Availability is knowable before d47 opens its mouth</b>, so a Commander whose provider
    /// has no server-side search is told that nothing further is coming rather than left waiting
    /// for a second part that never arrives.
    /// </para>
    /// </summary>
    public const string CannotSearch = "I cannot search from here, so that is all I have on it.";

    /// <summary>
    /// Whether a result that has just come back is still worth speaking.
    /// <para>
    /// <b>A follow-up that arrives stale is dropped rather than spoken.</b> The Commander may be
    /// interdicted or three jumps away by the time it lands, and a sentence about a system they
    /// left is worse than silence. The test is where they are now, not how long it took: a slow
    /// search that finishes while they are still admiring the view is fine, and a fast one that
    /// lands after a jump is not.
    /// </para>
    /// </summary>
    public static bool StillHere(long remarkedOn, long? whereTheyAreNow) => whereTheyAreNow == remarkedOn;
}
