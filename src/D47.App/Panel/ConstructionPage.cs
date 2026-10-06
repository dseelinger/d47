using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What Asset Mgmt › Construction reads and drives.</summary>
/// <param name="LookupsEnabled">Whether “Look things up in the galaxy” is on, which the market search needs.</param>
public sealed record ConstructionSurface(
    Func<CommanderGameState?> State,
    Func<DateTimeOffset> Now,
    CapabilityRegistry? Registry,
    SourcingBoard? Sourcing,
    Func<bool> LookupsEnabled);

/// <summary>Asset Mgmt › Construction: every site still being built, beside the selected site's needs and its last market search (#828).</summary>
public sealed class ConstructionPage : UserControl, IPageSummary
{
    public const string RootKey = "loadout.construction";

    public const string NeedsTool = "get_construction_needs";

    public const string SearchLabel = "Search markets";

    public const string Covered = "✓ COVERED";

    public const string Unknown = "UNKNOWN";

    public const string OrderOpen = "ORDER OPEN";

    public const string NoSites =
        "D47 hasn't seen a construction site in your journals. Dock at one and it appears here, with what it still needs.";

    public const string AllFinished =
        "Every construction site in your journals is finished or has failed. Dock at a new one and it appears here, with what it still needs.";

    public const string LookupsOff =
        "Looking markets up is switched off. Turn on “Look things up in the galaxy” in Settings to search.";

    /// <summary>Below this width the detail is drawn under the list.</summary>
    public const double StackBelow = 900;

    private const double ListWidth = 380;

    private const string NeedsColumns = "1.7*,*,*,*,*,112";

    private const string LotColumns = "1.7*,*,*,*";

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly ConstructionSurface _surface;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly bool _headset;

    private readonly Button _search = new() { Content = SearchLabel };
    private readonly Button _cancel = new() { Content = "Cancel", IsVisible = false };
    private readonly StatusLine _status = RoutingKit.Status();

    private CancellationTokenSource? _inFlight;
    private long? _selected;
    private bool _stacked;
    private object? _seen;

    public ConstructionPage(ConstructionSurface surface, Func<string, Task<bool>>? copy = null, bool headset = false)
    {
        _surface = surface;
        _copy = copy;
        _headset = headset;
        _stacked = headset;

        Margin = new Thickness(14);

        _search.Click += (_, _) => _ = SearchAsync();
        _cancel.Click += (_, _) => _inFlight?.Cancel();

        Draw();
    }

    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    /// <summary>The site whose detail is showing, by market id.</summary>
    public long? Selected => _selected;

