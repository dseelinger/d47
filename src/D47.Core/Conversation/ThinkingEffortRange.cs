namespace D47.Core.Conversation;

/// <summary>
/// The effort ladder, and the floor and ceiling a Commander can hold it between (list.md
/// Phase 54, "A floor and a ceiling").
/// <para>
/// <b>One place, so the clamp and the settings rows cannot disagree.</b> The rows offer a
/// truncated ladder, the clamp applies the bounds, and both read the order and the spelling
/// from here — two hand-written lists would drift the first time a rung was added, which is
/// exactly what <c>Xhigh</c> arriving between High and Max would have done.
/// </para>
/// <para>
/// <b>What the bounds are for.</b> The floor is the Commander saying a lookup still deserves
/// more than the cheapest setting; the ceiling is a cost dial, and it earns its keep twice
/// over because <see cref="EffortRouter"/> matches substrings with no word boundaries — "what
/// do you think about" hits "think about" and routes to Max.
/// </para>
/// </summary>
public static class ThinkingEffortRange
{
    /// <summary>
    /// The rungs in order, cheapest first. <see cref="ThinkingEffort"/>'s declaration order
    /// <em>is</em> the ladder, so this is read from the enum rather than written out again.
    /// </summary>
    public static IReadOnlyList<ThinkingEffort> Ladder { get; } = Enum.GetValues<ThinkingEffort>();

    /// <summary>Every rung as a settings row writes it.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Ladder.Select(Name)];

    /// <summary>The cheapest rung there is. Null floor means this.</summary>
    public static ThinkingEffort Lowest => Ladder[0];

    /// <summary>The hardest rung there is. Null ceiling means this.</summary>
    public static ThinkingEffort Highest => Ladder[^1];

    /// <summary>
    /// How a rung is written down. The enum's own name, matching how every other choice row
    /// spells an enum — the settings <em>file</em> camel-cases it at the JSON seam, and that
    /// is the store's business rather than the row's.
    /// </summary>
    public static string Name(ThinkingEffort effort) => effort.ToString();

    /// <summary>
    /// A rung from a row value, or null when the text names none. Guarded with
    /// <see cref="Enum.IsDefined{T}(T)"/> because <see cref="Enum.TryParse{T}(string, bool, out T)"/>
    /// happily parses "7" into a rung that does not exist, and a hand-edited settings file is
    /// exactly where that would come from.
    /// </summary>
    public static ThinkingEffort? Parse(string? value) =>
        Enum.TryParse<ThinkingEffort>(value, ignoreCase: true, out var effort) && Enum.IsDefined(effort)
            ? effort
            : null;

    /// <summary>
    /// The rung a turn actually runs at: what the router asked for, held between the bounds the
    /// Commander set. Both null is behaviour identical to no bounds at all.
    /// <para>
    /// <b>The bounds are ordered before they are applied.</b> A hand-edited settings file can
    /// put the floor above the ceiling, and Core must never throw on a settings file — which is
    /// what <c>Math.Clamp</c> does when the minimum exceeds the maximum, and is why the
    /// comparison is written out here rather than delegated. Swapping them is the reading that
    /// keeps both values meaning something; refusing the pair would mean deciding which of the
    /// two the Commander meant.
    /// </para>
    /// <para>
    /// Written out for a second reason as well: <c>Math.Clamp</c>'s generic overload constrains
    /// to <see cref="IComparable{T}"/>, which an enum does not implement, so it binds to the
    /// numeric overloads and will not take a rung at all.
    /// </para>
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
    /// The rungs at or above <paramref name="lowest"/> — what the ceiling row may offer, so it
    /// cannot be set below the floor from the picker.
    /// </summary>
    public static IReadOnlyList<string> NamesFrom(ThinkingEffort lowest) =>
        [.. Ladder.Where(effort => effort >= lowest).Select(Name)];

    /// <summary>
    /// The rungs at or below <paramref name="highest"/> — what the floor row may offer, for the
    /// same reason in the other direction.
    /// </summary>
    public static IReadOnlyList<string> NamesUpTo(ThinkingEffort highest) =>
        [.. Ladder.Where(effort => effort <= highest).Select(Name)];
}
