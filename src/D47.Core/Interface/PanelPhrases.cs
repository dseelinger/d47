using D47.Core.Help;

namespace D47.Core.Interface;

/// <summary>
/// Moving the panel by saying so (list.md Phase 25, "Drill in, and find your way back").
/// <para>
/// <b>Back is three routes that must agree</b> — the breadcrumb, a controller button, and a
/// phrase — and this is the third. Alongside it: the tabs by name, the modes of whichever tab is
/// showing, and <b>every crumb of the trail, because each crumb is a word that can be said as
/// well as pressed</b>.
/// </para>
/// <para>
/// The model-free path, like the keyword router and for the same three reasons: it works with no
/// provider, it is exercisable in tests with no network, and it reaches a surface the tool
/// vocabulary deliberately does not. Navigation is not a tool — nothing an in-game message says
/// should move the Commander's panel — so it is matched here rather than registered as one.
/// </para>
/// <para>
/// <b>Matching is whole-phrase and precise</b>, which is the router's own rule and load-bearing
/// for the same reason: a miss costs the Commander a press, and a false positive moves the page
/// out from under them mid-sentence. "Show me the ships in Deciat" is not a request for the
/// Loadout tab.
/// </para>
/// </summary>
public static class PanelPhrases
{
    /// <summary>
    /// Asking what you are looking at (asked for 2026-08-22).
    /// <para>
    /// <b>Bare "help" is allowed here and refused by the keyword router</b>, and the difference is
    /// not an inconsistency. The router matches a keyword anywhere in a sentence, so "help me plot
    /// a route" would hijack; this matches the whole utterance and nothing else, so it cannot.
    /// </para>
    /// <para>
    /// It matters most where there is no mark to press. The corner glyph needs a ray, and the
    /// Commander this whole feature was built for is wearing a headset with their hands on a
    /// stick — so help that can only be reached by pointing at it is help they cannot reach.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The openers a destination may be named after. Bare — "checklist" on its own — counts too,
    /// which is why the list has an empty entry rather than a special case.
    /// </summary>
    private static readonly IReadOnlyList<string> Openers =
    [
        string.Empty,
        "show ",
        "show me ",
        "show the ",
        "show me the ",
        "open ",
        "open the ",
        "go to ",
        "go to the ",
        "take me to ",
        "take me to the ",
        "switch to ",
        "switch to the ",
        "select ",
        "select the ",
    ];

    /// <summary>
    /// What a destination may be called after its name. "Select the checklist tab" was the
    /// reported miss (2026-08-21): the word "tab" is how a Commander looking at the bar names
    /// what is on it, and the phrase fell through to the model, which has no tool for the
    /// panel and said so. Bare — no suffix — is the first entry for the same reason bare is the
    /// first opener.
    /// </summary>
    private static readonly IReadOnlyList<string> Suffixes =
    [
        string.Empty,
        " tab",
    ];

    /// <summary>
    /// Moves the panel if the phrase named somewhere it could go, and says what happened.
    /// <para>
    /// Null when nothing matched, which is the common case and the one that must be cheap: every
    /// input passes through here on its way to a turn.
    /// </para>
    /// <para>
    /// Tried in order of specificity — back, then a crumb of the trail, then a mode of the tab
    /// showing, then a tab. A crumb before a tab because a crumb is where the Commander is and a
    /// tab is where they are not, and the trail can legitimately contain a word that is also a
    /// tab's name.
    /// </para>
    /// </summary>
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

        // Help for wherever they are standing, before any of the destinations below: it is about
        // the page they are on rather than a page they want to be on. Null when this level has no
        // band written yet, which lets the phrase fall through to the model rather than answering
        // "no" to somebody who asked for help.
        if (Help.Any(phrase => input == phrase))
        {
            return HelpLevel.Open(nav) ? "Help." : null;
        }

        // A crumb of the trail, which is what makes the breadcrumb sayable. The leaf is skipped:
        // it is where the Commander already is, and answering "you are there" to a phrase reads
        // as the phrase not having worked.
        for (var index = nav.Trail.Count - 2; index >= 0; index--)
        {
            if (Named(input, nav.Trail[index].Word) && nav.JumpTo(index))
            {
                return $"Back to {nav.Trail[^1].Word}.";
            }
        }

        // A mode of the tab showing. Only at a root, because that is the only place the control
        // is offered — a mode switch three levels down is a question about which stack you are
        // in rather than about what you are looking at.
        if (nav.AtRoot)
        {
            foreach (var root in nav.Roots(nav.Tab))
            {
                if (Named(input, root.Word) && nav.SelectRoot(root.Key))
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

            // Already there, and the phrase still means something: pressing the tab you are on
            // returns to its root, so saying its name does too.
            if (tab == nav.Tab)
            {
                return nav.ToRoot() ? $"Back to {nav.Root.Word}." : null;
            }

            return nav.Select(tab) ? $"{tab}." : null;
        }

        return null;
    }

    /// <summary>
    /// Whether the phrase names this destination — the word itself, or one of the openers
    /// followed by it, optionally followed by "tab", and nothing else.
    /// </summary>
    private static bool Named(string input, string word)
    {
        var wanted = Normalise(word);

        return wanted.Length > 0
               && Openers.Any(opener => Suffixes.Any(suffix => input == opener + wanted + suffix));
    }

    /// <summary>
    /// Lower case, trimmed, and without the punctuation a transcriber adds. A spoken "checklist."
    /// is the same request as a typed "checklist".
    /// </summary>
    private static string Normalise(string input) =>
        input.Trim().Trim('.', '!', '?', ',').Trim().ToLowerInvariant();
}
