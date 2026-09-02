namespace D47.Core.Interface;

/// <summary>How far a spoken scroll moves the page.</summary>
public enum PanelScrollStep
{
    /// <summary>A screenful back, less a line of overlap so nothing is read once and never again.</summary>
    PageUp,

    PageDown,

    /// <summary>A few lines, for a Commander nudging rather than travelling.</summary>
    LineUp,

    LineDown,
}

/// <summary>
/// What happened when a surface was asked to scroll
/// (<a href="https://github.com/dseelinger/d47/issues/263">#263</a>).
/// <para>
/// <b>Three outcomes, where there used to be a bool.</b> A surface that did not move said
/// <c>false</c> for two quite different reasons — already at that end, and nothing on this page
/// scrolls at all — and the caller could not tell them apart, so it treated both as "the phrase
/// was not a scroll" and let the sentence fall through to the language model. What came back was a
/// model guessing at a phrase it was never meant to see, at the cost of a turn.
/// </para>
/// <para>
/// The intent was already written down at the branch that returned false: a Commander who says
/// "page down" at the bottom should hear that they are at the bottom. It just had no way to travel.
/// </para>
/// </summary>
public enum PanelScrollOutcome
{
    /// <summary>Nothing on this surface scrolls — no scroller, or the content fits.</summary>
    NothingToScroll,

    /// <summary>There is a page, and it is already at the end the Commander asked for.</summary>
    AlreadyThere,

    /// <summary>The page moved.</summary>
    Moved,
}

/// <summary>
/// Scrolling the panel by saying so (<a href="https://github.com/dseelinger/d47/issues/34">#34</a>).
/// <para>
/// <b>Dragging the scrollbar was the whole of it, and on one surface there was nothing at all.</b>
/// In the headset a ray on the bar is the only way to move a page — the thumbsticks are unbound and
/// stay that way — and the flat overlay is click-through, so the wheel passes straight through it
/// too. Giving that surface pages to show without giving it a way to scroll them was a hole, and
/// this is the way out of it that costs no pointer.
/// </para>
/// <para>
/// <b>Beside <see cref="PanelPhrases"/> rather than inside it</b>, because they answer different
/// questions about the same utterance: that one decides <em>where</em> the panel is and this one
/// decides <em>how far down</em>. Neither is a tool — nothing an in-game message says should move
/// the Commander's page — so both are matched here, model-free, and cost no tool surface.
/// </para>
/// <para>
/// <b>Whole-utterance and precise</b>, which is the router's own rule and load-bearing for the
/// same reason: "page down" is an instruction and "what does page down do" is a question, and a
/// scroll that fires on the second one moves the page out from under somebody mid-sentence.
/// </para>
/// </summary>
public static class PanelScroll
{
    /// <summary>
    /// Every phrase and what it does. A flat table rather than a switch, so the set a test walks
    /// and the set the Commander can say are the same object.
    /// <para>
    /// The four the Commander asked for, with the synonyms somebody reaches for having forgotten
    /// which four they were. <b>"go up" and "up a level" are deliberately absent</b>: those belong
    /// to <see cref="PanelPhrases.Back"/> and mean leave this page, which is a different thing from
    /// reading further up it.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PanelScrollStep> Phrases =
        new Dictionary<string, PanelScrollStep>(StringComparer.OrdinalIgnoreCase)
        {
            ["page down"] = PanelScrollStep.PageDown,
            ["page forward"] = PanelScrollStep.PageDown,
            ["next page"] = PanelScrollStep.PageDown,

            ["page up"] = PanelScrollStep.PageUp,
            ["page back"] = PanelScrollStep.PageUp,
            ["previous page"] = PanelScrollStep.PageUp,

            ["scroll down"] = PanelScrollStep.LineDown,
            ["down a bit"] = PanelScrollStep.LineDown,
            ["scroll down a bit"] = PanelScrollStep.LineDown,

            ["scroll up"] = PanelScrollStep.LineUp,
            ["up a bit"] = PanelScrollStep.LineUp,
            ["scroll up a bit"] = PanelScrollStep.LineUp,
        };

    /// <summary>
    /// How many lines a nudge moves, where a page moves a screenful. Three because that is what a
    /// wheel notch does nearly everywhere, and a Commander who says "scroll down" twice expects to
    /// have gone about as far as one flick of a wheel.
    /// </summary>
    public const int Lines = 3;

    /// <summary>
    /// Which step this utterance asks for, or null when it asks for none — which is the common
    /// case and falls through to whatever else wanted the sentence.
    /// </summary>
    public static PanelScrollStep? Match(string spoken)
    {
        var said = Normalise(spoken);

        return said.Length > 0 && Phrases.TryGetValue(said, out var step) ? step : null;
    }

    /// <summary>
    /// What to say back, given what every surface did with the step
    /// (<a href="https://github.com/dseelinger/d47/issues/263">#263</a>).
    /// <para>
    /// <b>A matched phrase is always answered.</b> This used to be "answer only if something
    /// moved", and the rest fell through to the language model — which cost a request to be told
    /// it had no tool for keystrokes. The vocabulary above is sixteen exact phrases, closed and
    /// unambiguous, so a match is a request to scroll and there is nothing else it could have been
    /// meant for.
    /// </para>
    /// <para>
    /// <b>A move anywhere wins, and being at the end of a real page beats there being no page.</b>
    /// A Commander says a phrase once, into a room with up to three surfaces in it: what they want
    /// to hear is what happened to the one they are reading, and the surface that is showing
    /// something is the one that answers for the room.
    /// </para>
    /// <para>
    /// Here rather than in the host so it can be asserted against the words themselves. The host
    /// gathers the outcomes; what they mean out loud is a question about this vocabulary.
    /// </para>
    /// </summary>
    public static string Answer(PanelScrollStep step, IEnumerable<PanelScrollOutcome> outcomes)
    {
        var seen = outcomes.ToList();

        if (seen.Contains(PanelScrollOutcome.Moved))
        {
            return step switch
            {
                PanelScrollStep.PageDown => "Page down.",
                PanelScrollStep.PageUp => "Page up.",
                PanelScrollStep.LineDown => "Scrolled down.",
                _ => "Scrolled up.",
            };
        }

        if (!seen.Contains(PanelScrollOutcome.AlreadyThere))
        {
            return "There is nothing to scroll here.";
        }

        return step is PanelScrollStep.PageDown or PanelScrollStep.LineDown
            ? "Already at the bottom."
            : "Already at the top.";
    }

    /// <summary>
    /// What was said, reduced to the words: no surrounding punctuation and no doubled spaces. The
    /// same reduction <see cref="PanelPhrases"/> makes, so "page down." and "page  down" are one
    /// phrase in both places.
    /// </summary>
    private static string Normalise(string spoken) => string.Join(
        ' ',
        spoken.Split(
            [' ', '\t', '\r', '\n', '.', ',', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
