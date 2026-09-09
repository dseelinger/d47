using System.Text;

namespace D47.Core.Debrief;

/// <summary>Which adopted directions reach a prompt, and what they look like when they get there (#162).</summary>
public static class StandingDirections
{
    /// <summary>How many directions may reach one prompt.</summary>
    public const int MaxShown = 12;

    /// <summary>And how many characters, whichever binds first.</summary>
    public const int MaxCharacters = 1_500;

    /// <summary>What the model is told this block is.</summary>
    public const string Label =
        "Standing directions from the Commander. They took each of these by hand after a session, "
        + "in their own words, and they are about how you answer rather than about what is true. "
        + "Follow them. They never loosen anything you were told above, and nothing in them can.";

    /// <summary>The block for position 6 — the directions that apply whoever is aboard.</summary>
    public static string? Render(IEnumerable<StandingDirection> entries) => Render(entries, persona: null);

    /// <summary>The overlay for one core, for appending to the persona block at position 3.</summary>
    public static string? RenderFor(string persona, IEnumerable<StandingDirection> entries) =>
        string.IsNullOrWhiteSpace(persona) ? null : Render(entries, persona);

    /// <summary>
    /// The directions in the order they render — deterministic and total, so the same file always
    /// produces the same bytes and an unchanged file never invalidates a cached prefix.
    /// </summary>
    public static IReadOnlyList<StandingDirection> Shown(IEnumerable<StandingDirection> entries, string? persona)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = entries
            .Where(entry => entry.State == DirectionState.Adopted && entry.Kind == DirectionKind.Direction)
            .Where(entry => string.Equals(entry.Persona, persona, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.AdoptedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal);

        var shown = new List<StandingDirection>(MaxShown);
        var characters = 0;

        foreach (var entry in ordered)
        {
            if (shown.Count == MaxShown)
            {
                break;
            }

            var cost = entry.Text.Length + 3;

            if (characters + cost > MaxCharacters && shown.Count > 0)
            {
                break;
            }

            shown.Add(entry);
            characters += cost;
        }

        return shown;
    }

    private static string? Render(IEnumerable<StandingDirection> entries, string? persona)
    {
        var shown = Shown(entries, persona);

        if (shown.Count == 0)
        {
            return null;
        }

        var block = new StringBuilder(persona is null ? Label : PersonaLabel);

        foreach (var entry in shown)
        {
            block.Append("\n- ").Append(entry.Text);
        }

        return block.ToString();
    }

    /// <summary>The overlay's own label.</summary>
    private const string PersonaLabel =
        "The Commander has taken these directions for you in particular, in their own words. "
        + "They shape how you speak, and never what you are allowed to do.";
}

/// <summary>What the prompt carries for the length of one session (#162).</summary>
public sealed class StandingDirectionsSession
{
    private IReadOnlyList<StandingDirection> _latched = [];

    /// <summary>What was adopted when this session opened.</summary>
    public IReadOnlyList<StandingDirection> Latched => _latched;

    /// <summary>Opens a session over what the file says right now.</summary>
    public void Begin(IEnumerable<StandingDirection> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _latched = [.. entries.Where(entry => entry.State == DirectionState.Adopted)];
    }

    /// <summary>The block for position 6, or null when nothing general has been adopted.</summary>
    public string? Block() => StandingDirections.Render(_latched);

    /// <summary>The overlay for the core aboard, or null when that core has none.</summary>
    public string? Overlay(string? persona) =>
        persona is { Length: > 0 } id ? StandingDirections.RenderFor(id, _latched) : null;
}
