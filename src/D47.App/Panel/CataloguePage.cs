using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Viewing;

namespace D47.App.Panel;

/// <summary>
/// Media › Movies › Catalogue: search the video sources, filter the results, add a title to Your list or watch it. The
/// search box, sentence and count stay on screen and only the rows scroll. The desktop draws the next page when the list
/// is scrolled to its end; the headset pages by <see cref="VideoCatalogue.PageSize"/>.
/// </summary>
public sealed class CataloguePage : UserControl, IPageSummary, IPageChrome
{
    public const string PageSummary = "Free films and footage from the sources' own catalogues.";

    public const string Placeholder = "Search the catalogue";

    public const string SearchName = "CatalogueSearch";

    public const string RowName = "CatalogueRow";

    public const string BackName = "CatalogueBack";

    public const string AddName = "CatalogueAdd";

    public const string WatchName = "CatalogueWatch";

    public const string PreviousName = "CataloguePrevious";

    public const string NextName = "CatalogueNext";

    public const string RetryName = "CatalogueRetry";

    public const string FailedName = "CatalogueFailed";

    public const string PagerName = "CataloguePager";

    public const string Searching = "Searching the catalogue…";

    public const string FailedLabel = "Could not search the catalogue";

    public const string NothingFound = "No video matches. Try other words, or other sources.";

    public const string Licensed = "Only videos their source marks public domain or Creative Commons are listed.";

    private readonly WatchHost _host;
    private readonly PanelNavigator _nav;
    private readonly WaitSound? _wait;
    private readonly bool _big;
    private readonly VideoFilterBar _filters;
    private readonly StatusLine _searching = new();
    private readonly StatusLine _retrieving = new() { Margin = new Thickness(0, 0, 14, 8) };
    private readonly Notice _failed = new(NoticeLevel.Error, inline: false) { Name = FailedName, Label = FailedLabel, IsVisible = false };
    private readonly StackPanel _list = new() { Spacing = Gaps.Tile };
    private readonly TextBox _search = new()
    {
        Name = SearchName,
        PlaceholderText = Placeholder,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = new Thickness(0, 0, 14, 12),
    };
    private readonly List<VideoTitle> _titles = [];
    private readonly ScrollViewer _scroll;
    private readonly Grid _body;
    private readonly TextBlock _range = new() { FontFamily = new Avalonia.Media.FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Small, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previous = BooksKit.Tile(PreviousName, "◄ PREVIOUS", "Previous page");
    private readonly Button _next = BooksKit.Tile(NextName, "NEXT ►", "Next page");
    private readonly Button _add = BooksKit.Tile(AddName, "ADD TO MY LIST", "Add to my list");
    private readonly Button _watch = BooksKit.Tile(WatchName, "WATCH NOW", "Watch now");
    private readonly DockPanel _pager = new() { Name = PagerName, Margin = new Thickness(0, 6, 14, 0) };

    private readonly RowPresses _presses = new();

    private VideoPage _result = VideoPage.Empty;
    private string _query = string.Empty;
    private int _page = 1;
    private int _lastAsked = 1;
    private bool _lastAppend;
    private bool _busy;
    private bool _stalled;
    private int _version;
    private bool _asked;
    private CancellationTokenSource? _running;
    private string? _chosen;

    public CataloguePage(WatchHost host, PanelNavigator nav, Control? settingsStrip = null, WaitSound? wait = null, bool big = false)
    {
        _host = host;
        _nav = nav;
        _wait = wait;
        _big = big;
        BarTool = PageChrome.ToolOf(settingsStrip);

        _filters = new VideoFilterBar(host);
        _filters.Changed += (_, _) => Ask(1);

        var retry = new Button { Name = RetryName, Content = "Retry", MinHeight = TypeScale.MinimumTarget };
        retry.Click += (_, _) => Ask(_lastAsked, _lastAppend);
        _failed.Actions.Add(retry);

        foreach (var tile in new[] { _previous, _next, _add, _watch })
        {
            tile.MinWidth = 120;
        }

        _previous.Click += (_, _) => Ask(_page - 1);
        _next.Click += (_, _) => Ask(_page + 1);

        _search.KeyDown += OnSearchKey;
        _add.Click += (_, _) => AddChosen();
        _watch.Click += (_, _) => WatchChosen();

        var nextSide = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { _previous, _next } };
        DockPanel.SetDock(nextSide, Dock.Right);
        _pager.Children.Add(nextSide);
        _pager.Children.Add(_range);

        var back = BooksKit.Tile(BackName, "BACK", "Back");
        back.MinWidth = 120;
        back.Click += (_, _) => _nav.Back();

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { back, _add, _watch },
        };
        DockPanel.SetDock(footer, Dock.Bottom);

        _filters.OpenChanged += (_, _) => FitPanel();
        _filters.Margin = new Thickness(0, 0, 14, 0);

        var panel = _filters.TakePanel();
        panel.Margin = new Thickness(0, 6, 14, 0);
        Grid.SetRow(panel, 2);

        var results = _filters.TakeResults();
        results.Margin = new Thickness(0, big ? 4 : 6, 14, big ? 4 : 6);
        Grid.SetRow(results, 3);

