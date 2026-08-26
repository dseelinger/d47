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

        // Anything that decides what leaves this machine. The fold's job is to be calm, and a
        // page that went calm by no longer mentioning egress would be calm about the wrong thing
        // — the Commander's own ruling, 2026-08-26, taking the recommendation on the one flagged
        // item in the proposed list.
        if (row.EgressId is not null || row.EgressFor is not null)
        {
            return false;
        }

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
        rows.Count(row => row.Applies(settings) && IsFolded(row, settings, changed(row), showEverything));
}
