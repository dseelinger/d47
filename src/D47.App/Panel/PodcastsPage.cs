using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Podcasts;

namespace D47.App.Panel;

/// <summary>Media › Podcasts: the shows followed, the selected show's episodes newest first, and the transport.</summary>
public sealed class PodcastsPage : UserControl, IPageSummary, IPageChrome
{
    public const string RootKey = MediaFurnishing.PodcastsRoot;

    public const string AddKey = "media-podcasts.add";

    public const string PageSummary = "The shows you follow and their episodes, newest first.";

    public const string NoShows = "No shows yet. Add one here, or ask D47 to find one.";

    public const string NothingPlaying = "Nothing playing. Press an episode to start it.";

    public const string SeekingUnavailable = "Seeking unavailable for this episode";

    public const string ShowName = "PodcastShow";

    public const string AddName = "PodcastAdd";

    public const string RemoveName = "PodcastRemove";

    public const string RemoveConfirmName = "PodcastRemoveConfirm";

    public const string RemoveKeepName = "PodcastRemoveKeep";

    public const string EpisodeName = "PodcastEpisode";

    public const string PlayName = "PodcastPlay";

    public const string BackName = "PodcastBack";

    public const string ForwardName = "PodcastForward";

    public const string NextName = "PodcastNext";

    public const string SeekName = "PodcastSeek";

    public const string PreviousPageName = "PodcastPreviousPage";

    public const string NextPageName = "PodcastNextPage";

    public const string RetryName = "PodcastRetry";

    public const string ContinuousName = "PodcastContinuous";

    public const string OrderName = "PodcastOrder";

    public const string KeptOrderName = "PodcastKeptOrder";

    public const string ContinuousLabel = "Play another episode when one ends";

    public static readonly IReadOnlyList<string> OrderLabels = ["Oldest first", "Newest first"];

    private const double WindowOrderWidth = 320;

    private const double BigOrderWidth = 240;

    /// <summary>The episodes the big panel lists at a time.</summary>
    public const int BigEpisodesPerPage = 5;

    private const double WindowList = 340;

    private const double BigList = 300;

    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(1);

    private readonly PodcastsHost _host;
    private readonly PanelNavigator _nav;
    private readonly WaitSound? _wait;
    private readonly bool _big;
    private readonly RowPresses _presses = new();
    private readonly ContentControl _shows = new();
    private readonly StackPanel _episodes = new() { Spacing = 12 };
    private readonly StatusLine _line = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly Notice _failed = new(NoticeLevel.Error, inline: true) { IsVisible = false, Margin = new Thickness(0, 0, 0, 10) };
    private readonly DispatcherTimer _poll = new() { Interval = PollEvery };

    private readonly TextBlock _title = Typeset.Prose(string.Empty, 15, ThemeManager.WhiteKey, trimming: Avalonia.Media.TextTrimming.CharacterEllipsis, vertical: VerticalAlignment.Center);
    private readonly TextBlock _from = Typeset.Chrome(string.Empty, 12, ThemeManager.AKey, vertical: VerticalAlignment.Center);
    private readonly TextBlock _elapsed = Typeset.Mono("0:00", 12, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center);
    private readonly TextBlock _length = Typeset.Mono("0:00", 12, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center, align: Avalonia.Media.TextAlignment.Right);
    private readonly TextBlock _unavailable = Typeset.Chrome(string.Empty, 11, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);
    private readonly TextBlock _idle = Typeset.Prose(NothingPlaying, 15, ThemeManager.GreyKey, wrap: true);
    private readonly SeekBar _bar = new() { Name = SeekName };
    private readonly Button _back = Tile(BackName, "◄ BACK 30 S", "Back 30 seconds");
    private readonly Button _play = Tile(PlayName, "PLAY", "Play or pause");
    private readonly Button _forward = Tile(ForwardName, "FORWARD 30 S ►", "Forward 30 seconds");
    private readonly Button _next = Tile(NextName, "NEXT EPISODE ►", "Next episode");

    private bool _removing;
    private int _first;
    private string? _paged;
    private TimeSpan? _known;
    private TextBlock? _nowAside;

