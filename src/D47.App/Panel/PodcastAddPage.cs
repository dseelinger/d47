using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Podcasts;

namespace D47.App.Panel;

/// <summary>Media › Podcasts › Add a show: a directory search whose results are added by a tile, a feed address, and a built-in list of suggested shows.</summary>
public sealed class PodcastAddPage : UserControl, IPageSummary
{
    public const string PageSummary = "Find a show by name, pick one D47 suggests, or add one by its feed address.";

    public const string NameBoxName = "PodcastSearchName";

    public const string SearchName = "PodcastSearch";

    public const string ResultName = "PodcastResult";

    public const string ResultAddName = "PodcastResultAdd";

    public const string UrlBoxName = "PodcastFeedAddress";

    public const string UrlAddName = "PodcastFeedAdd";

    public const string BackName = "PodcastAddBack";

    public const string SuggestedName = "PodcastSuggested";

    public const string SuggestionName = "PodcastSuggestion";

    public const string SuggestionAddName = "PodcastSuggestionAdd";

    public const string SuggestedPreviousName = "PodcastSuggestedPrevious";

    public const string SuggestedNextName = "PodcastSuggestedNext";

    public const string SuggestedLine = "A few shows to start from. ADD follows one, as it does for a search result.";

    public const int BigSuggestionsPerPage = 4;

    public const string NotAFeed = "That address is not a podcast feed. Check it and paste it again.";

    public const string NothingMatches = "Nothing matches.";

    private const double Column = 900;

    private readonly PodcastsHost _host;
    private readonly PanelNavigator _nav;
    private readonly WaitSound? _wait;
    private readonly bool _big;
    private readonly TextBox _name;
    private readonly TextBox _url;
    private readonly Button _search = Tile(SearchName, "SEARCH", "Search");
    private readonly Button _addUrl = Tile(UrlAddName, "ADD", "Add the feed address");
    private readonly StatusLine _asking = new();
    private readonly Notice _failed = new(NoticeLevel.Error, inline: true) { IsVisible = false };
    private readonly StackPanel _results = new() { Spacing = Gaps.Tile };
    private readonly StatusLine _outcome = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _matches = Typeset.Chrome(string.Empty, 12, ThemeManager.GreyKey, vertical: VerticalAlignment.Bottom);
    private readonly Segment _groups;
    private readonly StackPanel _suggestions = new() { Spacing = Gaps.Tile };
    private readonly StatusLine _suggestedOutcome = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly HashSet<string> _adding = new(StringComparer.Ordinal);

    private IReadOnlyList<PodcastSearchResult> _found = [];
    private int _group;
    private int _first;

