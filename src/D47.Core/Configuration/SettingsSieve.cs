using D47.Core.Capabilities;
using D47.Core.Interface;

namespace D47.Core.Configuration;

/// <summary>One settings row with the place, group and heading it is drawn under; −1 where it has none.</summary>
public sealed record SieveRow(SettingRow Row, int Place, int Group, string? Heading);

/// <summary>A place's title and search terms, and the index of the area holding it.</summary>
public sealed record SievePlace(string Title, IReadOnlyList<string> Terms, int Area);

public sealed record SieveGroup(string Title, string Help);

public readonly record struct RowSift(bool Applies, bool Folded, bool Shown);

/// <summary><see cref="Showing"/> counts shown rows, <see cref="Folded"/> rows the fold hides.</summary>
public readonly record struct PlaceSift(bool Exists, int Showing, int Folded);

public readonly record struct GroupSift(bool Showing, bool Changed);

/// <summary>What a query, the fold and the settings leave on the settings page.</summary>
public sealed record SettingsSift(
    string Query,
    IReadOnlyList<RowSift> Rows,
    IReadOnlyList<PlaceSift> Places,
    IReadOnlyList<GroupSift> Groups)
{
    public bool Filtering => Query.Length > 0;
}

/// <summary>Decides which settings rows, places and groups show.</summary>
public static class SettingsSieve
{
    public static SettingsSift Sift(
        string query,
        IReadOnlyList<SieveRow> rows,
        IReadOnlyList<SievePlace> places,
        IReadOnlyList<string> areaTitles,
        IReadOnlyList<SieveGroup> groups,
        D47Settings current,
        Func<string, bool> isChanged,
        bool showEverything,
        IReadOnlySet<int> revealed)
    {
        var filtering = query.Length > 0;

        var placeNamed = new bool[places.Count];

        for (var i = 0; i < places.Count; i++)
        {
            var place = places[i];

            placeNamed[i] = filtering
                            && (place.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || place.Terms.Any(term => term.Contains(query, StringComparison.OrdinalIgnoreCase))
                                || (place.Area >= 0
                                    && areaTitles[place.Area].Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        var groupNamed = new bool[groups.Count];

        for (var g = 0; g < groups.Count; g++)
        {
            groupNamed[g] = filtering
                            && (groups[g].Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                || HelpLinks.Plain(groups[g].Help).Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var rowSifts = new RowSift[rows.Count];
        var exists = new bool[places.Count];
        var showing = new int[places.Count];
        var folded = new int[places.Count];
        var groupShowing = new bool[groups.Count];
        var groupChanged = new bool[groups.Count];

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];

            // A row that does not apply is absent, not disabled.
            var applies = row.Row.Applies(current) && !row.Row.DrawnElsewhere;

            var isFolded = applies
                           && SettingsFold.IsFolded(row.Row, current, row.Row.BoundKeys.Any(isChanged), showEverything);

            var isRevealed = row.Place >= 0 && revealed.Contains(row.Place);

            var shown = applies
                        && (!isFolded || isRevealed)
                        && (Matches(row.Row, query)
                            || (row.Heading is { } heading
                                && heading.Contains(query, StringComparison.OrdinalIgnoreCase))
                            || (row.Place >= 0 && placeNamed[row.Place])
                            || (row.Group >= 0 && groupNamed[row.Group]));

            rowSifts[r] = new RowSift(applies, isFolded, shown);

            if (row.Place >= 0)
            {
                exists[row.Place] |= applies;

                if (shown)
                {
                    showing[row.Place]++;
                }

                if (isFolded)
                {
                    folded[row.Place]++;
                }
            }

            if (row.Group >= 0)
            {
                groupShowing[row.Group] |= shown;
                groupChanged[row.Group] |= applies && isChanged(row.Row.Key);
            }
        }

        var placeSifts = new PlaceSift[places.Count];

        for (var i = 0; i < places.Count; i++)
        {
            placeSifts[i] = new PlaceSift(exists[i], showing[i], folded[i]);
        }

        var groupSifts = new GroupSift[groups.Count];

        for (var g = 0; g < groups.Count; g++)
        {
            groupSifts[g] = new GroupSift(groupShowing[g], groupChanged[g]);
        }

        return new SettingsSift(query, rowSifts, placeSifts, groupSifts);
    }

    /// <summary>Whether the label, the plain help or the key contains the query; true with no query.</summary>
    public static bool Matches(SettingRow row, string query) =>
        query.Length == 0
        || row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
        || HelpLinks.Plain(row.Help).Contains(query, StringComparison.OrdinalIgnoreCase)
        || row.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
}
