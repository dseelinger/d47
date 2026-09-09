namespace D47.Core.Adventures;

/// <summary>What kind of thing was said about a story (Phase 47, amended 2026-08-22).</summary>
public enum AdventureToldKind
{
    /// <summary>
    /// The opening, or a beat, as it was actually spoken — the model's wording where there was one.
    /// </summary>
    Beat,

    /// <summary>
    /// The ship's AI answering the Commander about the story between beats — the flavour the standing
    /// context exists to produce.
    /// </summary>
    Aside,
}

/// <summary>One thing that was said about an adventure, kept (asked for 2026-08-22).</summary>
public sealed record AdventureTold
{
    public required AdventureToldKind Kind { get; init; }

    /// <summary>What was said.</summary>
    public required string Text { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>The beat index, <c>-1</c> for the opening, and <c>-1</c> for an aside.</summary>
    public int Beat { get; init; } = -1;

    /// <summary>The beat's title, as it was when it fired.</summary>
    public string? Title { get; init; }

    /// <summary>What the Commander did to reach it, in words — arrive at Ossen's Lantern.</summary>
    public string? Trigger { get; init; }

    /// <summary>What the Commander said, on an aside.</summary>
    public string? Asked { get; init; }
}

/// <summary>The short acknowledgements, said the moment a beat fires (asked for 2026-08-22).</summary>
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

    /// <summary>The line at an index, wrapped.</summary>
    public static string Pick(int index) => Lines[Math.Abs(index) % Lines.Length];
}

/// <summary>Whether a stretch of conversation was about a particular story (asked for 2026-08-22).</summary>
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

    /// <summary>Every word the story answers to.</summary>
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
    /// Whether the needle sits in the haystack as a whole word. "The " is stripped off the front of a
    /// title first — a Commander asks about "the Anchorage" and about "Maren Anchorage", and neither is
    /// the string the beat was titled with.
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