    /// <summary>Redraws when the sites, the hold, the carrier, the system or the day have moved.</summary>
    public bool Tick()
    {
        if (Equals(Stamp(), _seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>Shows one site's detail.</summary>
    public void Select(long marketId)
    {
        if (_selected == marketId)
        {
            return;
        }

        _selected = marketId;

        if (_inFlight is null)
        {
            _status.Clear();
        }

        Draw();
    }

    /// <summary>The title line: how many sites are building, how many are stale, and the carrier's state.</summary>
    public static string Summarise(ColonisationSites sites, CarrierState carrier, DateTimeOffset now)
    {
        var current = sites.Current(now).Count;
        var stale = sites.NotSeenSince(now).Count;

        if (current + stale == 0)
        {
            return "No construction sites in your journals";
        }

        var parts = new List<string>
        {
            current == 1 ? "1 site building" : $"{Number(current)} sites building",
        };

        if (stale > 0)
        {
            parts.Add($"{Number(stale)} not seen lately");
        }

        if (carrier.Owned)
        {
            parts.Add(carrier.Hold.Reconciled == true ? "carrier reconciled" : "carrier not reconciled");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>When a site was seen, short: the time today, “yesterday”, or the date.</summary>
    public static string Seen(DateTimeOffset at, DateTimeOffset now)
    {
        var day = at.ToLocalTime().Date;
        var today = now.ToLocalTime().Date;

        return day == today ? Time(at)
            : day == today.AddDays(-1) ? "yesterday"
            : Day(at);
    }

    /// <summary>When a site was seen, for the Last seen cell: <c>Today 19:42</c> or <c>12 Sep 21:07</c>.</summary>
    public static string LastSeen(DateTimeOffset at, DateTimeOffset now) =>
        at.ToLocalTime().Date == now.ToLocalTime().Date
            ? $"Today {Time(at)}"
            : $"{Day(at)} {Time(at)}";

    /// <summary>The count of outstanding commodities and their tonnes: <c>10 · 41,280 t</c>.</summary>
    public static string Outstanding(ConstructionSite site) =>
        $"{Number(site.Outstanding.Count)} · {Tonnes(site.Outstanding.Sum(resource => resource.Remaining))}";

    /// <summary>The name the market search is asked for: the station, or the system where the station is not known.</summary>
    public static string SiteName(ConstructionSite site) => site.StationName ?? site.StarSystem ?? site.Where;

    /// <summary>The arguments the SEARCH MARKETS tile sends to <see cref="NeedsTool"/>.</summary>
    public static ToolArguments SearchArguments(ConstructionSite site) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site"] = SiteName(site),
            ["where_to_buy"] = "true",
        });

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_surface.Sourcing is { } board)
        {
            board.Posted += OnPosted;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_surface.Sourcing is { } board)
        {
            board.Posted -= OnPosted;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var stacked = _headset || finalSize.Width < StackBelow;

        if (stacked != _stacked)
        {
            _stacked = stacked;
            Dispatcher.UIThread.Post(Draw);
        }

        return base.ArrangeOverride(finalSize);
    }

    private void OnPosted() => Dispatcher.UIThread.Post(Draw);

    private object Stamp()
    {
        var state = _surface.State();
        var now = _surface.Now();

        return (
            state?.Colonisation,
            state?.Hold,
            state?.Carrier,
            state?.Location.StarSystem,
            _surface.LookupsEnabled(),
            now.ToLocalTime().Date,
            state is null ? 0 : state.Colonisation.Current(now).Count);
    }

    private void Draw()
    {
        _seen = Stamp();

        var state = _surface.State();
        var now = _surface.Now();
        var sites = state?.Colonisation ?? ColonisationSites.Empty;
        var carrier = state?.Carrier ?? CarrierState.None;

        var summary = Summarise(sites, carrier, now);

        if (summary != Summary)
        {
            Summary = summary;
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }

        var current = sites.Current(now);
        var stale = sites.NotSeenSince(now);

        Detach(_search);
        Detach(_cancel);
        Detach(_status);

        if (current.Count + stale.Count == 0)
        {
            Content = Empty(sites.IsKnown, carrier);
            return;
        }

        if (_selected is not { } id || !current.Concat(stale).Any(site => site.MarketId == id))
        {
            _selected = (current.Count > 0 ? current[0] : stale[0]).MarketId;
        }

        var selected = current.Concat(stale).First(site => site.MarketId == _selected);

        var list = List(current, stale, now);
        var hint = Hint($"Say: “what does {SiteName(selected)} still need”");
        hint.Margin = new Thickness(0, 14, 0, 0);
        var left = new StackPanel { Children = { list, hint } };

        var detail = Detail(state!, selected, stale.Contains(selected), now);

        Control panes;

        if (_stacked)
        {
            detail.Margin = new Thickness(0, 24, 0, 0);
            panes = new StackPanel { Children = { left, detail } };
        }
        else
        {
            var rule = new Border
            {
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(24, 0, 0, 0),
                Child = detail,
            };
            Themed(rule, Border.BorderBrushProperty, ThemeManager.Line2Key);
            Grid.SetColumn(rule, 1);

            panes = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(new GridLength(ListWidth)), new ColumnDefinition(GridLength.Star)],
                ColumnSpacing = 24,
                Children = { left, rule },
            };
        }

        Content = LoadoutPages.Scrolling(panes);
    }

