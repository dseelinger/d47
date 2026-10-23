using D47.Core.Capabilities;
using D47.Core.Configuration;

namespace D47.Core.Interface;

/// <summary>One line of the keys list: what the keys do, and the keys.</summary>
/// <param name="Keys">The keys as printed; empty for a changeable action with nothing bound.</param>
/// <param name="Joiner">The word drawn between two keys that are alternatives or the ends of a range; null draws them side by side.</param>
public sealed record KeyEntry(string Action, IReadOnlyList<string> Keys, string? Joiner = null)
{
    public bool Unbound => Keys.Count == 0;
}

/// <summary>A titled run of keys. <paramref name="Where"/> and <paramref name="Note"/> are the grey lines beside the title.</summary>
public sealed record KeyGroup(string Title, string? Where, string? Note, bool Changeable, IReadOnlyList<KeyEntry> Entries);

/// <summary>Every key the window answers to, in the order the keys dialog draws them.</summary>
public static class KeyList
{
    public const string WindowPlace = "window";

    private const string WhileElite = "Work while Elite has the foreground";

    private static KeyEntry K(string action, params string[] keys) => new(action, keys);

    private static KeyEntry Or(string action, params string[] keys) => new(action, keys, "or");

    private static KeyGroup Fixed(string title, string? where, params KeyEntry[] entries) =>
        new(title, where, null, false, entries);

    private static readonly KeyGroup GettingAround = Fixed(
        "Getting around",
        null,
        new KeyEntry("Show the first to seventh tab", ["Ctrl+1", "Ctrl+7"], "to"),
        K("Next tab", "Ctrl+Tab"),
        K("Previous tab", "Ctrl+Shift+Tab"),
        Or("Go back", "Esc", "Alt+←"),
        K("Move between the rows of a page", "← → ↑ ↓"),
        K("Help for the page you are on", "F1"),
        K("This list, when no text box has the cursor", "?"));

    private static readonly KeyGroup[] OnThePage =
    [
        Fixed(
            "On the page",
            null,
            K("Search the page you are on", "Ctrl+F"),
            K("Next and previous match, in the search box", "Enter", "Shift+Enter"),
            K("Clear the search", "Esc"),
            K("Copy the page", "Ctrl+Shift+C"),
            K("Scroll past the reading, on Transcript", "Ctrl+L"),
            K("Zoom the window in, out, back to 100%", "Ctrl+Plus", "Ctrl+Minus", "Ctrl+0")),
        Fixed(
            "In the ask box",
            null,
            K("Send", "Enter"),
            K("New line", "Shift+Enter"),
            K("Earlier and later things you asked", "↑", "↓")),
    ];

    private static readonly KeyGroup[] OnTheMaps =
    [
        Fixed(
            "Galaxy map",
            "With the map selected",
            K("Pan a quarter of the view", "← → ↑ ↓"),
            K("Zoom in and out", "+", "−"),
            K("Centre on you, 400 ly across", "Home")),
        Fixed(
            "Hull viewer",
            "With the hull selected",
            K("Turn the hull", "← → ↑ ↓"),
            new KeyEntry("Zoom in and out", ["+", "−", "PgUp", "PgDn"]),
            K("Reset the view", "Home")),
    ];

    /// <summary>
    /// The list: the fixed keys as written here, and each settings group that holds a hotkey row, read from
    /// <see cref="SettingsLayout"/>. <paramref name="boundAs"/> prints what a row's
    /// <see cref="SettingRow.BoundKeys"/> hold now, aligned by index, null where a slot holds nothing.
    /// </summary>
    public static IReadOnlyList<KeyGroup> Build(SettingsService settings, Func<SettingRow, IReadOnlyList<string?>> boundAs)
    {
        var changeable = (
            from place in SettingsLayout.Areas.SelectMany(area => area.Places)
            from placed in place.Groups
            let rows = placed.Entries
                .SelectMany(settings.RowsForEntry)
                .Where(row => row.Kind == SettingKind.Hotkey)
                .ToList()
            where rows.Count > 0
            select (Place: place, Group: new KeyGroup(
                placed.Title,
                "Settings › " + place.Title,
                rows.Any(row => row.SystemWide) ? WhileElite : null,
                true,
                [.. rows.Select(row => new KeyEntry(row.Label, [.. boundAs(row).OfType<string>()], "or"))])))
            .ToList();

        return
        [
            GettingAround,
            .. changeable.Where(c => c.Place.Id == WindowPlace).Select(c => c.Group),
            .. OnThePage,
            .. changeable.Where(c => c.Place.Id != WindowPlace).Select(c => c.Group),
            .. OnTheMaps,
        ];
    }
}
