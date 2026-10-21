using System.Text;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Help;

namespace D47.Core.Interface;

/// <summary>What a Find result lands on.</summary>
public enum PanelFindKind
{
    Page,
    SettingsPlace,
    SettingRow,
    HelpPage,
}

/// <summary>One Find result: what it is, how it reads, where it sits, and the key that reaches it.</summary>
/// <param name="Title">A destination's word, a place's title, a row's label or an article's title.</param>
/// <param name="Path">Where it sits, such as "Asset Mgmt › Ships".</param>
/// <param name="Target">The destination key, place id, row key or capability id, by <paramref name="Kind"/>.</param>
public sealed record PanelFindResult(PanelFindKind Kind, string Title, string Path, string Target);

/// <summary>Searches the pages, settings and help the app has, computed from the sources each time (#1471).</summary>
public sealed class PanelFind(
    PanelNavigator nav, SettingsService settings, IReadOnlyDictionary<PanelTab, string> tabWords)
{
    private const string SettingsWord = "Settings";
    private const string Separator = " › ";

    /// <summary>The results for a query, best first; empty for a blank query.</summary>
    public IReadOnlyList<PanelFindResult> Search(string query)
    {
        var needle = Normalize(query);

        if (needle.Length == 0)
        {
            return [];
        }

        return
        [
            .. Candidates()
                .Select(candidate => (candidate, rank: Rank(candidate, needle)))
                .Where(scored => scored.rank is not null)
                .OrderBy(scored => scored.rank!.Value.Rank)
                .ThenBy(scored => scored.rank!.Value.Run)
                .ThenBy(scored => scored.candidate.Result.Kind)
                .ThenBy(scored => scored.candidate.Result.Title.Length)
                .ThenBy(scored => scored.candidate.Order)
                .Select(scored => scored.candidate.Result),
        ];
    }

    private sealed record Candidate(
        PanelFindResult Result, int Order, IReadOnlyList<string> Aliases, string? Help);

    private IEnumerable<Candidate> Candidates()
    {
        var order = 0;

        foreach (var destination in nav.Destinations.Where(d => nav.CanShow(d.Key)))
        {
            var tab = TabWord(destination.Tab);
            var path = destination.SubPage is null || string.Equals(destination.Root.Word, tab, StringComparison.OrdinalIgnoreCase)
                ? tab
                : tab + Separator + destination.Root.Word;

            yield return new Candidate(
                new PanelFindResult(PanelFindKind.Page, destination.Word, path, destination.Key),
                order++,
                destination.Page.Spoken,
                null);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var area in SettingsLayout.Areas)
        {
            foreach (var place in area.Places)
            {
                var groups = place.Groups
                    .Select(group => (group, rows: group.Entries
                        .SelectMany(settings.RowsForEntry)
                        .Where(row => row.Applies(settings.Current))
                        .ToList()))
                    .ToList();

                if (groups.All(g => g.rows.Count == 0))
                {
                    continue;
                }

                var placePath = SettingsWord + Separator + area.Title;

                yield return new Candidate(
                    new PanelFindResult(PanelFindKind.SettingsPlace, place.Title, placePath, place.Id), order++, place.Terms, null);

                foreach (var (group, rows) in groups)
                {
                    foreach (var row in rows.Where(row => !row.DrawnElsewhere && seen.Add(row.Key)))
                    {
                        yield return Row(row, placePath + Separator + place.Title + Separator + group.Title, order++);
                    }
                }
            }
        }

        foreach (var tab in SettingsLayout.Tabs)
        {
            foreach (var row in tab.Entries
                .SelectMany(settings.RowsForEntry)
                .Where(row => row.Applies(settings.Current) && !row.DrawnElsewhere && seen.Add(row.Key)))
            {
                yield return Row(row, tab.Title, order++);
            }
        }

        foreach (var id in HelpLibrary.Pages)
        {
            if (HelpLibrary.For(id) is { } article)
            {
                yield return new Candidate(new PanelFindResult(PanelFindKind.HelpPage, article.Title, "Help", id), order++, [], null);
            }
        }
    }

    private static Candidate Row(SettingRow row, string path, int order) =>
        new(new PanelFindResult(PanelFindKind.SettingRow, row.Label, path, row.Key), order, [], HelpLinks.Plain(row.Help));

    private string TabWord(PanelTab tab) =>
        tab == PanelTab.Settings ? SettingsWord
        : tabWords.TryGetValue(tab, out var word) && word.Length > 0 ? word
        : tab.ToString();

    /// <summary>The best rank a candidate earns, 1 to 8, and for rank 8 the length of the shortest run of title holding the letters.</summary>
    private static (int Rank, int Run)? Rank(Candidate candidate, string needle)
    {
        var title = Normalize(candidate.Result.Title);

        if (title == needle)
        {
            return (1, 0);
        }

        if (title.StartsWith(needle, StringComparison.Ordinal))
        {
            return (2, 0);
        }

        var titleWords = title.Split(' ');

        if (needle.Split(' ').All(word => titleWords.Any(t => t.StartsWith(word, StringComparison.Ordinal))))
        {
            return (3, 0);
        }

        if (title.Contains(needle, StringComparison.Ordinal))
        {
            return (4, 0);
        }

        if (candidate.Aliases.Any(alias => Normalize(alias).Contains(needle, StringComparison.Ordinal)))
        {
            return (5, 0);
        }

        if (Normalize(candidate.Result.Path).Contains(needle, StringComparison.Ordinal))
        {
            return (6, 0);
        }

        if (candidate.Help is not null && Normalize(candidate.Help).Contains(needle, StringComparison.Ordinal))
        {
            return (7, 0);
        }

        return ShortestRun(title, needle.Replace(" ", string.Empty, StringComparison.Ordinal)) is { } run
            ? (8, run)
            : null;
    }

    /// <summary>The shortest stretch of the title that holds the letters in order, the first at the start of a word.</summary>
    private static int? ShortestRun(string title, string letters)
    {
        int? best = null;

        for (var start = 0; start < title.Length; start++)
        {
            if (title[start] != letters[0] || (start > 0 && title[start - 1] != ' '))
            {
                continue;
            }

            var next = 1;
            var end = start;

            for (var at = start + 1; at < title.Length && next < letters.Length; at++)
            {
                if (title[at] == letters[next])
                {
                    next++;
                    end = at;
                }
            }

            if (next == letters.Length && (best is null || end - start + 1 < best))
            {
                best = end - start + 1;
            }
        }

        return best;
    }

    /// <summary>Lower case, with every run of anything but letters and digits as one space.</summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().TrimEnd();
    }
}