    public PodcastsPage(PodcastsHost host, PanelNavigator nav, Control? settingsStrip = null, WaitSound? wait = null, bool big = false)
    {
        _host = host;
        _nav = nav;
        _wait = wait;
        _big = big;
        BarTool = PageChrome.ToolOf(settingsStrip);

        _bar.Dragged += fraction =>
        {
            if (_known is { } length)
            {
                _elapsed.Text = Clock(length * fraction);
            }
        };
        _bar.Released += fraction =>
        {
            if (_known is { } length)
            {
                Report(_host.Player.Seek(length * fraction));
            }

            Poll();
        };
        _back.Click += (_, _) => Report(_host.Player.SkipBack());
        _forward.Click += (_, _) => Report(_host.Player.SkipForward());
        _next.Click += async (_, _) => Report(await _host.Player.Next());
        _play.Click += (_, _) => Report(_host.Player.State is { Playing: true, PausedByCommander: false } ? _host.Player.Pause() : _host.Player.Resume());

        var root = new DockPanel { Margin = new Thickness(14) };

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

        DockPanel.SetDock(_failed, Dock.Top);
        DockPanel.SetDock(_line, Dock.Top);
        root.Children.Add(_failed);
        root.Children.Add(_line);

        var transport = Transport();
        DockPanel.SetDock(transport, Dock.Bottom);
        root.Children.Add(transport);

        DockPanel.SetDock(_shows, Dock.Left);
        root.Children.Add(_shows);
        root.Children.Add(new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = _episodes },
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = root;
        Draw();
    }

    public Control? BarTool { get; }

