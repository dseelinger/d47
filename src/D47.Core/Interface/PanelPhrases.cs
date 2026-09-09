using D47.Core.Help;

namespace D47.Core.Interface;

/// <summary>Moving the panel by saying so (Phase 25, "Drill in, and find your way back").</summary>
public static class PanelPhrases
{
    /// <summary>Asking what you are looking at (asked for 2026-08-22).</summary>
    public static readonly IReadOnlyList<string> Help =
    [
        "help",
        "help me",
        "what is this",
        "what is this page",
        "what does this do",
        "how does this work",
        "explain this",
        "explain this page",
    ];

    /// <summary>Going back a level, however it is asked for.</summary>
    public static readonly IReadOnlyList<string> Back =
        ["back", "go back", "take me back", "go up", "up a level"];

    /// <summary>The openers a destination may be named after.</summary>
    private static readonly IReadOnlyList<string> Openers =
    [
        string.Empty,
        .. Conversation.SpokenOpeners.All,
        "set the tab to ",
        "switch to the tab ",
        "go to the tab ",
        "set tab to ",
        "switch to tab ",
        "select the tab ",
        "go to tab ",
        "select tab ",
        "open the tab ",
        "open tab ",
        "tab ",
    ];

    /// <summary>
    /// What a destination may be called after its name. "Select the checklist tab" was the reported
    /// miss (2026-08-21): the word "tab" is how a Commander looking at the bar names what is on it, and
    /// the phrase fell through to the model, which has no tool for the panel and said so.
    /// </summary>
    private static readonly IReadOnlyList<string> Suffixes =
    [
        string.Empty,
        " tab",
    ];

    /// <summary>Moves the panel if the phrase named somewhere it could go, and says what happened.</summary>
    public static string? Apply(string spoken, PanelNavigator nav)
    {
        var input = Normalise(spoken);

        if (input.Length == 0)
        {
            return null;
        }

        if (Back.Any(phrase => input == phrase))
        {
            return nav.Back() ? $"Back to {nav.Trail[^1].Word}." : null;
        }

        // Help for wherever they are standing, before any of the destinations below: it is about the page
        // they are on rather than a page they want to be on.
        if (Help.Any(phrase => input == phrase))
        {
            return HelpLevel.Open(nav) ? "Help." : null;
        }

        // A crumb of the trail, which is what makes the breadcrumb sayable.
        for (var index = nav.Trail.Count - 2; index >= 0; index--)
        {
            if (Named(input, nav.Trail[index]) && nav.JumpTo(index))
            {
                return $"Back to {nav.Trail[^1].Word}.";
            }
        }

        // A mode of the tab showing.
        if (nav.AtRoot)
        {
            foreach (var root in nav.Roots(nav.Tab))
            {
                if (Named(input, root) && nav.SelectRoot(root.Key))
                {
                    return $"{root.Word}.";
                }
            }
        }

        foreach (var tab in Enum.GetValues<PanelTab>())
        {
            if (!Named(input, tab.ToString()) || !nav.Has(tab))
            {
                continue;
            }

            // Already there, and the phrase still means something: pressing the tab you are on returns to its
            // root, so saying its name does too.
            if (tab == nav.Tab)
            {
                return nav.ToRoot() ? $"Back to {nav.Root.Word}." : null;
            }

            return nav.Select(tab) ? $"{tab}." : null;
        }

        return null;
    }

    /// <summary>
    /// Whether the phrase names this destination — the word itself, or one of the openers followed by
    /// it, optionally followed by "tab", and nothing else.
    /// </summary>
    private static bool Named(string input, NavCrumb crumb) =>
        Named(input, crumb.Word) || crumb.Spoken.Any(alias => Named(input, alias));

    private static bool Named(string input, string word)
    {
        var wanted = Normalise(word);

        return wanted.Length > 0
               && Openers.Any(opener => Suffixes.Any(suffix => input == opener + wanted + suffix));
    }

    /// <summary>Lower case, trimmed, and without the punctuation a transcriber adds.</summary>
    private static string Normalise(string input) =>
        input.Trim().Trim('.', '!', '?', ',').Trim().ToLowerInvariant();
}
