using Avalonia.Controls;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the Search pages' HISTORY tile opens: the Commander's past searches in the full-page picker.</summary>
public static class SearchHistoryPicker
{
    public const string Key = "search.history";

    public const string Word = "History";

    public const string Help = "Your last 25 searches, newest first.";

    public const string OpenText = "Open";

    /// <summary>The tool a Kind facet option stands for; null for All.</summary>
    private static readonly (string Label, string? Tool)[] Kinds =
    [
        ("All", null),
        ("System", "describe_system"),
        ("Systems", "search_systems"),
        ("Stations", "search_stations"),
        ("Bodies", "find_body"),
    ];

    /// <summary>The picker for <paramref name="entries"/>, which are already newest first.</summary>
    public static PickerRequest Request(IReadOnlyList<SearchHistoryEntry> entries, string? current, DateTimeOffset now)
    {
        var byId = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);

        return new PickerRequest
        {
            Prompt = Word,
            Help = Help,
            Choices = [.. entries.Select(entry => entry.Id)],
            Describe = id => byId.TryGetValue(id, out var entry)
                ? SearchWords.Describe(entry.Tool, entry.Arguments, entry.Reference)
                : id,
            Status = id => byId.TryGetValue(id, out var entry) ? new ChoiceStatus(StatusOf(entry, now), ChoiceTone.Grey) : null,
            Facet = new SettingFacet
            {
                Label = "Kind",
                Options =
                [
                    .. Kinds.Select(kind => new SettingFacetOption(
                        kind.Label,
                        kind.Tool is null
                            ? null
                            : id => byId.TryGetValue(id, out var entry) && entry.Tool == kind.Tool)),
                ],
            },
            Current = current,
            AcceptText = OpenText,
        };
    }

    /// <summary>"{Total} found · {age}" for a galaxy search, the age alone for a system.</summary>
    public static string StatusOf(SearchHistoryEntry entry, DateTimeOffset now)
    {
        var age = MissionsPage.Ago(now - entry.AskedAt);

        return Total(entry) is { } total ? $"{total} found · {age}" : age;
    }

    /// <summary>The kind of page an entry opens, or null for a system.</summary>
    public static GalaxySearchKind? KindOf(SearchHistoryEntry entry) =>
        entry.Tool switch
        {
            "search_systems" => GalaxySearchKind.Systems,
            "search_stations" => GalaxySearchKind.Stations,
            "search_outfitting" => GalaxySearchKind.Outfitting,
            "find_body" => GalaxySearchKind.Bodies,
            _ => null,
        };

    /// <summary>The answer an entry stored, as the page draws it; null for a system.</summary>
    public static GalaxySearchPosting? Posting(SearchHistoryEntry entry) =>
        KindOf(entry) is { } kind && entry.Result is { } result
            ? new GalaxySearchPosting(
                kind,
                entry.Arguments,
                entry.Reference,
                result,
                Total(entry) == 0 ? "Nothing matched this search." : string.Empty,
                entry.AskedAt)
            : null;

    /// <summary>The arguments that ask an entry's question again: <c>near</c>, if left out, is the stored reference.</summary>
    public static IReadOnlyDictionary<string, string> Rerun(IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)
            || (arguments.TryGetValue("near", out var near) && !string.IsNullOrWhiteSpace(near)))
        {
            return arguments;
        }

        return new Dictionary<string, string>(arguments, StringComparer.Ordinal) { ["near"] = reference };
    }

    private static int? Total(SearchHistoryEntry entry) =>
        entry.Result switch
        {
            GalaxySearchResult result => result.Total,
            StationSearchResult result => result.Total,
            BodySearchResult result => result.Total,
            SignalSearchResult result => result.Total,
            _ => null,
        };
}

/// <summary>The HISTORY tile a Search page puts on the title line.</summary>
public sealed class HistoryTile
{
    public const string Name = "OpenHistory";

    private readonly GalaxySearchSurface? _searches;

    public HistoryTile(GalaxySearchSurface? searches)
    {
        _searches = searches;

        Button = new Button { Name = Name, Content = "HISTORY", Height = TypeScale.MinimumTarget };
        Button.Click += (_, _) => _searches?.OpenHistory?.Invoke();
        Refresh();
    }

    public Button Button { get; }

    /// <summary>Whether the tile can be pressed: the Commander has an entry and the page can open the picker.</summary>
    public bool CanOpen => Button.IsEnabled;

    /// <summary>Enables the tile while the Commander has entries; true when that changed it.</summary>
    public bool Refresh()
    {
        var was = Button.IsEnabled;
        Button.IsEnabled = _searches is { History: { } history, OpenHistory: not null }
            && history.All().Count > 0;

        return Button.IsEnabled != was;
    }

    public void Attach()
    {
        if (_searches?.History is { } history)
        {
            history.Changed += OnChanged;
        }

        Refresh();
    }

    public void Detach()
    {
        if (_searches?.History is { } history)
        {
            history.Changed -= OnChanged;
        }
    }

    private void OnChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(() => Refresh());
}
