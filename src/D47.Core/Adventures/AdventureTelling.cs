namespace D47.Core.Adventures;

/// <summary>What kind of thing was said about a story (Phase 47, amended 2026-08-22).</summary>
public enum AdventureToldKind
{
    /// <summary>The opening, or a beat, as it was actually spoken — the model's wording where there was one.</summary>
    Beat,

    /// <summary>
    /// The ship's AI answering the Commander about the story between beats — the flavour the
    /// standing context exists to produce.
    /// </summary>
    Aside,
}

/// <summary>
/// One thing that was said about an adventure, kept (asked for 2026-08-22).
/// <para>
/// <b>What was heard, not what was authored.</b> <see cref="Adventure.Beats"/> holds the line a
/// beat was written with; this holds the line the Commander was actually read, which is the
/// model's wording whenever there was a model — and those are not the same text. A Commander who
/// flies a story over four evenings has no other record of it: the transcript is one session long
/// and carries every other thing d47 said as well.
/// </para>
/// <para>
/// Persisted with the adventure, and capped at <see cref="AdventureLimits.MaxTold"/> — a story is
/// at most twelve beats, and the rest of the room is for the asides between them.
/// </para>
/// </summary>
public sealed record AdventureTold
{
    public required AdventureToldKind Kind { get; init; }

    /// <summary>What was said. For an aside, the ship's AI's reply and not the question.</summary>
    public required string Text { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>The beat index, <c>-1</c> for the opening, and <c>-1</c> for an aside.</summary>
    public int Beat { get; init; } = -1;

    /// <summary>The beat's title, as it was when it fired. Null on an aside.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// What the Commander did to reach it, in words — <em>arrive at Ossen's Lantern</em>. Stored
    /// rather than derived: it is the one line the tab draws in the highlight colour, and an
    /// adventure edited after the fact would otherwise re-describe a beat that has already fired.
    /// </summary>
    public string? Trigger { get; init; }

    /// <summary>What the Commander said, on an aside. Null on a beat, which nobody asked for.</summary>
    public string? Asked { get; init; }
}

/// <summary>
/// The short acknowledgements, said the moment a beat fires (asked for 2026-08-22).
/// <para>
/// <b>These exist because the beat itself is deliberately late.</b>
/// <see cref="AdventureCallout.Settle"/> holds a reached beat for twenty seconds so its prose is
/// not read out over the jump that reached it, and the model then spends up to
/// <c>FlavourBudget</c> rewriting it in the core's voice. That is the right shape for the prose
/// and the wrong shape for feedback: reported as <em>"it can take a while after triggering a
/// trigger for me to hear anything"</em>, with the Commander unsure they had done the thing at
/// all. So the confirmation is split off from the telling — this lands at once and says only
/// <em>yes, that was it</em>, and the beat arrives when it was always going to.
/// </para>
/// <para>
/// <b>Never rewritten by a model, and that is the whole design.</b> A model call is the latency
/// being fixed. They are stock lines picked by index, exactly as <see cref="Callouts.AmbientLines"/>
/// is — which is also why <see cref="AdventureCallout.AckPrefix"/> is a different prefix from
/// <see cref="AdventureCallout.KeyPrefix"/>: <c>FlavourBriefs</c> matches on the prefix, and an
/// acknowledgement sharing one with the beat would be sent to the very round trip it exists to get
/// ahead of.
/// </para>
/// <para>
/// Short, because the Commander is waiting through it: none is longer than four words, so the
/// synthesiser has almost nothing to do before the first sample.
/// </para>
/// </summary>
public static class AdventureAcks
{
    private static readonly string[] Lines =
    [
        "That's it.",
        "You've done it.",
        "Well done.",
        "There it is.",
        "That's the one.",
        "Got it.",
        "Nicely done.",
        "You found it.",
        "That's the place.",
        "Right where it should be.",
    ];

    public static int Count => Lines.Length;

    /// <summary>The line at an index, wrapped. Given an index and nothing else, like the ambient pool.</summary>
    public static string Pick(int index) => Lines[Math.Abs(index) % Lines.Length];
}

/// <summary>
/// Whether a stretch of conversation was about a particular story (asked for 2026-08-22).
/// <para>
/// The heuristic the Commander chose: a turn joins the story's feed when their words or the
/// reply name the story, one of its beats, or a place one of its beats waits at. No model call
/// and no second prompt — a classification turn on every exchange would put a round trip in front
/// of every answer, which is the cost this whole change is trying to remove elsewhere.
/// </para>
/// <para>
/// <b>Whole words, and nothing shorter than four letters.</b> Substring matching on Elite's names
/// is how <em>Sol</em> matches "solar" and a beat called "The Turn" matches "turn left" — so a
/// needle has to sit between non-letters, and the short ones are not looked for at all. It misses
/// an oblique exchange rather than filling the feed with unrelated ones, which is the right way
/// round for a page whose whole promise is <em>adventure only</em>.
/// </para>
/// </summary>
public static class AdventureMention
{
    /// <summary>Shorter than this and a name matches too much to be evidence of anything.</summary>
    public const int ShortestNeedle = 4;

    /// <summary>Whether either side of one exchange was about this adventure.</summary>
    public static bool InExchange(Adventure adventure, string? asked, string? answered) =>
        Mentions(adventure, asked) || Mentions(adventure, answered);

    /// <summary>Whether one piece of text names this adventure, a beat of it, or a place in it.</summary>
    public static bool Mentions(Adventure adventure, string? text)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var needle in Needles(adventure))
        {
            if (Holds(text, needle))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every word the story answers to. Public so a test can say what it is looking for.</summary>
    public static IEnumerable<string> Needles(Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        yield return adventure.Name;

        foreach (var beat in adventure.Beats)
        {
            yield return beat.Title;

            if (beat.Trigger.System is { } system)
            {
                yield return system;
            }

            if (beat.Trigger.Station is { } station)
            {
                yield return station;
            }

            if (beat.Trigger.Body is { } body)
            {
                yield return body;
            }
        }
    }

    /// <summary>
    /// Whether the needle sits in the haystack as a whole word. "The " is stripped off the front
    /// of a title first — a Commander asks about "the Anchorage" and about "Maren Anchorage", and
    /// neither is the string the beat was titled with.
    /// </summary>
    private static bool Holds(string haystack, string? needle)
    {
        if (needle is null)
        {
            return false;
        }

        var wanted = needle.Trim();

        if (wanted.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
        {
            wanted = wanted[4..];
        }

        if (wanted.Length < ShortestNeedle)
        {
            return false;
        }

        var at = haystack.IndexOf(wanted, StringComparison.OrdinalIgnoreCase);

        while (at >= 0)
        {
            var before = at == 0 || !char.IsLetter(haystack[at - 1]);
            var after = at + wanted.Length >= haystack.Length || !char.IsLetter(haystack[at + wanted.Length]);

            if (before && after)
            {
                return true;
            }

            at = haystack.IndexOf(wanted, at + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
