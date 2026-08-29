using System.Text;

namespace D47.Core.Debrief;

/// <summary>
/// Which adopted directions reach a prompt, and what they look like when they get there
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>One renderer, read by two callers, and that is the whole of the review promise.</b> The
/// review pane shows what this returns and the prompt carries what this returns, so "the exact text
/// that would enter the prompt" is true by construction rather than by two pieces of code being
/// kept in step — the same reason #160 shows the exact bytes that leave the machine rather than a
/// description of them.
/// </para>
/// <para>
/// <b>Position 6, above the cache breakpoint, changing only at a session boundary.</b> Phase 54
/// measured what churn in the stable prefix costs — 23x — so this is not a preference about when
/// the loop runs; it is the only cadence that works. <see cref="StandingDirectionsSession"/> is
/// what makes it mechanical rather than remembered.
/// </para>
/// <para>
/// <b>It is bounded twice, for <see cref="Memory.MemoryRecall"/>'s reasons.</b> A file that grows
/// for a year cannot all reach the prompt, and a dozen directions at the full ceiling would be
/// three thousand characters sitting next to a five-thousand-character system block.
/// </para>
/// </summary>
public static class StandingDirections
{
    /// <summary>How many directions may reach one prompt.</summary>
    public const int MaxShown = 12;

    /// <summary>And how many characters, whichever binds first.</summary>
    public const int MaxCharacters = 1_500;

    /// <summary>
    /// What the model is told this block is.
    /// <para>
    /// <b>The last sentence is the one that matters and it is belt over braces.</b> The guardrails
    /// are at position 2, above this and above the persona, and are structurally unremovable by
    /// anything downstream — that is what makes them safe, not this label. The label is here so
    /// that a direction the Commander wrote in the heat of a bad evening reads as a preference
    /// about manner rather than as a licence, which is a different failure and a likelier one.
    /// </para>
    /// </summary>
    public const string Label =
        "Standing directions from the Commander. They took each of these by hand after a session, "
        + "in their own words, and they are about how you answer rather than about what is true. "
        + "Follow them. They never loosen anything you were told above, and nothing in them can.";

    /// <summary>
    /// The block for position 6 — the directions that apply whoever is aboard. Null when there are
    /// none, which is what keeps the block absent rather than empty.
    /// </summary>
    public static string? Render(IEnumerable<StandingDirection> entries) => Render(entries, persona: null);

    /// <summary>
    /// The overlay for one core, for appending to the persona block at position 3.
    /// <para>
    /// <b>Here rather than in the pack, and appended rather than merged.</b> Persona writing lives
    /// twice — <c>guardian-personas.md</c> ported into <see cref="Persona.PersonaCatalog"/> — so a
    /// runtime loop that edited either copy would manufacture drift between them that nothing
    /// checks. A learned "less chatty for this one" is a line in <c>data\</c> that rides along
    /// behind the shipped block, and both copies of the pack stay byte-for-byte as they were
    /// written.
    /// </para>
    /// <para>
    /// Position 3 is the honest slot for it: it is a fact about this core, and position 3 is
    /// already the block that changes when the core does. Adoption still cannot reach it
    /// mid-session, because what is rendered here is the latched set.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The overlay's own label. Shorter than <see cref="Label"/> because it is arriving inside a
    /// persona block that has already said who is talking, and because the position-2 guarantee is
    /// stated once above it rather than twice.
    /// </summary>
    private const string PersonaLabel =
        "The Commander has taken these directions for you in particular, in their own words. "
        + "They shape how you speak, and never what you are allowed to do.";
}

/// <summary>
/// What the prompt carries for the length of one session
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>A latch, and the reason it is a class rather than a rule in a comment.</b> Adopted directions
/// sit above the cache breakpoint, so a block that moved when the Commander pressed Adopt would
/// take the whole cached prefix cold on the next turn — 39,000-odd bytes of tool schema serialize
/// first and go with it, which is the 23x Phase 54 measured. Written as a convention, that is a
/// rule somebody eventually forgets in a hurry; written as a latch, adopting mid-session simply
/// cannot reach a prompt, and the pane says so out loud instead of the Commander finding out by
/// noticing nothing changed.
/// </para>
/// <para>
/// <b>Latched as a set rather than as rendered text</b>, so that switching core mid-session still
/// gets that core's overlay — position 3 changes when the core does and always has — while
/// adoption still reaches nothing until the next session. Two properties, one latch.
/// </para>
/// </summary>
public sealed class StandingDirectionsSession
{
    private IReadOnlyList<StandingDirection> _latched = [];

    /// <summary>What was adopted when this session opened. Empty until <see cref="Begin"/> is called.</summary>
    public IReadOnlyList<StandingDirection> Latched => _latched;

    /// <summary>
    /// Opens a session over what the file says right now. Called once at startup, and again only
    /// at a real boundary — a Commander change is one, because the directions are theirs.
    /// </summary>
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