        _failed.Margin = new Thickness(0, 0, 14, 8);

        var above = new StackPanel { Children = { _retrieving, _failed } };
        Grid.SetRow(above, 4);
        Grid.SetRow(_filters, 1);

        var content = new StackPanel { Spacing = big ? 8 : 12, Children = { _list, _searching } };

        if (big)
        {
            content.Children.Add(_pager);
        }

        _scroll = LoadoutPages.Scrolling(content);
        Grid.SetRow(_scroll, 5);

        if (!big)
        {
            _scroll.ScrollChanged += (_, _) => CheckSoon();
        }

        _body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*"),
            ColumnDefinitions = { new ColumnDefinition(1, GridUnitType.Star) { MaxWidth = 1100 } },
            Children = { _search, _filters, panel, results, above, _scroll },
        };

        var root = new DockPanel { Margin = new Thickness(14) };

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

        root.Children.Add(footer);
        root.Children.Add(_body);

        Content = root;
        Draw();
    }

    public Control? BarTool { get; }

    public string? HelpTopic => InterfaceCapability.Id;

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The "1–25 of N" the pager reads.</summary>
    internal static string Range(int page, int shown, int total) =>
        shown == 0
            ? string.Create(CultureInfo.InvariantCulture, $"0 of {total}")
            : string.Create(CultureInfo.InvariantCulture, $"{((page - 1) * VideoCatalogue.PageSize) + 1}–{((page - 1) * VideoCatalogue.PageSize) + shown} of {total}");

    /// <summary>How a row names a title's kind.</summary>
    internal static string KindOf(VideoKind kind) => kind switch
    {
        VideoKind.FeatureFilm => "feature film",
        VideoKind.ShortFilm => "short film",
        VideoKind.Footage => "footage",
        _ => string.Empty,
    };

    /// <summary>A row's second line: where the title is from, what it is and where its captions come from, or why it cannot play.</summary>
    internal static string SubOf(VideoTitle title) =>
        title.Playable
            ? string.Join(" · ", new[] { WatchHost.SourceOf(title), KindOf(title.Kind), WatchHost.CaptionsOf(title) }.Where(part => part.Length > 0))
            : string.Join(" · ", new[] { WatchHost.SourceOf(title), title.Unplayable ?? "cannot be played here" }.Where(part => part.Length > 0));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Store.Changed += OnStoreChanged;

        var wanted = (_host.CatalogueQuery ?? string.Empty).Trim();

        if (!_asked || wanted != _query)
        {
            _query = wanted;
            _search.Text = wanted;
            Ask(1);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Store.Changed -= OnStoreChanged;
    }

    private void OnStoreChanged() => Dispatcher.UIThread.Post(DrawActions);

    private VideoTitle? Chosen => _titles.FirstOrDefault(title => Key(title) == _chosen);

    private static string Key(VideoTitle title) => $"{title.Source}.{title.Id}";

    private void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            Search();
        }
        else if (e.Key == Avalonia.Input.Key.Down && _list.Children.OfType<Button>().FirstOrDefault(row => row.IsEffectivelyEnabled && row.Focusable) is { } first)
        {
            e.Handled = true;
            first.Focus();
        }
    }

    /// <summary>Searches for what the box says, from the first page.</summary>
    internal void Search()
    {
        _query = (_search.Text ?? string.Empty).Trim();
        _host.CatalogueQuery = _query;
        Ask(1);
    }

    /// <summary>Searches for page <paramref name="page"/> off the calling thread. A search overtaken by a newer one is dropped.</summary>
    internal void Ask(int page, bool append = false)
    {
        _ = AskAsync(page, append);
    }

    /// <summary>
    /// Draws page <paramref name="page"/>, or on the desktop under <paramref name="append"/> adds it under the rows
    /// drawn, leaving them as they are when it fails.
    /// </summary>
    internal async Task AskAsync(int page, bool append = false)
    {
        _asked = true;
        _lastAsked = Math.Max(1, page);
        _lastAppend = append;
        _busy = true;
        _running?.Cancel();

        var running = _running = new CancellationTokenSource();
        var version = ++_version;
        var query = _query;
        var filters = _filters.Filters;
        var asked = _lastAsked;
        VideoPage? result = null;

        _failed.IsVisible = false;

        if (!append)
        {
            _chosen = null;
        }

        try
        {
            await Waiting.While(
                append ? _searching : _retrieving,
                Searching,
                _wait,
                async () => result = await _host.SearchAsync(query, filters, asked, running.Token).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != _version || result is null)
        {
            return;
        }

        _searching.Clear();
        _retrieving.Clear();
        _busy = false;

        if (append)
        {
            Append(result, asked);
            return;
        }

        _stalled = false;
        _page = asked;
        _result = result;
        _titles.Clear();
        _titles.AddRange(result.Titles);
        Draw();
        _scroll.Offset = default;
    }

    private void Append(VideoPage result, int asked)
    {
        var problem = result.Problem is { Length: > 0 };

        _stalled = problem && result.Titles.Count == 0;

        if (problem)
        {
            _failed.Text = result.Problem;
            _failed.Level = NoticeLevel.Warning;
            _failed.IsVisible = true;
        }

        if (_stalled)
        {
            return;
        }

        _page = asked;
        _result = result;

        var drawn = _titles.Select(Key).ToHashSet(StringComparer.Ordinal);
        var added = result.Titles.Where(title => !drawn.Contains(Key(title))).ToList();

        _titles.AddRange(added);

        foreach (var title in added)
        {
            _list.Children.Add(Row(title));
        }

        _filters.ShowDrawn(_titles.Count, _result.Total, _query);
        DrawActions();
        CheckSoon();
    }

    private void CheckSoon()
    {
        if (!_big)
        {
            Dispatcher.UIThread.Post(MoreIfAtEnd, DispatcherPriority.Background);
        }
    }

    /// <summary>Asks for the next page when the desktop list is within one row of its end and more titles match.</summary>
    private void MoreIfAtEnd()
    {
        if (_big || !_asked || _busy || _stalled || !_result.More || _titles.Count == 0 || _scroll.Viewport.Height <= 0)
        {
            return;
        }

        var row = _scroll.Extent.Height / Math.Max(1, _list.Children.Count);

        if (_scroll.Offset.Y + _scroll.Viewport.Height >= _scroll.Extent.Height - row)
        {
            Ask(_page + 1, append: true);
        }
    }

    /// <summary>In the headset, an open token panel takes the list's height and the list and its pager are hidden.</summary>
    private void FitPanel()
    {
        if (!_big)
        {
            return;
        }

        var open = _filters.PanelOpen;

        _body.RowDefinitions[2].Height = open ? GridLength.Star : GridLength.Auto;
        _body.RowDefinitions[5].Height = open ? GridLength.Auto : GridLength.Star;
        _scroll.IsVisible = !open;
    }

    private void Draw()
    {
        _list.Children.Clear();

        var failed = _result.Problem is { Length: > 0 };

        _failed.IsVisible = failed;

        if (failed)
        {
            _failed.Text = _result.Problem;
            _failed.Level = _result.Titles.Count == 0 ? NoticeLevel.Error : NoticeLevel.Warning;
        }

        if (_asked && !failed)
        {
            if (_big)
            {
                _filters.Show(_result.Total, _query);
            }
            else
            {
                _filters.Show(_titles.Count, _result.Total, _query);
            }
        }
        else
        {
            _filters.ShowNone();
        }

        if (_asked && _result.Titles.Count == 0 && !failed)
        {
            _list.Children.Add(Typeset.Prose(NothingFound, 15, ThemeManager.GreyKey, wrap: true));
        }

        foreach (var title in _result.Titles)
        {
            _list.Children.Add(Row(title));
        }

        DrawActions();
        CheckSoon();
    }

    /// <summary>Marks the chosen row, leaving the rows drawn and the scroll as they are.</summary>
    private void Redress()
    {
        foreach (var row in _list.Children.OfType<Button>())
        {
            ListRow.Dress(row, (row.Tag as string) == _chosen);
        }

        DrawActions();
    }

    private void DrawActions()
    {
        var chosen = Chosen;
        var kept = chosen is not null && _host.Store.Find(chosen.Source, chosen.Id) is { InList: true };

        _pager.IsVisible = _big && _asked && (_result.Titles.Count > 0 || _page > 1);
        _range.Text = Range(_page, _result.Titles.Count, _result.Total);
        _previous.IsEnabled = _page > 1;
        _next.IsEnabled = _result.More;
        _add.IsEnabled = chosen is not null && !kept;
        _watch.IsEnabled = chosen is { Playable: true };
    }

    private Button Row(VideoTitle title)
    {
        var main = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ListRow.Name(new TextBlock { Text = title.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }),
                ListRow.Sub(new TextBlock { Text = SubOf(title), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }),
            },
        };

        var aside = ListRow.Aside(new TextBlock
        {
            Text = title.Length is { } length ? WatchPage.Clock(length) : string.Empty,
            FontFamily = new Avalonia.Media.FontFamily(Fonts.MonoFamily),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });
        DockPanel.SetDock(aside, Dock.Right);

        var row = new Button
        {
            Name = RowName,
            Tag = Key(title),
            IsEnabled = title.Playable,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
            Content = new DockPanel { LastChildFill = true, Children = { aside, main } },
        };
        AutomationProperties.SetName(row, title.Title);
        ListRow.Dress(row, Key(title) == _chosen);
        row.Click += (_, _) =>
        {
            _chosen = Key(title);
            Redress();
        };

        if (!_big && title.Playable)
        {
            ListRow.OnDoubleClick(row, _presses, Key(title), () =>
            {
                _chosen = Key(title);
                Redress();
                WatchChosen();
            });
        }

        return row;
    }

    private void AddChosen()
    {
        if (Chosen is { } title)
        {
            _host.Store.Add(title.Source, title.Id, DateTimeOffset.UtcNow, title);
        }
    }

    private void WatchChosen()
    {
        if (Chosen is { Playable: true } title)
        {
            WatchPage.OpenPlayer(_nav, _host, title, start: true, headset: _big);
        }
    }
}
