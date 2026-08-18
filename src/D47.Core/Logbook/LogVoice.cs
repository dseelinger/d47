namespace D47.Core.Logbook;

/// <summary>
/// Whose log it is (list.md Phase 33, item 3). Two voices in the item, and a third asked for
/// while the plan of record was being written.
/// <para>
/// <b>They are not interchangeable and the difference is the point.</b> The Commander's own first
/// person is what a Commander has been writing since 3300 and needs nothing from d47 but the facts.
/// The ship's AI writing about the Commander is the one only d47 can do — and it is therefore the
/// one that has to be opted into rather than the one that happens to you.
/// </para>
/// </summary>
public enum LogVoice
{
    /// <summary>The Commander, in their own words. The shipped default, and the plain one.</summary>
    FirstPerson,

    /// <summary>The selected persona, writing about the Commander.</summary>
    ShipsAi,

    /// <summary>The Commander's own account, with the persona interjecting.</summary>
    FirstPersonWithCommentary,
}

/// <summary>
/// Ids, labels, and the rule that governs the two voices needing a personality.
/// <para>
/// <b>The protection is item 3's, in one method.</b> A log is d47 speaking at length, so a
/// personality switched off writes plainly rather than writing as somebody else — and
/// <see cref="Resolve"/> is the only place that decides it, so the prompt, the header and the
/// spoken answer cannot disagree about which voice actually wrote the file.
/// </para>
/// </summary>
public static class LogVoices
{
    public static IReadOnlyList<string> Ids { get; } = ["first-person", "ships-ai", "first-person-with-commentary"];

    public static string IdOf(LogVoice voice) => voice switch
    {
        LogVoice.ShipsAi => "ships-ai",
        LogVoice.FirstPersonWithCommentary => "first-person-with-commentary",
        _ => "first-person",
    };

    public static LogVoice Parse(string? id) => (id ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "ships-ai" => LogVoice.ShipsAi,
        "first-person-with-commentary" => LogVoice.FirstPersonWithCommentary,
        _ => LogVoice.FirstPerson,
    };

    public static string LabelOf(string id) => Parse(id) switch
    {
        LogVoice.ShipsAi => "D47 writes about you",
        LogVoice.FirstPersonWithCommentary => "You write, D47 chips in",
        _ => "You write it, in your own words",
    };

    /// <summary>Whether this voice needs a persona at all.</summary>
    public static bool NeedsPersona(LogVoice voice) => voice is not LogVoice.FirstPerson;

    /// <summary>
    /// What voice actually writes, given whether there is a personality to write in.
    /// <para>
    /// <c>ships-ai</c> with the personality off becomes plain reportage rather than the persona,
    /// and <c>first-person-with-commentary</c> loses its commentary and becomes the Commander's
    /// own account. Neither silently substitutes a different character, which is the failure item
    /// 3 names: a log is d47 at length, and d47 with its personality off is not somebody else.
    /// </para>
    /// </summary>
    public static LogVoice Resolve(LogVoice asked, bool personalityEnabled) =>
        personalityEnabled || !NeedsPersona(asked) ? asked : LogVoice.FirstPerson;

    /// <summary>
    /// What the file's header says happened, when the voice asked for is not the voice used. Null
    /// when they agree, which is the ordinary case.
    /// </summary>
    public static string? Degraded(LogVoice asked, LogVoice used) =>
        asked == used
            ? null
            : $"You asked for \"{LabelOf(IdOf(asked))}\". Personality is switched off, so this was written "
              + $"as \"{LabelOf(IdOf(used))}\" instead — D47 does not write as somebody else when it has "
              + "been told not to be anybody.";
}