    public string? HelpTopic => PodcastCapability.Id;

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Changed += OnChanged;
        _host.Player.Changed += OnPlayerChanged;
        _poll.Tick += OnPoll;
        _poll.Start();
        Draw();
        _ = RefreshAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Changed -= OnChanged;
        _host.Player.Changed -= OnPlayerChanged;
        _poll.Tick -= OnPoll;
        _poll.Stop();
    }

    /// <summary>Fetches every feed with the wait mark shown, then a notice for each feed that could not be read.</summary>
    internal async Task RefreshAsync()
    {
        var feeds = _host.FeedCount;

        if (feeds == 0)
        {
            return;
        }

        _failed.IsVisible = false;
        IReadOnlyList<PodcastRefresh> results = [];

        await Waiting.While(
            _line,
            feeds == 1 ? "Refreshing 1 feed…" : $"Refreshing {feeds} feeds…",
            _wait,
            async () => results = await _host.Refresh());

        _line.Clear();
        ShowProblems(results);
        Draw();
    }

    private void ShowProblems(IReadOnlyList<PodcastRefresh> results)
    {
        var problems = results.Where(result => result.Problem is not null).ToList();

        if (problems.Count == 0)
        {
            _failed.IsVisible = false;
            return;
        }

        _failed.Label = problems.Count == 1 ? "1 feed not refreshed" : $"{problems.Count} feeds not refreshed";
        _failed.Text = string.Join(" ", problems.Select(problem => problem.Problem));
        _failed.Detail = problems.Count == 1 ? problems[0].Feed.Host : null;
        _failed.Actions.Clear();

        var retry = new Button { Name = RetryName, Content = "RETRY", MinHeight = TypeScale.MinimumTarget };
        retry.Click += (_, _) => _ = RefreshAsync();
        _failed.Actions.Add(retry);
        _failed.IsVisible = true;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Draw);

    private void OnPlayerChanged(PodcastPlayerState? state) => Dispatcher.UIThread.Post(Draw);

    private void OnPoll(object? sender, EventArgs e) => Poll();

    private CachedShow? Chosen()
    {
        var shows = _host.Shows;

        return shows.FirstOrDefault(show => string.Equals(show.Feed.AbsoluteUri, _host.Selected, StringComparison.Ordinal))
            ?? shows.FirstOrDefault();
    }

    private void Draw()
    {
        var chosen = Chosen();

        if (chosen is not null && !string.Equals(_host.Selected, chosen.Feed.AbsoluteUri, StringComparison.Ordinal))
        {
            _host.Selected = chosen.Feed.AbsoluteUri;
        }

        if (!string.Equals(_paged, chosen?.Feed.AbsoluteUri, StringComparison.Ordinal))
        {
            _paged = chosen?.Feed.AbsoluteUri;
            _first = 0;
            _removing = false;
        }

        _shows.Content = ShowList(chosen);
        DrawEpisodes(chosen);
        Poll();
    }

    // ----- the shows

    private ScrollViewer ShowList(CachedShow? chosen)
    {
        var shows = _host.Shows;
        var listening = _host.Player.Listening;
        var column = new StackPanel { Spacing = Gaps.Tile, Width = _big ? BigList : WindowList, Margin = new Thickness(0, 0, 28, 0) };

        AutomationProperties.SetName(column, "Shows");
        column.Children.Add(ListRow.Head($"Shows · {shows.Count}"));

        foreach (var show in shows)
        {
            var lines = PodcastBrowse.Episodes(show.Feed, show.Show, listening);
            var fresh = PodcastBrowse.NewCount(lines);
            var main = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { ListRow.Name(new TextBlock { Text = show.Show.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }) },
            };
            var dock = new DockPanel { LastChildFill = true };

            if (_big)
            {
                if (fresh > 0)
                {
                    var aside = ListRow.Aside(new TextBlock { Text = $"{fresh} new", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                    DockPanel.SetDock(aside, Dock.Right);
                    dock.Children.Add(aside);
                }
            }
            else
            {
                main.Children.Add(ListRow.Sub(new TextBlock
                {
                    Text = $"{(fresh == 0 ? "Nothing new" : $"{fresh} new")} · {Plural(lines.Count, "episode")}",
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                }));
            }

            dock.Children.Add(main);

            var row = new Button
            {
                Name = ShowName,
                Tag = show.Feed.AbsoluteUri,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = TypeScale.MinimumTarget,
                Content = dock,
            };
            AutomationProperties.SetName(row, show.Show.Title);
            ListRow.Dress(row, ReferenceEquals(show, chosen));
            row.Click += (_, _) =>
            {
                _host.Selected = show.Feed.AbsoluteUri;
                Draw();
            };

            if (!_big)
            {
                ListRow.OnDoubleClick(row, _presses, show.Feed.AbsoluteUri, () =>
                {
                    _host.Selected = show.Feed.AbsoluteUri;
                    Draw();
                    _ = PlayShowAsync(show);
                });
            }

            column.Children.Add(row);
        }

        if (shows.Count == 0)
        {
            column.Children.Add(Typeset.Prose(NoShows, 15, ThemeManager.GreyKey, wrap: true, margin: new Thickness(0, 6, 0, 6)));
        }

        column.Children.Add(new Border { Height = 8 });

        var add = Tile(AddName, "ADD A SHOW", "Add a show");
        add.Click += (_, _) => _nav.Drill(AddCrumb);
        column.Children.Add(add);

        if (_big && chosen is not null)
        {
            column.Children.Add(Removal(chosen, wide: false));
        }

        return new ScrollViewer
        {
            Content = column,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>The crumb of the Add a show page.</summary>
    public static NavCrumb AddCrumb { get; } = new(AddKey, "Add a show") { Help = PodcastCapability.Id, Whole = true };

    /// <summary>The remove tile, or the question that replaces it, in a row when <paramref name="wide"/> and stacked otherwise.</summary>
    private Control Removal(CachedShow show, bool wide)
    {
        if (!_removing)
        {
            var remove = Tile(RemoveName, "REMOVE THIS SHOW", "Remove this show");
            remove.Classes.Add("destructive");
            remove.Click += (_, _) =>
            {
                _removing = true;
                Draw();
            };

            return remove;
        }

        var confirm = Tile(RemoveConfirmName, "REMOVE", "Remove");
        confirm.Classes.Add("destructive");
        confirm.Click += (_, _) =>
        {
            _removing = false;
            _host.Remove(show.Feed);
        };

        var keep = Tile(RemoveKeepName, "KEEP", "Keep");
        keep.Click += (_, _) =>
        {
            _removing = false;
            Draw();
        };

        var question = Typeset.Prose(
            $"Remove {show.Show.Title} and forget where you are in its episodes?",
            15,
            ThemeManager.WhiteKey,
            wrap: true,
            vertical: VerticalAlignment.Center,
            margin: wide ? new Thickness(0, 0, 14, 0) : new Thickness(0, 6, 0, 6));

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { confirm, keep } };

        if (wide)
        {
            return new StackPanel { Orientation = Orientation.Horizontal, Children = { question, tiles } };
        }

        confirm.MinWidth = 100;
        keep.MinWidth = 100;

        return new StackPanel { Children = { question, tiles } };
    }

    // ----- the episodes

    private void DrawEpisodes(CachedShow? chosen)
    {
        _episodes.Children.Clear();
        _nowAside = null;

        if (chosen is null)
        {
            return;
        }

        var lines = PodcastBrowse.Episodes(chosen.Feed, chosen.Show, _host.Player.Listening);
        var state = _host.Player.State;
        var playing = state is not null && state.Feed.AbsoluteUri == chosen.Feed.AbsoluteUri ? state.Episode.Id : null;

        if (_big)
        {
            _first = Math.Clamp(_first, 0, Math.Max(0, (lines.Count - 1) / BigEpisodesPerPage * BigEpisodesPerPage));
        }

        IEnumerable<PodcastEpisodeLine> shown = _big ? lines.Skip(_first).Take(BigEpisodesPerPage) : lines;
        var rows = new StackPanel { Spacing = Gaps.Tile };

        if (_big)
        {
            _episodes.Children.Add(ContinuousRow(chosen));
            rows.Children.Add(ListRow.Head($"{chosen.Show.Title} · {Plural(lines.Count, "episode")}"));
        }
        else
        {
            _episodes.Children.Add(RoutingKit.Section(
                chosen.Show.Title,
                Typeset.Chrome(Plural(lines.Count, "episode").ToUpperInvariant(), 12, ThemeManager.GreyKey, vertical: VerticalAlignment.Bottom)));

            _episodes.Children.Add(ContinuousRow(chosen));

            var hint = Typeset.Prose("Press an episode to play it from where you left off.", 15, ThemeManager.GreyKey, wrap: true, vertical: VerticalAlignment.Center);
            var removal = Removal(chosen, wide: true);
            removal.HorizontalAlignment = HorizontalAlignment.Right;

            _episodes.Children.Add(new DockPanel
            {
                LastChildFill = true,
                Children = { Docked(removal, Dock.Right), hint },
            });
        }

        foreach (var line in shown)
        {
            rows.Children.Add(EpisodeRow(chosen, line, string.Equals(line.Episode.Id, playing, StringComparison.Ordinal), state));
        }

        _episodes.Children.Add(rows);

        if (_big)
        {
            _episodes.Children.Add(Pager(lines.Count));
        }
    }

    /// <summary>The selected show's continuous play: a checkbox, and the order, which is disabled and named below while it is off.</summary>
    private StackPanel ContinuousRow(CachedShow show)
    {
        var play = _host.PlayFor(show.Feed);
        var (box, _) = LabeledCheckBox.Build(ContinuousLabel);

        box.Name = ContinuousName;
        box.IsChecked = play.Continuous;
        box.MinHeight = TypeScale.MinimumTarget;
        box.IsCheckedChanged += (_, _) => _host.SetPlay(show.Feed, box.IsChecked == true, null);

        var order = new Segment
        {
            Name = OrderName,
            ItemsSource = OrderLabels,
            SelectedIndex = play.Order == PodcastOrder.NewestFirst ? 1 : 0,
            Width = _big ? BigOrderWidth : WindowOrderWidth,
            IsEnabled = play.Continuous,
        };

        AutomationProperties.SetName(order, "Order");
        order.SelectionChanged += (_, _) => _host.SetPlay(show.Feed, null, order.SelectedIndex == 1 ? PodcastOrder.NewestFirst : PodcastOrder.OldestFirst);
        DockPanel.SetDock(order, Dock.Right);

        var row = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 12) };

        row.Children.Add(new DockPanel { LastChildFill = true, Children = { order, box } });

        if (!play.Continuous)
        {
            var kept = Typeset.Chrome(
                $"Order kept for when it is on: {(play.Order == PodcastOrder.NewestFirst ? "newest" : "oldest")} first",
                11,
                ThemeManager.GreyKey,
                align: Avalonia.Media.TextAlignment.Right);

            kept.Name = KeptOrderName;
            kept.HorizontalAlignment = HorizontalAlignment.Right;
            row.Children.Add(kept);
        }

        return row;
    }

    private Button EpisodeRow(CachedShow show, PodcastEpisodeLine line, bool now, PodcastPlayerState? state)
    {
        var finished = line.Mark == PodcastEpisodeMark.Finished && !now;
        var ink = now ? ThemeManager.CyanKey : finished ? ThemeManager.GreyKey : null;
        var name = ListRow.Name(new TextBlock { Text = line.Episode.Title, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        var main = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        Ink(name, ink);

        if (!_big)
        {
            var sub = ListRow.Sub(new TextBlock
            {
                Inlines = [Fonts.Mono($"{line.Episode.Published.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} · {Clock(line.Episode.Duration)}")],
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            });

            Ink(sub, ink);
            main.Children.Add(sub);
        }

        var dock = new DockPanel { LastChildFill = true };

        if (Aside(line, now, state) is { } aside)
        {
            dock.Children.Add(Docked(aside, Dock.Right));
        }

        dock.Children.Add(main);

        var row = new Button
        {
            Name = EpisodeName,
            Tag = line.Episode.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
            Content = dock,
        };
        AutomationProperties.SetName(row, line.Episode.Title);
        ListRow.Dress(row);
        row.Click += (_, _) => _ = PlayAsync(show, line.Episode);

        return row;
    }

    private static void Ink(TextBlock text, string? key)
    {
        if (key is not null)
        {
            ThemeInk.Bind(text, TextBlock.ForegroundProperty, key);
        }
    }

    private Control? Aside(PodcastEpisodeLine line, bool now, PodcastPlayerState? state)
    {
        if (now && state is not null)
        {
            var at = Typeset.Mono(Reading(state.Position, state.Length), 12, ThemeManager.CyanKey, vertical: VerticalAlignment.Center);
            _nowAside = at;

            return Pair(Typeset.Chrome(state.PausedByCommander ? "PAUSED" : "NOW PLAYING", 12, ThemeManager.CyanKey, vertical: VerticalAlignment.Center), at);
        }

        return line.Mark switch
        {
            PodcastEpisodeMark.New => Typeset.Chrome("NEW", 12, ThemeManager.AKey, vertical: VerticalAlignment.Center),
            PodcastEpisodeMark.Finished => Typeset.Chrome("FINISHED", 12, ThemeManager.GreyKey, vertical: VerticalAlignment.Center),
            PodcastEpisodeMark.InProgress => Pair(
                Typeset.Chrome("IN PROGRESS", 12, ThemeManager.AKey, vertical: VerticalAlignment.Center),
                Typeset.Mono(Reading(line.Position ?? TimeSpan.Zero, line.Episode.Duration), 12, ThemeManager.AKey, vertical: VerticalAlignment.Center)),
            _ => null,
        };
    }

    /// <summary>The position, with the length after it on the window.</summary>
    private string Reading(TimeSpan position, TimeSpan? length) => _big ? Clock(position) : $"{Clock(position)} / {Clock(length)}";

    private static StackPanel Pair(Control first, Control second) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        Margin = new Thickness(10, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Children = { first, second },
    };

    private Grid Pager(int count)
    {
        var previous = Tile(PreviousPageName, "◄ PREVIOUS", "Previous episodes");
        previous.IsEnabled = _first > 0;
        previous.Click += (_, _) =>
        {
            _first = Math.Max(0, _first - BigEpisodesPerPage);
            Draw();
        };

        var next = Tile(NextPageName, "NEXT ►", "More episodes");
        next.IsEnabled = _first + BigEpisodesPerPage < count;
        next.Click += (_, _) =>
        {
            _first += BigEpisodesPerPage;
            Draw();
        };

        var range = Typeset.Mono(
            count == 0 ? "0" : $"{_first + 1}–{Math.Min(count, _first + BigEpisodesPerPage)} of {count}",
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

    private async Task PlayShowAsync(CachedShow show)
    {
        Report(await _host.PlayShow(show.Feed));
        Draw();
    }

    private async Task PlayAsync(CachedShow show, PodcastEpisode episode)
    {
        var said = await _host.Player.Play(show.Feed, show.Show, episode);

        Report(said);
        Draw();
    }

    // ----- the transport

    private Border Transport()
    {
        var head = new DockPanel { LastChildFill = true };

        head.Children.Add(Docked(Typeset.Chrome("NOW PLAYING", 12, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, margin: new Thickness(0, 0, 14, 0)), Dock.Left));
        head.Children.Add(Docked(_from, Dock.Right));
        _from.Margin = new Thickness(14, 0, 0, 0);

        if (_big)
        {
            head.Children.Add(Docked(_unavailable, Dock.Right));
            _unavailable.Margin = new Thickness(14, 0, 0, 0);
        }

        head.Children.Add(_title);

        var seek = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            [
                new ColumnDefinition(new GridLength(_big ? 48 : 56)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(_big ? 48 : 56)),
            ],
        };

        Grid.SetColumn(_bar, 1);
        Grid.SetColumn(_length, 2);
        seek.Children.Add(_elapsed);
        seek.Children.Add(_bar);
        seek.Children.Add(_length);

        Control below;

        if (_big)
        {
            seek.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var tiles = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Gaps.Tile,
                Margin = new Thickness(12, 0, 0, 0),
                Children = { _back, _play, _forward, _next },
            };

            Grid.SetColumn(tiles, 3);
            seek.Children.Add(tiles);
            below = _idle;
        }
        else
        {
            foreach (var tile in new[] { _back, _play, _forward, _next })
            {
                tile.MinWidth = 150;
            }

            below = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _back, _play, _forward, _next } };

            if (_idle.Parent is null)
            {
                ((WrapPanel)below).Children.Add(_idle);
                _idle.Margin = new Thickness(14, 0, 0, 0);
                _idle.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        var frame = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 12, 0, 0),
            Margin = new Thickness(0, 12, 0, 0),
            Child = new StackPanel { Spacing = _big ? 6 : 10, Children = { head, seek, below } },
        };

        ThemeInk.Bind(frame, Border.BorderBrushProperty, ThemeManager.AKey);

        return frame;
    }

    /// <summary>Reads the episode in the slot and redraws the title, the seek bar, the times and the tiles.</summary>
    internal void Poll()
    {
        var state = _host.Player.State;
        var live = state is not null;
        var canSeek = live && !state!.FromStartOnly;
        var ink = live ? ThemeManager.WhiteKey : ThemeManager.GreyKey;

        _title.Text = state?.Episode.Title ?? string.Empty;
        _from.Text = state?.Show.ToUpperInvariant() ?? string.Empty;
        _known = state?.Length;
        _length.Text = live ? Clock(state!.Length) : "0:00";
        ThemeInk.Bind(_length, TextBlock.ForegroundProperty, ink);
        ThemeInk.Bind(_elapsed, TextBlock.ForegroundProperty, ink);
        _unavailable.Text = live && !canSeek ? SeekingUnavailable.ToUpperInvariant() : string.Empty;
        _bar.Active = canSeek && state!.Length is { } length && length > TimeSpan.Zero;
        _back.IsEnabled = canSeek;
        _forward.IsEnabled = canSeek;
        _play.IsEnabled = live;
        _next.IsEnabled = live;
        _play.Content = state is { Playing: true, PausedByCommander: false } ? "PAUSE" : "PLAY";
        _idle.IsVisible = !live;

        if (_bar.IsDragging)
        {
            return;
        }

        _elapsed.Text = live ? Clock(state!.Position) : "0:00";
        _bar.Position = state?.Length is { } total && total > TimeSpan.Zero ? state.Position / total : 0;

        if (_nowAside is not null && state is not null)
        {
            _nowAside.Text = Reading(state.Position, state.Length);
        }
    }

    /// <summary>Says what a transport action answered when it did nothing, or when it ended the episode.</summary>
    private void Report(string sentence)
    {
        if (PodcastPlayer.Refused(sentence) || (_host.Player.State is null && !string.IsNullOrWhiteSpace(sentence)))
        {
            _line.Say(sentence);
        }
        else
        {
            _line.Clear();
        }

        Poll();
    }

    // ----- small helpers

    /// <summary>A time as <c>m:ss</c>, or <c>h:mm:ss</c> from an hour.</summary>
    internal static string Clock(TimeSpan? time) =>
        time is not { } t
            ? AudioPlayerReadout.UnknownClock
            : t.TotalHours >= 1
                ? t.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : $"{(int)Math.Max(0, t.TotalMinutes)}:{Math.Max(0, t.Seconds):00}";

    private static string Plural(int count, string word) => count == 1 ? $"1 {word}" : $"{count} {word}s";

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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(tile, accessibleName);

        return tile;
    }
}
