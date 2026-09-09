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

/// <summary>What happened when a surface was asked to scroll (#263).</summary>
public enum PanelScrollOutcome
{
    /// <summary>Nothing on this surface scrolls — no scroller, or the content fits.</summary>
    NothingToScroll,

    /// <summary>There is a page, and it is already at the end the Commander asked for.</summary>
    AlreadyThere,

    /// <summary>The page moved.</summary>
    Moved,
}

/// <summary>Scrolling the panel by saying so (#34).</summary>
public static class PanelScroll
{
    /// <summary>Every phrase and what it does.</summary>
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

    /// <summary>How many lines a nudge moves, where a page moves a screenful.</summary>
    public const int Lines = 3;

    /// <summary>
    /// Which step this utterance asks for, or null when it asks for none — which is the common case and
    /// falls through to whatever else wanted the sentence.
    /// </summary>
    public static PanelScrollStep? Match(string spoken)
    {
        var said = Normalise(spoken);

        return said.Length > 0 && Phrases.TryGetValue(said, out var step) ? step : null;
    }

    /// <summary>What to say back, given what every surface did with the step (#263).</summary>
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

    /// <summary>What was said, reduced to the words: no surrounding punctuation and no doubled spaces.</summary>
    private static string Normalise(string spoken) => string.Join(
        ' ',
        spoken.Split(
            [' ', '\t', '\r', '\n', '.', ',', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