    public PodcastAddPage(PodcastsHost host, PanelNavigator nav, WaitSound? wait = null, bool big = false)
    {
        _host = host;
        _nav = nav;
        _wait = wait;
        _big = big;

        _name = Field("Show name", "NAME", NameBoxName, "Type a show's name");
        _url = Field("Feed address", "URL", UrlBoxName, "https://");

        _name.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = SearchAsync();
            }
        };
        _url.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Address() is not null)
            {
                e.Handled = true;
                _ = AddAddressAsync();
            }
        };
        _url.TextChanged += (_, _) =>
        {
            _addUrl.IsEnabled = Address() is not null;
            FieldMessage.Clear(_url);
        };
        _search.Click += (_, _) => _ = SearchAsync();
        _addUrl.Click += (_, _) => _ = AddAddressAsync();
        _addUrl.IsEnabled = false;

        var suggested = SuggestedPodcasts.Groups;

        _groups = new Segment
        {
            Name = SuggestedName,
            ItemsSource = [.. suggested.Select(group => group.Name)],
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        if (!_big)
        {
            _groups.Statuses = [.. suggested.Select(group => (ChoiceStatus?)new ChoiceStatus(Shows(group.Shows.Count), ChoiceTone.Grey))];
        }

        _groups.SelectionChanged += (_, _) =>
        {
            _group = Math.Clamp(_groups.SelectedIndex, 0, suggested.Count - 1);
            _first = 0;
            DrawSuggestions();
        };

        var back = Tile(BackName, "BACK", "Back");
        back.Click += (_, _) => _nav.Back();

        var body = new StackPanel { Spacing = _big ? 8 : 16, MaxWidth = Column, HorizontalAlignment = HorizontalAlignment.Left };

        body.Children.Add(RoutingKit.Section("Find a show", _matches));
        body.Children.Add(Joined(_name, _search));
        body.Children.Add(_asking);
        body.Children.Add(_failed);
        body.Children.Add(_results);
        body.Children.Add(RoutingKit.Section("Add by feed address"));

        if (!_big)
        {
            body.Children.Add(Typeset.Prose("Paste the address of a show's RSS feed. D47 adds it to your Podcast feeds setting.", 15, ThemeManager.GreyKey, wrap: true));
        }

        body.Children.Add(Joined(_url, _addUrl));
        body.Children.Add(_outcome);
        body.Children.Add(RoutingKit.Section("Suggested shows"));

        if (!_big)
        {
            body.Children.Add(Typeset.Prose(SuggestedLine, 15, ThemeManager.GreyKey, wrap: true));
        }

        body.Children.Add(_groups);
        body.Children.Add(_suggestedOutcome);
        body.Children.Add(_suggestions);
        DrawSuggestions();

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { back } };
        back.MinWidth = 120;
        DockPanel.SetDock(footer, Dock.Bottom);

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children =
            {
                footer,
                new ScrollViewer
                {
                    Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = body },
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                },
            },
        };
    }

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>Searches the directory for the words in the name box.</summary>
    internal async Task SearchAsync()
    {
        var words = (_name.Text ?? string.Empty).Trim();

        if (words.Length == 0)
        {
            return;
        }

        _failed.IsVisible = false;
        _results.Children.Clear();
        _matches.Text = string.Empty;
        PodcastSearchOutcome? outcome = null;

        try
        {
            await Waiting.While(_asking, "Retrieving results…", _wait, async () => outcome = await _host.Search(words), _search);
        }
        finally
        {
            _asking.Clear();
        }

        ShowResults(outcome!);
    }

    private void ShowResults(PodcastSearchOutcome outcome)
    {
        _results.Children.Clear();

        if (outcome.Problem is { } problem)
        {
            _found = [];
            _failed.Label = "Search failed";
            _failed.Text = problem;
            _failed.Actions.Clear();

            var retry = Tile("PodcastSearchRetry", "RETRY", "Retry the search");
            retry.Click += (_, _) => _ = SearchAsync();
            _failed.Actions.Add(retry);
            _failed.IsVisible = true;

            return;
        }

        _found = [.. outcome.Results.Take(10)];

        if (_found.Count == 0)
        {
            _results.Children.Add(Typeset.Prose(NothingMatches, 15, ThemeManager.GreyKey, margin: new Thickness(12, 8, 0, 8)));

            return;
        }

        _matches.Text = (_found.Count == 1 ? "1 SHOW MATCHES" : $"{_found.Count} SHOWS MATCH");

        for (var i = 0; i < _found.Count; i++)
        {
            _results.Children.Add(ResultRow(i + 1, _found[i]));
        }
    }

    private static string Shows(int count) => count == 1 ? "1 show" : $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} shows";

    private void DrawSuggestions()
    {
        var group = SuggestedPodcasts.Groups[_group];
        var count = group.Shows.Count;

        _suggestions.Children.Clear();

        if (!_big)
        {
            _suggestions.Children.Add(LoadoutPages.ListHead($"{group.Name} · {count.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        }

        var shown = _big ? group.Shows.Skip(_first).Take(BigSuggestionsPerPage) : group.Shows;

        foreach (var show in shown)
        {
            _suggestions.Children.Add(ResultRow(null, show, suggested: true));
        }

        if (_big)
        {
            _suggestions.Children.Add(SuggestedPager(count));
        }
    }

    private Grid SuggestedPager(int count)
    {
        var previous = Tile(SuggestedPreviousName, "◄ PREVIOUS", "Previous suggested shows");
        previous.IsEnabled = _first > 0;
        previous.Click += (_, _) =>
        {
            _first = Math.Max(0, _first - BigSuggestionsPerPage);
            DrawSuggestions();
        };

        var next = Tile(SuggestedNextName, "NEXT ►", "More suggested shows");
        next.IsEnabled = _first + BigSuggestionsPerPage < count;
        next.Click += (_, _) =>
        {
            _first += BigSuggestionsPerPage;
            DrawSuggestions();
        };

        var range = Typeset.Mono(
            $"{_first + 1}–{Math.Min(count, _first + BigSuggestionsPerPage)} of {count}",
            12,
            ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center,
            margin: new Thickness(12, 0, 0, 0));

        Grid.SetColumn(previous, 1);
        Grid.SetColumn(next, 2);

        return new Grid
        {
            ColumnSpacing = Gaps.Tile,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
            Margin = new Thickness(0, 2, 0, 0),
            Children = { range, previous, next },
        };
    }

    private Control ResultRow(int? number, PodcastSearchResult result, bool suggested = false)
    {
        var following = _host.Following(result.Feed);
        var adding = _adding.Contains(result.Feed.AbsoluteUri);
        var name = ListRow.Name(new TextBlock { Text = result.Title, TextTrimming = TextTrimming.CharacterEllipsis });

        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        if (number is { } n)
        {
            heading.Children.Add(Typeset.Mono(n.ToString(System.Globalization.CultureInfo.InvariantCulture), 13, ThemeManager.GreyKey, vertical: VerticalAlignment.Center));
        }

        heading.Children.Add(name);

        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { heading } };
        var episodes = result.EpisodeCount is { } count ? (count == 1 ? "1 episode" : $"{count} episodes") : null;

        if (!_big)
        {
            var sub = string.Join(" · ", new[] { result.Author, episodes }.Where(part => !string.IsNullOrWhiteSpace(part)));

            if (sub.Length > 0)
            {
                main.Children.Add(ListRow.Sub(new TextBlock { Text = sub, TextTrimming = TextTrimming.CharacterEllipsis }));
            }
        }

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };

        var aside = suggested ? result.Author : episodes;

        if (_big && !string.IsNullOrWhiteSpace(aside))
        {
            right.Children.Add(ListRow.Aside(new TextBlock { Text = aside, VerticalAlignment = VerticalAlignment.Center }));
        }

        if (following)
        {
            right.Children.Add(Typeset.Chrome("FOLLOWING", 13, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, horizontal: HorizontalAlignment.Center, margin: new Thickness(0, 0, 0, 0)));
            right.Children[^1].Width = 120;
        }
        else
        {
            if (adding)
            {
                right.Children.Add(new WaitingState(WaitForm.Inline) { VerticalAlignment = VerticalAlignment.Center });
            }

            var add = Tile(suggested ? SuggestionAddName : ResultAddName, "ADD", $"Add {result.Title}");
            add.MinWidth = 120;
            add.IsEnabled = !adding;
            add.Click += (_, _) => _ = AddResultAsync(result, suggested);
            right.Children.Add(add);
        }

        var row = new Border { Padding = new Thickness(12, 0, 0, 0), MinHeight = TypeScale.MinimumTarget };
        ListRow.Dress(row);
        row.Classes.Set(ListRow.DimClass, following);
        AutomationProperties.SetName(row, result.Title);
        row.Name = suggested ? SuggestionName : ResultName;
        row.Child = new DockPanel { LastChildFill = true, Children = { Docked(right, Dock.Right), main } };

        return row;
    }

    private async Task AddResultAsync(PodcastSearchResult result, bool suggested)
    {
        var outcome = suggested ? _suggestedOutcome : _outcome;

        _adding.Add(result.Feed.AbsoluteUri);
        outcome.Clear();
        Redraw();

        PodcastRefresh added;

        try
        {
            added = await _host.Add(result.Feed);
        }
        finally
        {
            _adding.Remove(result.Feed.AbsoluteUri);
        }

        Redraw();
        Said(outcome, result.Title, added);
    }

    private void Redraw()
    {
        if (_found.Count > 0)
        {
            ShowResults(new PodcastSearchOutcome(_found, null));
        }

        DrawSuggestions();
    }

    private async Task AddAddressAsync()
    {
        if (Address() is not { } feed)
        {
            return;
        }

        PodcastRefresh? added = null;

        await Waiting.While(_outcome, "Reading the feed…", _wait, async () => added = await _host.Add(feed), _addUrl);

        _outcome.Clear();
        _addUrl.IsEnabled = Address() is not null;

        if (added is { Show: { } show })
        {
            _url.Text = string.Empty;
            Said(_outcome, show.Title, added);
        }
        else
        {
            FieldMessage.ShowError(_url, added?.Problem is null or "This is not an RSS feed." ? NotAFeed : added.Problem);
        }
    }

    private static void Said(StatusLine outcome, string title, PodcastRefresh added)
    {
        if (added.Show is { } show)
        {
            outcome.Say(show.Episodes.Count == 1
                ? $"Added {show.Title}, with 1 episode."
                : $"Added {show.Title}, with {show.Episodes.Count} episodes.");
        }
        else
        {
            outcome.Fail($"{title} could not be added. {added.Problem}");
        }
    }

    /// <summary>The box's text as an http or https address, or null.</summary>
    private Uri? Address() =>
        Uri.TryCreate((_url.Text ?? string.Empty).Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;

    private static TextBox Field(string accessibleName, string prefix, string name, string placeholder)
    {
        var box = new TextBox
        {
            Name = name,
            PlaceholderText = placeholder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            InnerLeftContent = Typeset.Chrome(
                prefix + ":", TypeScale.Control, ThemeManager.AKey,
                weight: FontWeight.SemiBold, tracking: TypeScale.Control * Fonts.ChromeTracking,
                vertical: VerticalAlignment.Center, margin: new Thickness(12, 0, 0, 0)),
        };
        AutomationProperties.SetName(box, accessibleName);

        return box;
    }

    private static Grid Joined(TextBox box, Button tile)
    {
        tile.MinWidth = 120;
        Grid.SetColumn(tile, 1);

        return new Grid
        {
            ColumnSpacing = Gaps.Tile,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Children = { box, tile },
        };
    }

    private static T Docked<T>(T control, Dock dock)
        where T : Control
    {
        DockPanel.SetDock(control, dock);

        return control;
    }

    private static Button Tile(string name, string text, string accessibleName)
    {
        var tile = new Button
        {
            Name = name,
            Content = text,
            MinHeight = TypeScale.MinimumTarget,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(tile, accessibleName);

        return tile;
    }
}
