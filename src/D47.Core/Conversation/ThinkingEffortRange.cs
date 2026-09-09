namespace D47.Core.Conversation;

/// <summary>
/// The effort ladder, and the floor and ceiling a Commander can hold it between (Phase 54, "A floor and
/// a ceiling").
/// </summary>
public static class ThinkingEffortRange
{
    /// <summary>The rungs in order, cheapest first.</summary>
    public static IReadOnlyList<ThinkingEffort> Ladder { get; } = Enum.GetValues<ThinkingEffort>();

    /// <summary>Every rung as a settings row writes it.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Ladder.Select(Name)];

    /// <summary>The cheapest rung there is.</summary>
    public static ThinkingEffort Lowest => Ladder[0];

    /// <summary>The hardest rung there is.</summary>
    public static ThinkingEffort Highest => Ladder[^1];

    /// <summary>How a rung is written down.</summary>
    public static string Name(ThinkingEffort effort) => effort.ToString();

    /// <summary>A rung from a row value, or null when the text names none.</summary>
    public static ThinkingEffort? Parse(string? value) =>
        Enum.TryParse<ThinkingEffort>(value, ignoreCase: true, out var effort) && Enum.IsDefined(effort)
            ? effort
            : null;

    /// <summary>
    /// The rung a turn actually runs at: what the router asked for, held between the bounds the
    /// Commander set.
    /// </summary>
    public static ThinkingEffort Clamp(ThinkingEffort chosen, ThinkingEffort? floor, ThinkingEffort? ceiling)
    {
        var first = floor ?? Lowest;
        var second = ceiling ?? Highest;

        var low = first <= second ? first : second;
        var high = first <= second ? second : first;

        if (chosen < low)
        {
            return low;
        }

        return chosen > high ? high : chosen;
    }

    /// <summary>
    /// The rungs at or above <paramref name="lowest"/> — what the ceiling row may offer, so it cannot
    /// be set below the floor from the picker.
    /// </summary>
    public static IReadOnlyList<string> NamesFrom(ThinkingEffort lowest) =>
        [.. Ladder.Where(effort => effort >= lowest).Select(Name)];

    /// <summary>
    /// The rungs at or below <paramref name="highest"/> — what the floor row may offer, for the same
    /// reason in the other direction.
    /// </summary>
    public static IReadOnlyList<string> NamesUpTo(ThinkingEffort highest) =>
        [.. Ladder.Where(effort => effort <= highest).Select(Name)];
}
