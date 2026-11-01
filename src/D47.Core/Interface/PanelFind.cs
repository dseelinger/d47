using System.Text;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Help;
using D47.Core.Knowledge;

namespace D47.Core.Interface;

/// <summary>What a Find result lands on.</summary>
public enum PanelFindKind
{
    Page,
    SettingsPlace,
    SettingRow,
    HelpPage,
    StarSystem,
    Station,
    Carrier,
    Faction,
    Engineer,
    Bookmark,
}

/// <summary>A name to rank beside the pages: what it is, how it reads, where it sits and the key that reaches it.</summary>
public sealed record PanelFindName(
    PanelFindKind Kind, string Title, string Path, string Target, long? SystemAddress = null, double? LightYears = null);

/// <summary>One Find result: what it is, how it reads, where it sits, and the key that reaches it.</summary>
/// <param name="Title">A destination's word, a place's title, a row's label or an article's title.</param>
/// <param name="Path">Where it sits, such as "Asset Mgmt › Ships".</param>
/// <param name="Target">The destination key, place id, row key or capability id, by <paramref name="Kind"/>.</param>
/// <param name="Rank">The step it matched at, 1 to 8; lower is better. A name matches at 1 to 4 only.</param>
/// <param name="ReadOnly">A row that shows a value and has no control to change it.</param>
/// <param name="SystemAddress">The system a galaxy name sits in, when the lookup gave it.</param>
/// <param name="LightYears">A station's distance from the Commander's system, when both positions are known.</param>
public sealed record PanelFindResult(
    PanelFindKind Kind, string Title, string Path, string Target, int Rank, bool ReadOnly = false,
    long? SystemAddress = null, double? LightYears = null)
{
    /// <summary>The path and the title, such as "Asset Mgmt › Ships › Hull pictures".</summary>
    public string Where => Path + PanelFind.Separator + Title;
}

/// <summary>Searches the pages, settings and help the app has, computed from the sources each time (#1471).</summary>
public sealed class PanelFind(
    PanelNavigator nav,
    SettingsService settings,
    IReadOnlyDictionary<PanelTab, string> tabWords,
    Func<IReadOnlyList<Bookmark>>? bookmarks = null)
{
    private const string SettingsWord = "Settings";
    internal const string Separator = " › ";

    private const int NameSteps = 4;

    /// <summary>
    /// Every result for a query, best first; empty for a blank query. <paramref name="extraNames"/> are names
    /// fetched elsewhere, ranked with the local ones.
    /// </summary>
    public IReadOnlyList<PanelFindResult> Search(string query, IReadOnlyList<PanelFindName>? extraNames = null)
    {
        var needle = Normalize(query);

        if (needle.Length == 0)
        {
            return [];
        }

        var scored = Candidates()
            .Select(candidate => (candidate, rank: Rank(candidate, needle)))
            .Where(scored => scored.rank is not null)
            .OrderBy(scored => scored.rank!.Value.Rank)
            .ThenBy(scored => scored.rank!.Value.Run)
            .ThenBy(scored => scored.candidate.Result.Kind)
            .ThenBy(scored => scored.candidate.Result.ReadOnly)
            .ThenBy(scored => scored.candidate.Result.Title.Length)
            .ThenBy(scored => scored.candidate.Order)
            .Select(scored => scored.candidate.Result with { Rank = scored.rank!.Value.Rank })
            .ToList();

        var named = Names(extraNames)
            .Select(name => (name, rank: NameRank(Normalize(name.Title), needle)))
            .Where(item => item.rank is not null)
            .ToList();

        var results = new List<PanelFindResult>(scored.Count + named.Count);

        for (var step = 1; step <= 8; step++)
        {
            results.AddRange(scored.Where(result => result.Rank == step));

            if (step <= NameSteps)
            {
                results.AddRange(TakeTurns(named.Where(item => item.rank == step).Select(item => item.name), step));
            }
        }

        return results;
    }

    private static IEnumerable<PanelFindResult> TakeTurns(IEnumerable<PanelFindName> names, int step)
    {
        var kinds = names
            .GroupBy(name => name.Kind)
            .OrderBy(group => group.Key)
            .Select(group => group
                .OrderBy(name => name.Title.Length)
                .ThenBy(name => name.Title, StringComparer.OrdinalIgnoreCase)
                .ToList())
            .ToList();

        for (var turn = 0; kinds.Any(kind => turn < kind.Count); turn++)
        {
            foreach (var kind in kinds.Where(kind => turn < kind.Count))
            {
                var name = kind[turn];
                yield return new PanelFindResult(name.Kind, name.Title, name.Path, name.Target, step, false, name.SystemAddress, name.LightYears);
            }
        }
    }

    private IEnumerable<PanelFindName> Names(IReadOnlyList<PanelFindName>? extraNames)
    {
        var galaxySystems = (extraNames ?? [])
            .Where(name => name.Kind == PanelFindKind.StarSystem)
            .Select(name => name.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (settings.Current.Knowledge.GalaxySearch)
        {
            foreach (var system in SystemNameTable.Names.Where(system => !galaxySystems.Contains(system)))
            {
                yield return new PanelFindName(PanelFindKind.StarSystem, system, "Star system", system);
            }
        }

        foreach (var engineer in EngineerDirectory.All)
        {
            yield return new PanelFindName(PanelFindKind.Engineer, engineer.Name, "Engineer", engineer.Name);
        }

        foreach (var bookmark in bookmarks?.Invoke() ?? [])
        {
            yield return new PanelFindName(
                PanelFindKind.Bookmark, bookmark.Name, "Bookmark" + Separator + bookmark.System, bookmark.Name);
        }

        foreach (var name in extraNames ?? [])
        {
            yield return name;
        }
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
                new PanelFindResult(PanelFindKind.Page, destination.Word, path, destination.Key, 0),
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
                    new PanelFindResult(PanelFindKind.SettingsPlace, place.Title, placePath, place.Id, 0), order++, place.Terms, null);

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
            if (HelpLibrary.Lead(id) is { } article)
            {
                yield return new Candidate(new PanelFindResult(PanelFindKind.HelpPage, article.Title, "Help", id, 0), order++, [], null);
            }
        }
    }

    private static Candidate Row(SettingRow row, string path, int order) =>
        new(new PanelFindResult(PanelFindKind.SettingRow, row.Label, path, row.Key, 0, row.Kind == SettingKind.Info), order, [], HelpLinks.Plain(row.Help));

    private string TabWord(PanelTab tab) =>
        tab == PanelTab.Settings ? SettingsWord
        : tabWords.TryGetValue(tab, out var word) && word.Length > 0 ? word
        : tab.ToString();

    /// <summary>The best rank a candidate earns, 1 to 8, and for rank 8 the length of the shortest run of title holding the letters.</summary>
    private static (int Rank, int Run)? Rank(Candidate candidate, string needle)
    {
        var title = Normalize(candidate.Result.Title);

        if (NameRank(title, needle) is { } step)
        {
            return (step, 0);
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

    /// <summary>The step, 1 to 4, at which a normalized title matches: exact, starts with, every word starts a title word, contains.</summary>
    private static int? NameRank(string title, string needle)
    {
        if (title == needle)
        {
            return 1;
        }

        if (title.StartsWith(needle, StringComparison.Ordinal))
        {
            return 2;
        }

        var titleWords = title.Split(' ');

        if (needle.Split(' ').All(word => titleWords.Any(t => t.StartsWith(word, StringComparison.Ordinal))))
        {
            return 3;
        }

        return title.Contains(needle, StringComparison.Ordinal) ? 4 : null;
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
