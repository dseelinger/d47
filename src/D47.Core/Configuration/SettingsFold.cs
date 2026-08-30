using D47.Core.Capabilities;

namespace D47.Core.Configuration;

/// <summary>
/// Which settings rows the calm page shows
/// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
/// <para>
/// Asked for 2026-08-26: <i>"There are a lot of knobs for the Commander to tweak, which is great
/// for techies like me, but might overwhelm a commander that is new to AI or even Elite. This
/// should be helpful, not anxiety-inducing."</i>
/// </para>
/// <para>
/// <b>Folding is a pure display decision and must stay one.</b> Nothing here writes, clears,
/// normalises or defaults anything. A folded row keeps its value or its default, is still written
/// by every spoken phrase, and is still where a help link lands. The way that promise breaks is a
/// well-meaning tidy-on-save pass, which is why it is asserted rather than merely stated.
/// </para>
/// <para>
/// <b>The predicate lives on the row</b> (<see cref="SettingRow.Advanced"/>), not here and not in
/// the panel. This is only the three overrides that outrank it, and they exist because each names
/// a way the fold would otherwise be actively harmful rather than merely incomplete.
/// </para>
/// </summary>
public static class SettingsFold
{
    /// <summary>
    /// Whether this row is hidden right now.
    /// </summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="settings">
    /// What the Commander has actually chosen, for the third override — a row they changed is
    /// never folded.
    /// </param>
    /// <param name="changed">
    /// Whether that row differs from a fresh install's, asked of the service rather than worked
    /// out here: <c>SettingsService.IsChanged</c> owns that comparison and a second copy of it
    /// would eventually disagree.
    /// </param>
    /// <param name="showEverything">The Commander's own answer, which outranks all of it.</param>
    public static bool IsFolded(SettingRow row, D47Settings settings, bool changed, bool showEverything)
    {
        if (showEverything || !row.Advanced || row.PageTop)
        {
            return false;
        }

        // A secret has no default and no value to fall back to, so a folded one is a row that
        // silently does nothing — and a Commander who cannot see the key box cannot work out why
        // nothing speaks.
        if (row.Kind == SettingKind.Secret)
        {
            return false;
        }

        // There was a third rule here and it was too wide. It exempted anything carrying an
        // egress disclosure, on the reasoning that a page which went calm by no longer mentioning
        // egress would be calm about the wrong thing. That reasoning still holds; the rule did not
        // express it.
        //
        // Exactly two kinds of row carry a disclosure: the API key rows, which are secrets and are
        // already exempt one clause up, and the five per-slot voice provider rows. So the rule
        // reached nothing it was written for and one thing it was not — five "who speaks for X"
        // pickers on a page whose whole job is to be short. Narrowed on the Commander's
        // instruction, 2026-08-26.
        //
        // <b>What actually protects the rows that decide what leaves this machine is that they are
        // not marked Advanced</b> — llm.webSearch, the two galaxy-search rows, the two privacy
        // rows and memory.enabled. That was true while this clause stood as well; the clause was
        // never what was doing it. It is asserted by name in SeventyFiveKnobsTests rather than
        // left to be inferred from a property none of them carry.
        //
        // <b>A slot provider is not that kind of row.</b> It chooses which of several providers
        // speaks a line that is already going out; it does not decide whether anything goes.
        // Turning egress off for those slots is the provider row above them, which stays on the
        // page.

        // The fold's promise is "you are not missing anything", and a row the Commander changed is
        // by definition something they did. This also makes the whole rule self-adjusting: a new
        // Commander has changed nothing and sees the calm page, and a tinkerer sees their own work
        // — and would have the toggle on anyway.
        _ = settings;

        return !changed;
    }

    /// <summary>
    /// How many rows the fold is currently hiding, for the toggle's own hint. A fold that will not
    /// say how much it is folding reads as a secret; one that does reads as tidy.
    /// </summary>
    public static int Folded(
        IEnumerable<SettingRow> rows,
        D47Settings settings,
        Func<SettingRow, bool> changed,
        bool showEverything) =>
        rows.Count(row => row.Applies(settings)
                          && !row.DrawnElsewhere
                          && IsFolded(row, settings, changed(row), showEverything));
}