    private static Control Empty(bool known, CarrierState carrier)
    {
        var hint = Hint("Say: “find a system to colonise”");
        hint.Margin = new Thickness(0, 12, 0, 0);

        var cells = DataGrid([CarrierCell(carrier)], columns: 2);
        cells.Margin = new Thickness(0, 12, 0, 0);

        return new StackPanel
        {
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                LoadoutPages.Section("Construction sites"),
                Prose(known ? AllFinished : NoSites, TypeScale.Secondary, ThemeManager.WhiteKey),
                cells,
                hint,
            },
        };
    }

    private StackPanel List(IReadOnlyList<ConstructionSite> current, IReadOnlyList<ConstructionSite> stale, DateTimeOffset now)
    {
        var list = new StackPanel();

        void Group(string head, IReadOnlyList<ConstructionSite> sites, bool old)
        {
            if (sites.Count == 0)
            {
                return;
            }

            var heading = ListRow.Head($"{head} · {Number(sites.Count)}");

            if (list.Children.Count > 0)
            {
                heading.Margin = new Thickness(0, 12, 0, 2);
            }

            list.Children.Add(heading);

            var rows = new StackPanel { Spacing = Gaps.Tile };

            foreach (var site in sites)
            {
                rows.Children.Add(Row(site, old, now));
            }

            list.Children.Add(rows);
        }

        Group("Building", current, old: false);
        Group("Not seen since", stale, old: true);

        return list;
    }

    private Button Row(ConstructionSite site, bool old, DateTimeOffset now)
    {
        var selected = site.MarketId == _selected;

        var name = ListRow.Name(new TextBlock
        {
            Text = site.StationName ?? site.StarSystem ?? "Construction site",
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var parts = new List<string>();

        if (site.StarSystem is { } system && site.StationName is not null)
        {
            parts.Add(system);
        }

        parts.Add($"{Percent(site.Progress)} built");

        if (!old)
        {
            parts.Add($"seen {Seen(site.SeenAt, now)}");
        }

        var sub = ListRow.Sub(new TextBlock
        {
            Text = string.Join(" · ", parts),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, sub } };

        var aside = ListRow.SecondaryInk(new TextBlock
        {
            Text = old ? Day(site.SeenAt) : Outstanding(site),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = ListRow.AsideSize,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
        });
        Grid.SetColumn(aside, 1);

        var button = new Button
        {
            Tag = site.MarketId,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Children = { left, aside },
            },
        };

        ListRow.Dress(button, selected);
        button.Classes.Set(ListRow.DimClass, old && !selected);

        button.Click += (_, _) => Select(site.MarketId);

        return button;
    }

    private StackPanel Detail(CommanderGameState state, ConstructionSite site, bool old, DateTimeOffset now)
    {
        var detail = new StackPanel();
        var station = site.StationName ?? site.StarSystem ?? "Construction site";

        var crumb = TitleText.Context($"Construction › {station}");
        crumb.Margin = new Thickness(0, 10, 0, 10);
        detail.Children.Add(crumb);

        var title = TitleText.Build(station, 24, TitleRank.Screen);
        title.TextWrapping = TextWrapping.Wrap;

        TextBlock? system = null;

        if (site.StarSystem is { } named && site.StationName is not null)
        {
            system = new TextBlock
            {
                Text = named,
                FontFamily = Fonts.ChromeFamily,
                FontSize = TypeScale.Tip,
                FontWeight = FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap,
            };
            Themed(system, TextBlock.ForegroundProperty, Here(named, state) ? ThemeManager.CyanKey : ThemeManager.AKey);
        }

        Control? copy = site.StarSystem is { } copied && _copy is { } clipboard ? CopyGlyph.For(copied, clipboard) : null;

        var heading = new StackPanel { Spacing = 2, Children = { title } };

        if (system is not null)
        {
            heading.Children.Add(system);
        }

        var titleRow = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 16,
            Children = { heading },
        };

        if (copy is not null)
        {
            copy.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(copy, 1);
            titleRow.Children.Add(copy);
        }

        detail.Children.Add(titleRow);

        if (old)
        {
            var days = Math.Max(0, (int)(now - site.SeenAt).TotalDays);
            var notice = new Notice(NoticeLevel.Warning)
            {
                Label = $"Not seen since {Day(site.SeenAt)}",
                Text = $"These figures are {Number(days)} day{(days == 1 ? string.Empty : "s")} old. Dock at {station} to bring "
                    + "them up to date. D47 won't answer for this site unless you name it.",
                Margin = new Thickness(0, 12, 0, 0),
            };
            detail.Children.Add(notice);
        }

        var built = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Mono(Percent(site.Progress), TypeScale.Body, ThemeManager.WhiteKey),
                Gauge.Track(Math.Clamp(site.Progress, 0, 1), Gauge.FillKey(GaugeFill.Progress)),
            },
        };

        var cells = DataGrid(
        [
            Block("Built", built),
            Block("Last seen", Mono(LastSeen(site.SeenAt, now), TypeScale.Body, old ? ThemeManager.WarnKey : ThemeManager.WhiteKey)),
            Block("Outstanding", Mono(Outstanding(site), TypeScale.Body, ThemeManager.WhiteKey)),
            CarrierCell(state.Carrier),
        ], columns: _stacked ? 2 : 4);
        cells.Margin = new Thickness(0, 14, 0, 0);
        detail.Children.Add(cells);

        var needs = ConstructionNeeds.For(site, state.Hold, state.Carrier);

        detail.Children.Add(Needs(needs, state.Carrier));
        detail.Children.Add(LastSearch(site, station));

        return detail;
    }

    private static Border CarrierCell(CarrierState carrier)
    {
        var value = !carrier.Owned
            ? Chrome("None owned", TypeScale.Small, ThemeManager.GreyKey)
            : carrier.Hold.Reconciled == true
                ? Runs(
                    ThemeManager.BlueKey,
                    [new Run("✓ RECONCILED"), .. carrier.Hold.CheckedAt is { } at ? new Inline[] { new Run(" · "), Fonts.Mono(Time(at)) } : []])
                : Runs(
                    ThemeManager.GreyKey,
                    [new Run("NOT RECONCILED"), .. carrier.Hold.CheckedAt is { } since ? new Inline[] { new Run(" SINCE "), Fonts.Mono(Day(since).ToUpperInvariant()) } : []]);

        return Block("Carrier", value);
    }

    private Control Needs(IReadOnlyList<ConstructionNeeds> needs, CarrierState carrier)
    {
        var toBuy = needs.Sum(need => need.ToBuy);
        var section = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

        section.Children.Add(RoutingKit.Section("Needs", Mono($"to buy {Tonnes(toBuy)}", TypeScale.Small, ThemeManager.AKey)));

        if (needs.Count == 0)
        {
            section.Children.Add(Prose("Nothing outstanding on the manifest.", TypeScale.Small, ThemeManager.GreyKey));
            return section;
        }

        var heads = new Grid { ColumnDefinitions = new ColumnDefinitions(NeedsColumns), ColumnSpacing = 12, Margin = new Thickness(14, 0, 14, 4) };

        foreach (var (text, column, right) in new[]
                 {
                     ("Commodity", 0, false),
                     ("Remaining", 1, true),
                     ("In hold", 2, true),
                     ("On carrier", 3, true),
                     ("To buy", 4, true),
                     ("Order", 5, false),
                 })
        {
            var head = MaterialsPage.Chrome(text, ThemeManager.GreyKey);
            head.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(head, column);
            heads.Children.Add(head);
        }

        section.Children.Add(heads);

        var rows = new StackPanel { Spacing = Gaps.Tile };

        foreach (var need in needs)
        {
            rows.Children.Add(NeedRow(need, carrier.Owned));
        }

        section.Children.Add(rows);

        var hint = Prose(NeedsHint(carrier), TypeScale.Small, ThemeManager.GreyKey);
        hint.Margin = new Thickness(0, 8, 0, 0);
        section.Children.Add(hint);

        return section;
    }

    /// <summary>The line under the needs table: what To buy subtracts, and why the carrier is left out where it is.</summary>
    public static string NeedsHint(CarrierState carrier) =>
        !carrier.Owned ? "To buy is what is left after the hold. The market search asks for that."
        : carrier.Hold.Reconciled == true
            ? "To buy is what is left after the hold and the carrier. The market search asks for that."
        : carrier.Hold.CheckedAt is { } at
            ? $"The carrier's count has not matched its own total since {Day(at)}, so To buy leaves the carrier out. "
                + "It counts again once the carrier reports an empty hold or its figures match."
            : "The carrier's hold has not been checked yet, so To buy leaves the carrier out. "
                + "It counts once the carrier reports its cargo.";

    private static Border NeedRow(ConstructionNeeds need, bool carrierOwned)
    {
        var name = new TextBlock
        {
            Text = need.Resource.Name,
            FontSize = TypeScale.Tip,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var remaining = Figure(Number(need.Remaining), ThemeManager.WhiteKey, 1);
        var hold = Held(need.InHold, 2);

        TextBlock onCarrier = need.OnCarrier is { } tonnes ? Held(tonnes, 3)
            : carrierOwned ? Cell(Chrome(Unknown, TypeScale.MetaSmall, ThemeManager.GreyKey), 3)
            : Figure("—", ThemeManager.Grey2Key, 3);

        var toBuy = need.ToBuy > 0
            ? Figure(Number(need.ToBuy), ThemeManager.AKey, 4)
            : Cell(Chrome(Covered, TypeScale.MetaSmall, ThemeManager.BlueKey), 4);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(NeedsColumns),
            ColumnSpacing = 12,
            Children = { name, remaining, hold, onCarrier, toBuy },
        };

        if (need.OrderOpen)
        {
            var order = Chrome(OrderOpen, TypeScale.MetaSmall, ThemeManager.CyanKey);
            order.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(order, 5);
            grid.Children.Add(order);
        }

        var row = new Border { MinHeight = 36, Padding = new Thickness(14, 6), Child = grid };
        Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;

        static TextBlock Held(int tonnes, int column) =>
            tonnes > 0 ? Figure(Number(tonnes), ThemeManager.YellowKey, column) : Figure("—", ThemeManager.Grey2Key, column);

        static TextBlock Figure(string text, string key, int column) => Cell(Mono(text, TypeScale.Small, key), column);

        static TextBlock Cell(TextBlock block, int column)
        {
            block.HorizontalAlignment = HorizontalAlignment.Right;
            block.VerticalAlignment = VerticalAlignment.Center;
            block.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(block, column);
            return block;
        }
    }

    private Control LastSearch(ConstructionSite site, string station)
    {
        var section = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        var posting = _surface.Sourcing?.Last;
        var ours = posting is not null && string.Equals(posting.Site, site.Where, StringComparison.Ordinal);

        section.Children.Add(RoutingKit.Section(
            "Last market search",
            ours ? Mono($"{Time(posting!.AskedAt)} · near {posting.Near}", TypeScale.Small, ThemeManager.GreyKey) : null));

        if (ours)
        {
            section.Children.Add(Plan(posting!.Answer.Plan));
        }
        else
        {
            var line = posting is null
                ? $"No market search for {station} yet."
                : $"No search for {station} yet. The last one, at {Time(posting.AskedAt)}, was for {posting.Site}.";

            section.Children.Add(Prose(line, TypeScale.Small, ThemeManager.WhiteKey));
        }

        var lookups = _surface.LookupsEnabled() && _surface.Registry is not null;
        _search.IsEnabled = lookups && _inFlight is null;

        var actions = RoutingKit.Actions(_search, _cancel);
        actions.Margin = new Thickness(0, 12, 0, 0);
        section.Children.Add(actions);

        if (!_surface.LookupsEnabled())
        {
            var off = Prose(LookupsOff, TypeScale.Small, ThemeManager.GreyKey);
            off.Margin = new Thickness(0, 6, 0, 0);
            section.Children.Add(off);
        }
        else
        {
            var say = Hint($"Say: “where can I buy what {station} needs”");
            say.Margin = new Thickness(0, 6, 0, 0);
            section.Children.Add(say);
        }

        section.Children.Add(_status);

        return section;
    }

    private static StackPanel Plan(SourcingPlan plan)
    {
        var drawn = new StackPanel { Spacing = Gaps.Tile };

        foreach (var stop in plan.Stops)
        {
            drawn.Children.Add(Stop(stop));
        }

        if (plan.Stops.Count == 0)
        {
            var none = new Border { Padding = new Thickness(14, 10), Child = Prose("No station in range sells any of it.", TypeScale.Small, ThemeManager.WhiteKey) };
            Themed(none, Border.BackgroundProperty, ThemeManager.SlabKey);
            drawn.Children.Add(none);
        }

        var unpriced = plan.Unpriced.Count == 0
            ? Mono("—", TypeScale.Small, ThemeManager.Grey2Key)
            : Chrome(string.Join(", ", plan.Unpriced), TypeScale.Small, ThemeManager.RedKey);

        var shortfall = plan.Shortfalls.Count == 0
            ? Mono("—", TypeScale.Small, ThemeManager.Grey2Key)
            : Runs(
                ThemeManager.WarnKey,
                [.. plan.Shortfalls
                    .OrderByDescending(pair => pair.Value)
                    .SelectMany((pair, index) => new Inline[]
                    {
                        new Run($"{(index == 0 ? string.Empty : ", ")}{pair.Key.ToUpperInvariant()} · "),
                        Fonts.Mono(Tonnes(pair.Value)),
                    })]);

        drawn.Children.Add(DataGrid(
        [
            Block("Nothing in range sells", unpriced),
            Block("Short after every stop", shortfall),
            Block("Whole plan", Mono(Credits(plan.Total), TypeScale.Body, ThemeManager.WhiteKey)),
        ], columns: 3));

        return drawn;
    }

    private static Border Stop(SourcingStop stop)
    {
        var station = Chrome(stop.Market.Station, TypeScale.Secondary, ThemeManager.WhiteKey);
        station.FontWeight = FontWeight.SemiBold;

        var system = new TextBlock
        {
            Text = stop.Market.System,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Tip,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Themed(system, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var names = new WrapPanel { ItemSpacing = 12, Children = { station, system } };

        if (stop.Distance > 0)
        {
            var distance = Mono(string.Create(CultureInfo.InvariantCulture, $"{stop.Distance:0.#} LY"), TypeScale.Meta, ThemeManager.GreyKey);
            distance.VerticalAlignment = VerticalAlignment.Bottom;
            names.Children.Add(distance);
        }

        var total = Mono(Credits(stop.Total), TypeScale.Small, ThemeManager.AKey);
        total.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(total, 1);

        var head = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 12,
            Children = { names, total },
        };

        var lines = new StackPanel { Spacing = 6, Children = { head } };

        foreach (var lot in stop.Lots.OrderByDescending(lot => lot.Tonnes))
        {
            var name = Prose(lot.Commodity, TypeScale.Small, ThemeManager.WhiteKey);
            var tonnes = Right(Mono(Tonnes(lot.Tonnes), TypeScale.Small, ThemeManager.WhiteKey), 1);
            var price = Right(Mono($"{Number(lot.UnitPrice)} cr/t", TypeScale.Small, ThemeManager.GreyKey), 2);
            var line = Right(Mono(Credits(lot.Total), TypeScale.Small, ThemeManager.AKey), 3);

            lines.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(LotColumns),
                ColumnSpacing = 12,
                Children = { name, tonnes, price, line },
            });
        }

        var slab = new Border { Padding = new Thickness(14, 10), Child = lines };
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;

        static TextBlock Right(TextBlock block, int column)
        {
            block.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(block, column);
            return block;
        }
    }

    private async Task SearchAsync()
    {
        if (_surface.Registry is not { } registry
            || _surface.State() is not { } state
            || _selected is not { } id
            || state.Colonisation.ById(id) is not { } site)
        {
            return;
        }

        _inFlight?.Cancel();
        var inFlight = _inFlight = new CancellationTokenSource(Budget);

        _search.IsEnabled = false;
        _cancel.IsVisible = true;
        RoutingKit.Say(_status, "Reading the markets nearby…");

        try
        {
            var result = await registry
                .InvokeAsync(NeedsTool, SearchArguments(site), inFlight.Token, ToolCaller.Commander)
                .ConfigureAwait(true);

            RoutingKit.Say(_status, result.Content, result.IsError);
        }
        catch (OperationCanceledException)
        {
            RoutingKit.Say(_status, "Stopped.");
        }
        finally
        {
            _inFlight = null;
            inFlight.Dispose();
            _search.IsEnabled = _surface.LookupsEnabled();
            _cancel.IsVisible = false;
        }
    }

    private static bool Here(string system, CommanderGameState state) =>
        string.Equals(system, state.Location.StarSystem, StringComparison.OrdinalIgnoreCase);

    private static void Detach(Control control)
    {
        if (control.Parent is Avalonia.Controls.Panel parent)
        {
            parent.Children.Remove(control);
        }
    }

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Tonnes(long value) => $"{Number(value)} t";

    private static string Credits(long value) => $"{Number(value)} cr";

    private static string Percent(double progress) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(Math.Clamp(progress, 0, 1) * 100):0}%");

    private static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Day(DateTimeOffset at) => at.ToLocalTime().ToString("d MMM", CultureInfo.InvariantCulture);

    private static TextBlock Hint(string text) => Prose(text, TypeScale.Tip, ThemeManager.GreyKey);

    /// <summary>A <c>slab</c> data block: a grey chrome label over its value.</summary>
    private static Border Block(string label, Control value)
    {
        var block = new Border
        {
            Padding = new Thickness(14, 12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new StackPanel
            {
                Spacing = 4,
                Children = { Chrome(label, TypeScale.Meta, ThemeManager.GreyKey), value },
            },
        };
        Themed(block, Border.BackgroundProperty, ThemeManager.SlabKey);
        return block;
    }

    /// <summary>Equal columns, 2px apart, wrapping to a new row.</summary>
    private static Grid DataGrid(IReadOnlyList<Control> cells, int columns)
    {
        var grid = new Grid { ColumnSpacing = Gaps.Tile, RowSpacing = Gaps.Tile };

        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var index = 0; index < cells.Count; index++)
        {
            if (index % columns == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            Grid.SetRow(cells[index], index / columns);
            Grid.SetColumn(cells[index], index % columns);
            grid.Children.Add(cells[index]);
        }

        return grid;
    }

    private static TextBlock Chrome(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = size * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    /// <summary>Chrome type at Small, built from runs so a figure among them can be mono.</summary>
    private static TextBlock Runs(string key, IEnumerable<Inline> runs)
    {
        var block = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Small * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };

        block.Inlines!.AddRange(runs);
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Mono(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Prose(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
