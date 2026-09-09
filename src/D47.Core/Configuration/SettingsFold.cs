using D47.Core.Capabilities;

namespace D47.Core.Configuration;

/// <summary>Which settings rows the calm page shows (#60).</summary>
public static class SettingsFold
{
    /// <summary>Whether this row is hidden right now.</summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="settings">
    /// What the Commander has actually chosen, for the third override — a row they changed is never
    /// folded.
    /// </param>
    /// <param name="changed">
    /// Whether that row differs from a fresh install's, asked of the service rather than worked out
    /// here: <c>SettingsService.IsChanged</c> owns that comparison and a second copy of it would
    /// eventually disagree.
    /// </param>
    /// <param name="showEverything">
    /// The Commander's own answer, which outranks all of it.
    /// </param>
    public static bool IsFolded(SettingRow row, D47Settings settings, bool changed, bool showEverything)
    {
        if (showEverything || !row.Advanced || row.PageTop)
        {
            return false;
        }

        // A secret has no default and no value to fall back to, so a folded one is a row that silently does
        // nothing — and a Commander who cannot see the key box cannot work out why nothing speaks.
        if (row.Kind == SettingKind.Secret)
        {
            return false;
        }

        // There was a third rule here and it was too wide.

        // The fold's promise is "you are not missing anything", and a row the Commander changed is by
        // definition something they did.
        _ = settings;

        return !changed;
    }

    /// <summary>How many rows the fold is currently hiding, for the toggle's own hint.</summary>
    public static int Folded(
        IEnumerable<SettingRow> rows,
        D47Settings settings,
        Func<SettingRow, bool> changed,
        bool showEverything) =>
        rows.Count(row => row.Applies(settings)
                          && !row.DrawnElsewhere
                          && IsFolded(row, settings, changed(row), showEverything));
}
