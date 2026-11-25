using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Where to buy it, and what it costs there (Phase 49, "Asked by voice, and drawn on the Astrometrics tab").
/// </summary>
public sealed class RouteMarketPage : UserControl
{
    public const string HeadName = "MarketHead";

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    public const string TableId = "Route.Market";

    public const string AddName = "AddCommodity";

    public const string TonnesName = "CommodityTonnes";

    public const string DistanceName = "MarketDistance";

    public const int DefaultDistance = 500;

    public const string FullName = "CommoditiesFull";

    /// <summary>Marks an added commodity's tile; pressing it removes the commodity.</summary>
    public const string ItemClass = "commodity-tile";

    public const string FullText = "Five commodities is the most. Remove one to add another.";

    public const string HelpText =
        "Add each commodity with the tonnes you need; a station has to trade every one. Leave tonnes blank for any amount.";

    public const string NoneText = "Add a commodity first.";

    public const string CarriersOnlyName = "MarketCarriersOnly";

    public const string CarrierMarkName = "MarketCarrierMark";

    public const string CarrierMarkText = "Fleet carrier";

    public const string NoteName = "MarketObtainNote";

    public const string NoteLabel = "How to get it";

    public const string HotspotName = "MarketHotspotLink";

    public const string RingTypeLineName = "MarketRingTypeLine";

    public const string RingTypeLinkName = "MarketRingTypeLink";

    public const string NothingName = "MarketNothingFound";

    /// <summary>The carriers-only Notice and the note stop at this width.</summary>
    private const double NoteWidth = 900;

    private const double CopyGap = 6;

    private readonly CapabilityRegistry _registry;
    private readonly CommodityBoard _board;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly Func<string?> _here;
    private readonly Func<BodySearchLink?>? _bodySearch;

    private readonly StackPanel _body = new() { Spacing = 12 };

    private readonly TextBox _add = new() { Name = AddName, PlaceholderText = "Type a commodity" };

    private readonly SystemNameSuggestions<string> _names;

    private readonly Amount _tonnes = new()
    {
        Name = TonnesName,
        Width = 160,
        Minimum = 1,
        Maximum = 99999,
        Step = 10,
        Format = "#,0",
        Placeholder = "Any",
    };

    private readonly Amount _distance = new()
    {
        Name = DistanceName,
        Value = DefaultDistance,
        Minimum = 1,
        Maximum = 99999,
        Step = 10,
        Format = "#,0",
    };

    private readonly TextBlock _full = RoutingKit.Prose(FullText);
    private readonly WrapPanel _tiles = new() { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };

    private readonly CheckBox _selling;
    private readonly CheckBox _largePad;

    /// <summary>Opts back into surface stations, which <c>surface_stations</c> leaves out by default (#309).</summary>
    private readonly CheckBox _surfaceStations;
    private readonly MaxReportAge _age = new(72);

    private readonly StatusLine _status;

    private Button _find = new();

    private IReadOnlyList<CommodityWant> _items = [];

    private bool _sorting;

    private readonly StackPanel _results = new() { Spacing = 6 };
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly ColumnArrangementMemory? _arrangements;
    private readonly D47.App.Voice.WaitSound? _waitSound;

    private ArrangedTable<CommodityOffer>? _table;
    private string? _shape;
    private CommodityPosting? _posted;
    private CommodityPosting? _shown;
    private bool _local;

    public RouteMarketPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        Func<string, Task<bool>>? copy = null,
        ColumnWidthMemory? columnWidths = null,
        Func<string?>? here = null,
        TimeSpan? suggestionPause = null,
        Func<BodySearchLink?>? bodySearch = null,
        D47.App.Voice.WaitSound? waitSound = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _columnWidths = columnWidths;
        _arrangements = columnArrangements;
        _waitSound = waitSound;
        _bodySearch = bodySearch;
        _registry = registry;
        _board = board;
        _copy = copy;
        _here = here ?? (() => null);

        _selling = RoutingKit.Switch("Selling it, not buying");
        _largePad = RoutingKit.Switch("Large pad only");
        _surfaceStations = RoutingKit.Switch("Include surface stations");

        _status = RoutingKit.Status();

        SystemNameSuggestions<string>? names = null;
        names = new SystemNameSuggestions<string>(
            _add,
            (typed, _) => Task.FromResult(Offer(typed)),
            name => name,
            () => true,
            name =>
            {
                names!.SetText(string.Empty);
                Add(name, _tonnes.Value is { } tonnes && tonnes >= 1 ? (int)tonnes : null);
                _tonnes.Value = null;
            },
            suggestionPause);
        _names = names;

        _full.Name = FullName;
        Avalonia.Automation.AutomationProperties.SetName(_tonnes, "Tonnes");
        FormField.Announce(_add, "Add a commodity", FieldNeed.Optional);

        DrawItems();

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _body,
        };

        Build();
    }

    /// <summary>Types into the add field.</summary>
    public void Type(string text) => _add.Text = text;

    /// <summary>Adds a commodity as picking a suggestion does; one already added, or a sixth, is left out.</summary>
    public void Add(string commodity, int? tonnes)
    {
        if (_items.Count >= CommodityWant.Most
            || _items.Any(kept => string.Equals(kept.Commodity, commodity, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _items = [.. _items, new CommodityWant(commodity, tonnes)];
        DrawItems();
    }

    /// <summary>Replaces the tiles with <paramref name="commodity"/> alone and runs the search, as Find it does.</summary>
    public void SearchFor(string commodity)
    {
        _items = [];
        Add(commodity, null);
        _tonnes.Value = null;
        _find.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>Removes a commodity, as its tile does.</summary>
    public void Remove(string commodity)
    {
        _items = [.. _items.Where(kept => !string.Equals(kept.Commodity, commodity, StringComparison.OrdinalIgnoreCase))];
        DrawItems();
    }

    /// <summary>The list <c>commodity</c> takes: <c>"Gold: 20, Silver"</c>.</summary>
    public static string Listed(IEnumerable<CommodityWant> wants) =>
        string.Join(", ", wants.Select(want => want.Tonnes is { } load
            ? $"{want.Commodity}: {load.ToString(CultureInfo.InvariantCulture)}"
            : want.Commodity));

    /// <summary>What Find it sends to <c>find_nearest_station</c>.</summary>
    public ToolArguments FormArguments()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = Listed(_items),
        };

        if (_selling.IsChecked == true)
        {
            values["selling"] = "true";
        }

        if (_largePad.IsChecked == true)
        {
            values["large_pad"] = "true";
        }

        if (_surfaceStations.IsChecked == true)
        {
            values["surface_stations"] = "true";
        }

        values["max_distance"] = (_distance.Value ?? DefaultDistance).ToString("0.##", CultureInfo.InvariantCulture);
        values["max_price_age_hours"] = _age.Value.ToString(CultureInfo.InvariantCulture);

        return new ToolArguments(values);
    }

    /// <summary>Redraws, after a search.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _body.Children.Clear();

        _body.Children.Add(SearchCard());
        DrawResults();
        _body.Children.Add(_results);
    }

    private Control SearchCard()
    {
        // Every control kept in a field is placed again by each Build(), so it leaves the panel the last one put it in.
        Detach(_add);
        Detach(_names.List);
        Detach(_tonnes);
        Detach(_full);
        Detach(_tiles);
        Detach(_selling);
        Detach(_largePad);
        Detach(_surfaceStations);
        Detach(_distance);
        Detach(_age.Picker);
        Detach(_status);

        var find = _find = new Button { Content = "Find it" };
        var cancel = new Button { Content = "Cancel", IsVisible = false };

        RoutingKit.RunPlot(
            find,
            cancel,
            _status,
            Budget,
            "Reading the markets nearby…",
            Chosen,
            token => _registry.InvokeAsync("find_nearest_station", FormArguments(), token),
            _ => Refresh(),
            _waitSound);

        var add = new StackPanel { Spacing = 3 };
        add.Children.Add(FormField.Label("Add a commodity", FieldNeed.Optional));
        add.Children.Add(_add);
        add.Children.Add(_names.List);

        var tonnes = Labelled("Tonnes", _tonnes);
        Grid.SetColumn(tonnes, 1);

        // The add field takes the width the Tonnes box leaves, up to a limit.
        var fields = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 560 }, new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 12,
            Children = { add, tonnes },
        };

        var commodities = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                RoutingKit.Section("Commodities"),
                fields,
                _full,
                _tiles,
                RoutingKit.Prose(HelpText),
            },
        };

        var market = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                RoutingKit.Section("The market"),
                RoutingKit.Fields(Labelled("Distance in LY", _distance), Labelled("Max price age", _age.Picker)),
                RoutingKit.Fields(_selling, _largePad, _surfaceStations),
                RoutingKit.Actions(find, cancel),
                _status,
            },
        };

        return new StackPanel { Spacing = 14, Children = { commodities, market } };
    }

    /// <summary>Whether there is anything to search for; says so on the status line when not.</summary>
    private bool Chosen()
    {
        if (_items.Count > 0)
        {
            return true;
        }

        RoutingKit.Say(_status, NoneText, error: true);

        return false;
    }

    /// <summary>The market names for what is typed, shortest first: every typed word starts a word of the name.</summary>
    private IReadOnlyList<string> Offer(string typed)
    {
        var words = typed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return [];
        }

        var kept = _items;

        return
        [
            .. MarketCommodities.Names
                .Where(name => kept.All(item => !string.Equals(item.Commodity, name, StringComparison.OrdinalIgnoreCase)))
                .Where(name => words.All(word => name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(known => known.StartsWith(word, StringComparison.OrdinalIgnoreCase))))
                .OrderBy(name => name.Length)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(NameSuggestions.Most),
        ];
    }

    private void DrawItems()
    {
        var full = _items.Count >= CommodityWant.Most;

        _add.IsEnabled = !full;
        _tonnes.IsEnabled = !full;
        _full.IsVisible = full;

        if (full)
        {
            _names.Close();
        }

        _tiles.Children.Clear();

        foreach (var item in _items)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(Chrome(item.Commodity));

            if (item.Tonnes is { } load)
            {
                content.Children.Add(Chrome("·"));
                content.Children.Add(Typeset.Mono($"{load.ToString("N0", CultureInfo.InvariantCulture)} T", TypeScale.Control, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center));
            }

            content.Children.Add(Chrome("✕"));

            var tile = new Button
            {
                Content = content,
                Height = TypeScale.MinimumTarget,
                Classes = { ItemClass },
                Tag = item.Commodity,
            };
            Avalonia.Automation.AutomationProperties.SetName(tile, $"Remove {item.Commodity}");

            var kept = item.Commodity;
            tile.Click += (_, _) => Remove(kept);
            _tiles.Children.Add(tile);
        }

        _tiles.IsVisible = _items.Count > 0;
    }

    private static TextBlock Chrome(string text) =>
        Typeset.Chrome(
            text.ToUpperInvariant(), TypeScale.Control, ThemeManager.AKey,
            weight: FontWeight.SemiBold, tracking: TypeScale.Control * Fonts.ChromeTracking, vertical: VerticalAlignment.Center);

    private void DrawResults()
    {
        _results.Children.Clear();

        if (_board.Last is not { } posting)
        {
            return;
        }

        var buying = posting.Query.Side == TradeSide.Buying;

        var named = posting.Query.Wants.Select(want => want.Commodity).ToList();
        var what = named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";

        var heading = RoutingKit.Ink(
            $"{(buying ? "Buying" : "Selling")} {what} near {posting.Near}",
            TypeScale.Body,
            ThemeManager.AKey,
            wrap: true);

        var stack = new StackPanel { Spacing = 10, Children = { RoutingKit.Section("What came back"), heading } };
        var found = posting.Answer.Offers.Count > 0;

        if (!found)
        {
            stack.Children.Add(NothingFound(posting, buying, named));
        }
        else if (posting.CarriersOnly)
        {
            stack.Children.Add(CarriersOnly(posting, buying));
        }

        if (Note(posting) is { } note)
        {
            stack.Children.Add(note);
        }

        if (found)
        {
            var table = Arrange(posting);
            table.Observe(posting.Answer.Offers);
            var rows = new StackPanel { Spacing = 2 };

            rows.Children.Add(LedgerTables.Head(table, new Thickness(12, 0)));

            if (LocalLine(table, posting) is { } line)
            {
                stack.Children.Insert(2, line);
            }

            if (ColumnFilter.Empty(table, posting.Answer.Offers) is { } empty)
            {
                rows.Children.Add(empty);
            }

            foreach (var offer in Ordered(table, posting))
            {
                var row = ListRow.Dress(new Border { Child = table.Row(offer) });
                row.IsVisible = !ColumnFilter.HidesRows(table);
                rows.Children.Add(row);
            }

            stack.Children.Add(LedgerTables.Arranger(table, posting.Answer.Offers, LocalSort(table)));
            stack.Children.Add(LedgerTables.Sideways(table, rows));
        }

        // The date on the answer itself, which is a different caveat from the date on each price.
        stack.Children.Add(RoutingKit.Prose(
            $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
            + "Commanders; supply moves fastest of all."));

        _results.Children.Add(stack);
    }

    private static string Reach(CommodityPosting posting) =>
        posting.Query.MaxDistance.ToString("0.##", CultureInfo.InvariantCulture);

    private static Control NothingFound(CommodityPosting posting, bool buying, List<string> named)
    {
        var text = Several(posting)
            ? $"No station or fleet carrier within {Reach(posting)} ly {(buying ? "has" : "buys")} them all."
            : $"Nothing within {Reach(posting)} ly {(buying ? "sells" : "buys")} {named[0]}, at a station or on a fleet carrier.";

        var line = RoutingKit.Ink(text, TypeScale.Body, ThemeManager.WhiteKey, wrap: true);
        line.Name = NothingName;

        return line;
    }

    private static Control CarriersOnly(CommodityPosting posting, bool buying)
    {
        var text = Several(posting)
            ? $"No station within {Reach(posting)} ly {(buying ? "has" : "buys")} them all, so these are fleet carriers."
            : $"No station within {Reach(posting)} ly {(buying ? "sells" : "buys")} {posting.Query.Commodity}, so these are fleet carriers.";

        return new Notice(NoticeLevel.Warning)
        {
            Name = CarriersOnlyName,
            Label = "Carriers only",
            Text = $"{text} Their owners set the prices, and the listings move.",
            MaxWidth = NoteWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
    }

    /// <summary>The "how to get it" block, or null when no commodity has a method to show.</summary>
    private Control? Note(CommodityPosting posting)
    {
        var lines = CommodityNote.For(posting);

        if (lines.Count == 0)
        {
            return null;
        }

        var link = _bodySearch?.Invoke();
        var sentences = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        foreach (var line in lines)
        {
            var row = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                ColumnSpacing = 12,
            };

            row.Children.Add(Typeset.Prose(
                line.Sentence, TypeScale.Secondary, ThemeManager.WhiteKey, wrap: true, vertical: VerticalAlignment.Center));

            if (link is not null && line.Hotspot is { } commodity)
            {
                var tile = new Button
                {
                    Name = HotspotName,
                    Content = $"Find rings with {commodity} hotspots",
                    Height = TypeScale.MinimumTarget,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = commodity,
                };
                Grid.SetColumn(tile, 1);

                var near = posting.Near;
                var reach = Math.Min(posting.Query.MaxDistance, BodyQuery.MaxDistanceCeiling);

                tile.Click += (_, _) => link.Run(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["hotspot"] = commodity,
                    ["near"] = near,
                    ["max_distance"] = reach.ToString("0.##", CultureInfo.InvariantCulture),
                });

                row.Children.Add(tile);
            }

            sentences.Children.Add(row);

            if (line.RingTypes is { } ringTypes)
            {
                sentences.Children.Add(RingTypeRow(posting, link, ringTypes, line.RingType));
            }
        }

        var label = Typeset.Chrome(
            NoteLabel.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey,
            weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking,
            vertical: VerticalAlignment.Center);

        Grid.SetColumn(sentences, 1);

        var inside = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 14,
            Children = { label, sentences },
        };

        var slab = new Border
        {
            Name = NoteName,
            Child = inside,
            Padding = new Thickness(14, 8),
            MinHeight = TypeScale.MinimumTarget,
            MaxWidth = NoteWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private static Grid RingTypeRow(CommodityPosting posting, BodySearchLink? link, string text, string? ringType)
    {
        var row = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 12,
        };

        var prose = Typeset.Prose(text, TypeScale.Secondary, ThemeManager.WhiteKey, wrap: true, vertical: VerticalAlignment.Center);
        prose.Name = RingTypeLineName;
        row.Children.Add(prose);

        if (link is not null && ringType is not null)
        {
            var tile = new Button
            {
                Name = RingTypeLinkName,
                Content = $"Find {ringType} rings",
                Height = TypeScale.MinimumTarget,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = ringType,
            };
            Grid.SetColumn(tile, 1);

            var near = posting.Near;
            var reach = Math.Min(posting.Query.MaxDistance, BodyQuery.MaxDistanceCeiling);

            tile.Click += (_, _) => link.Run(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ring_type"] = ringType,
                ["near"] = near,
                ["max_distance"] = reach.ToString("0.##", CultureInfo.InvariantCulture),
            });

            row.Children.Add(tile);
        }

        return row;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _table?.Reopen();
    }

    /// <summary>
    /// The answer's table, kept across redraws so its COLUMNS bar stays open and built again when the fields
    /// change shape. The sort shown is the posting's own, once for each posting.
    /// </summary>
    private ArrangedTable<CommodityOffer> Arrange(CommodityPosting posting)
    {
        _posted = posting;
        var shape = ShapeOf(posting);

        if (_table is null || _shape != shape)
        {
            var table = new ArrangedTable<CommodityOffer>(
                TableId, Fields(posting), _arrangements, _arrangements?.Headset == true ? null : _columnWidths, 10, headName: HeadName);
            table.Changed += DrawResults;
            table.Sorted += SortByHeader;
            _table = table;
            _shape = shape;
            _shown = null;
        }
        else
        {
            _table.Refresh();
        }

        if (!ReferenceEquals(_shown, posting))
        {
            _shown = posting;
            _local = false;
            ShowOrder(_table, posting);
        }

        return _table;
    }

    /// <summary>What decides the fields and their headers.</summary>
    private static string ShapeOf(CommodityPosting posting) =>
        string.Join(
            '|',
            posting.Answer.OriginKnown,
            posting.Query.Side,
            Several(posting),
            ByTheTonne(posting),
            posting.Query.Wants.Any(want => want.Tonnes is null),
            string.Join('/', posting.Query.Wants.Select(want => want.Commodity)));

    /// <summary>Ranked by the usual order until a column is pressed, so a first press reverses it.</summary>
    private static void ShowOrder(ArrangedTable<CommodityOffer> table, CommodityPosting posting)
    {
        var best = posting.Query.Side == TradeSide.Buying ? SortDirection.Ascending : SortDirection.Descending;

        if (posting.Query.OrderBy == CommodityOrder.Distance
            && table.ShowSort(DistanceKey, posting.Query.Descending == true ? SortDirection.Descending : SortDirection.Ascending))
        {
            return;
        }

        table.ShowSort(
            Several(posting) ? TotalKey : PriceKey,
            posting.Query.Descending is { } descending ? descending ? SortDirection.Descending : SortDirection.Ascending : best);
    }

    private const string DistanceKey = "Distance";

    private const string PriceKey = "Price";

    private const string TotalKey = "Total";

    private const string CommoditiesKey = "Commodities";

    private IReadOnlyList<TableField<CommodityOffer>> Fields(CommodityPosting posting)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        var best = buying ? SortDirection.Ascending : SortDirection.Descending;

        List<TableField<CommodityOffer>> fields =
        [
            StarSystemPage.Headed(
                SortColumn<CommodityOffer>.Text("Station", offer => offer.Market.Station, offer => offer.Market.System),
                new TableColumn("Station", ColumnFit.Fill) { Min = 160 },
                offer => StationCell(offer.Market.Station, offer.Market.System, offer.Market.IsCarrier))
                with
                {
                    Filters =
                    [
                        ValueLists.One<CommodityOffer>("System", offer => offer.Market.System, "No system"),
                        ValueLists.Flag<CommodityOffer>("Fleet carrier", offer => offer.Market.IsCarrier, "Not known"),
                    ],
                },
        ];

        if (posting.Answer.OriginKnown)
        {
            fields.Add(StarSystemPage.Headed(
                SortColumn<CommodityOffer>.Remote("Distance", SortDirection.Ascending),
                new TableColumn("Distance"),
                offer => Figure($"{offer.Distance.ToString("0.#", CultureInfo.InvariantCulture)} ly"),
                right: true));
        }

        if (Several(posting))
        {
            var second = buying ? "Price / stock" : "Pays / demand";
            var wants = posting.Query.Wants;

            fields.Add(new TableField<CommodityOffer>(CommoditiesKey, new TableColumn(wants[0].Commodity), offer => LotCell(offer, 0))
            {
                Label = wants[0].Commodity,
                Name = Spoken(wants),
                Sort = LotSort(wants[0].Commodity, 0, best) with { Second = second },
                Filters = [],
                Alignment = HorizontalAlignment.Right,
                Required = true,
                More =
                [
                    .. wants.Skip(1).Select((want, index) =>
                    {
                        var at = index + 1;

                        return new TableField<CommodityOffer>(CommoditiesKey + "." + want.Commodity, new TableColumn(want.Commodity), offer => LotCell(offer, at))
                        {
                            Label = want.Commodity,
                            Sort = LotSort(want.Commodity, at, best) with { Second = second },
                            Filters = [],
                            Alignment = HorizontalAlignment.Right,
                        };
                    }),
                ],
            });

            fields.Add(StarSystemPage.Headed(
                SortColumn<CommodityOffer>.Remote("Total", best) with
                {
                    Second = wants.Any(want => want.Tonnes is null) ? "1 T where blank" : null,
                },
                new TableColumn("Total"),
                offer => Figure(Count(offer.Total)),
                right: true));
        }
        else
        {
            fields.Add(StarSystemPage.Headed(
                SortColumn<CommodityOffer>.Remote(buying ? "Price" : "Pays", best),
                new TableColumn("Price"),
                offer => Figure(Count(offer.UnitPrice)),
                right: true));
            fields.Add(StarSystemPage.Headed(
                SortColumn<CommodityOffer>.Figure<long>(buying ? "Stock" : "Demand", offer => StockOf(offer, posting.Query.Commodity, buying), SortDirection.Descending),
                new TableColumn("Stock"),
                offer => Figure(Count(StockOf(offer, posting.Query.Commodity, buying))),
                right: true));

            if (ByTheTonne(posting))
            {
                fields.Add(StarSystemPage.Headed(
                    SortColumn<CommodityOffer>.Figure<long>("The load", offer => offer.Total, SortDirection.Descending), new TableColumn("Load"), offer => Figure(Count(offer.Total)), right: true));
            }
        }

        fields.Add(StarSystemPage.Headed(SortColumn<CommodityOffer>.Figure<long>("Priced", offer => offer.Market.UpdatedAt?.ToUnixTimeSeconds(), SortDirection.Descending), new TableColumn("Priced"), PricedCell, right: true));

        fields.Add(Added("FromStar", "From star", offer => LightCell(offer.Market.DistanceToArrival), SortColumn<CommodityOffer>.Figure<double>("From star", offer => offer.Market.DistanceToArrival, SortDirection.Ascending), right: true));
        fields.Add(Added("LargePad", "Large pad", offer => TableCells.Flag(offer.Market.HasLargePad), SortColumn<CommodityOffer>.Figure<int>("Large pad", offer => offer.Market.HasLargePad ? 1 : 0, SortDirection.Descending))
            with { Filters = [ValueLists.Flag<CommodityOffer>("Large pad", offer => offer.Market.HasLargePad, "Not known")] });
        fields.Add(Added("Surface", "Surface", offer => TableCells.Flag(offer.Market.IsSurface), SortColumn<CommodityOffer>.Figure<int>("Surface", offer => offer.Market.IsSurface ? 1 : 0, SortDirection.Descending))
            with { Filters = [ValueLists.Flag<CommodityOffer>("Surface", offer => offer.Market.IsSurface, "Not known")] });
        fields.Add(Added("Power", "Power", offer => TableCells.Text(offer.Market.ControllingPower), SortColumn<CommodityOffer>.Text("Power", offer => offer.Market.ControllingPower))
            with { Filters = [ValueLists.One<CommodityOffer>("Power", offer => offer.Market.ControllingPower, "No Power")] });
        fields.Add(Added("PowerState", "Power state", offer => TableCells.Text(offer.Market.PowerState), SortColumn<CommodityOffer>.Text("Power state", offer => offer.Market.PowerState))
            with { Filters = [ValueLists.One<CommodityOffer>("Power state", offer => offer.Market.PowerState, "No Power state")] });

        return fields;
    }

    private static SortColumn<CommodityOffer> LotSort(string commodity, int at, SortDirection best) =>
        SortColumn<CommodityOffer>.Figure<long>(commodity, offer => at < offer.Lots.Count ? offer.Lots[at].UnitPrice : null, best);

    private static long StockOf(CommodityOffer offer, string commodity, bool buying)
    {
        var quote = offer.Market.Quote(commodity);

        return buying ? quote?.Supply ?? 0 : quote?.Demand ?? 0;
    }

    private static TableField<CommodityOffer> Added(string key, string label, Func<CommodityOffer, Control> cell, SortColumn<CommodityOffer> sort, bool right = false) =>
        LedgerTables.Added(key, new TableColumn(key), cell, sort) with
        {
            Alignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

    private static Control LightCell(double? lightSeconds)
    {
        var text = StationSearchPage.Light(lightSeconds);

        return Typeset.Mono(
            text,
            TypeScale.Control,
            lightSeconds is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            vertical: VerticalAlignment.Center,
            horizontal: HorizontalAlignment.Right);
    }

    /// <summary>"Gold", "Gold and Silver", "Gold, Silver and Tin".</summary>
    private static string Spoken(IReadOnlyList<CommodityWant> wants) =>
        wants.Count == 1
            ? wants[0].Commodity
            : $"{string.Join(", ", wants.Take(wants.Count - 1).Select(want => want.Commodity))} and {wants[^1].Commodity}";

    /// <summary>Asks the same question again, ranked by the pressed column; none is the usual order.</summary>
    private void SortByHeader()
    {
        if (_posted is not { } posting || _table is not { } table)
        {
            return;
        }

        var wasLocal = _local;
        _local = table.SortedBy is { } sort && sort.Key is not (DistanceKey or PriceKey or TotalKey);

        if (_local)
        {
            return;
        }

        if (wasLocal && table.SortedBy is null)
        {
            ShowOrder(table, posting);

            return;
        }

        SortBy(posting, table.SortedBy);
    }

    /// <summary>The offers in the order of the pressed column while that column sorts the rows shown.</summary>
    private IReadOnlyList<CommodityOffer> Ordered(ArrangedTable<CommodityOffer> table, CommodityPosting posting) =>
        _local ? table.Shown(posting.Answer.Offers) : table.Filter(posting.Answer.Offers);

    /// <summary>The name of the field the rows shown are sorted by while that sort is done here; null while the service orders them.</summary>
    private string? LocalSort(ArrangedTable<CommodityOffer> table)
    {
        if (!_local || table.SortedBy is not { } sort)
        {
            return null;
        }

        var field = table.ShownFields.FirstOrDefault(shown => shown.Key == sort.Key);

        return field?.Name ?? field?.Label ?? sort.Key;
    }

    /// <summary>"These 2 sorted by Stock", under the heading while the rows shown are sorted.</summary>
    private Control? LocalLine(ArrangedTable<CommodityOffer> table, CommodityPosting posting)
    {
        if (LocalSort(table) is not { } by || table.Hidden(posting.Answer.Offers) > 0)
        {
            return null;
        }

        var text = $"These {posting.Answer.Offers.Count.ToString(CultureInfo.InvariantCulture)} sorted by {by}";
        var line = Typeset.Chrome(
            text.ToUpperInvariant(), TypeScale.Small, ThemeManager.AKey,
            weight: FontWeight.Medium, tracking: TypeScale.Small * Fonts.ChromeTracking);
        line.Name = LocalOrderName;

        return line;
    }

    /// <summary>The line saying the rows shown, not the search, are sorted.</summary>
    public const string LocalOrderName = "LocalOrder";

    private void SortBy(CommodityPosting posting, (string Key, SortDirection Direction)? order)
    {
        var query = posting.Query;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = Listed(query.Wants),
            ["near"] = posting.Near,
            ["max_distance"] = query.MaxDistance.ToString("0.##", CultureInfo.InvariantCulture),
            ["limit"] = query.Limit.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var (key, on) in new (string, bool)[]
                 {
                     ("selling", query.Side == TradeSide.Selling),
                     ("large_pad", query.LargePadOnly),
                     ("include_carriers", query.IncludeCarriers),
                     ("surface_stations", query.SurfaceStations),
                 })
        {
            if (on)
            {
                values[key] = "true";
            }
        }

        if (query.MaxStationDistance is { } lightSeconds)
        {
            values["max_station_distance"] = lightSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        }

        if (query.MinAvailable is { } least)
        {
            values["min_supply"] = least.ToString(CultureInfo.InvariantCulture);
        }

        if (query.Tag is { } tag)
        {
            values["tag"] = tag;
        }

        if (order is { } sort)
        {
            values["order_by"] = sort.Key == DistanceKey ? "distance" : "price";
            values["order"] = sort.Direction == SortDirection.Ascending ? "asc" : "desc";
        }

        _ = AskAsync(new ToolArguments(values));
    }

    private async Task AskAsync(ToolArguments arguments)
    {
        if (_sorting)
        {
            return;
        }

        _sorting = true;

        try
        {
            using var inFlight = new CancellationTokenSource(Budget);
            ToolResult? result = null;

            await Waiting.While(
                _status,
                "Reading the markets nearby…",
                _waitSound,
                async () => result = await _registry.InvokeAsync("find_nearest_station", arguments, inFlight.Token).ConfigureAwait(true))
                .ConfigureAwait(true);

            RoutingKit.Say(_status, result!.Content, result.IsError);
            Refresh();
        }
        catch (OperationCanceledException)
        {
            RoutingKit.Say(_status, "Stopped.");
        }
        finally
        {
            _sorting = false;
        }
    }

    private Control LotCell(CommodityOffer offer, int at)
    {
        var buying = _posted!.Query.Side == TradeSide.Buying;
        var lot = offer.Lots[at];
        var held = offer.Market.Quote(lot.Commodity);
        var stock = held is null ? "?" : Count(buying ? held.Supply : held.Demand);

        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 1,
            Children = { Figure(Count(lot.UnitPrice)), Figure(stock, ThemeManager.GreyKey, TypeScale.Caption) },
        };
    }

    /// <summary>The Commander's own reading is named as theirs, because it is the one figure here with no caveat on it.</summary>
    private static Control PricedCell(CommodityOffer offer)
    {
        var priced = RoutingKit.Ink(
            offer.Market.UpdatedAt is { } when
                ? $"{(offer.IsTheirs ? "you saw it " : string.Empty)}{Ago(DateTimeOffset.UtcNow - when)}"
                : "undated",
            TypeScale.Secondary,
            offer.IsTheirs ? ThemeManager.CyanKey : ThemeManager.GreyKey);
        priced.TextTrimming = TextTrimming.CharacterEllipsis;
        priced.VerticalAlignment = VerticalAlignment.Center;
        priced.HorizontalAlignment = HorizontalAlignment.Right;
        priced.TextAlignment = TextAlignment.Right;

        return priced;
    }

    private static string Count(long figure) => figure.ToString("N0", CultureInfo.InvariantCulture);

    private static string Ago(TimeSpan old) => old switch
    {
        { TotalHours: < 1 } => "within the hour",
        { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
        { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
        _ => $"{old.TotalDays / 7:0} weeks ago",
    };

    /// <summary>The Station column: the station in White, its system in A or Cyan where the Commander is, and a copy glyph for the system (#157).</summary>
    private Control StationCell(string station, string system, bool carrier)
    {
        // The name takes what the copy glyph leaves, so a long one trims rather than running under it.
        var cells = new DockPanel();

        if (_copy is { } copy)
        {
            var glyph = CopyGlyph.For(system, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(CopyGap, 0, 0, 0);

            DockPanel.SetDock(glyph, Dock.Right);
            cells.Children.Add(glyph);
        }

        if (_copy is null)
        {
            var name = new Run(station);
            ThemeInk.Bind(name, TextElement.ForegroundProperty, ThemeManager.WhiteKey);

            var place = new Run($" ({system})");
            ThemeInk.Bind(place, TextElement.ForegroundProperty, RoutingKit.SystemKey(system, _here()));

            cells.Children.Add(new TextBlock
            {
                FontSize = TypeScale.Secondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Inlines = [name, place],
            });
        }
        else
        {
            cells.Children.Add(Placed(station, system));
        }

        if (!carrier)
        {
            return cells;
        }

        var mark = Typeset.Chrome(
            CarrierMarkText.ToUpperInvariant(), TypeScale.MetaSmall, ThemeManager.GreyKey,
            weight: FontWeight.SemiBold, tracking: TypeScale.MetaSmall * Fonts.ChromeTracking);
        mark.Name = CarrierMarkName;

        return new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { cells, mark } };
    }

    /// <summary>
    /// The station, then its system as a link in brackets, each a block of its own centred on the row:
    /// the link's press target is taller than a line of text, so it cannot sit inside one. The station trims first.
    /// </summary>
    private DockPanel Placed(string station, string system)
    {
        TextBlock Piece(string text, string key)
        {
            var piece = new TextBlock { Text = text, FontSize = TypeScale.Secondary, VerticalAlignment = VerticalAlignment.Center };
            ThemeInk.Bind(piece, TextBlock.ForegroundProperty, key);
            return piece;
        }

        var link = new TextBlock { Text = system, FontSize = TypeScale.Secondary };
        ThemeInk.Bind(link, TextBlock.ForegroundProperty, RoutingKit.SystemKey(system, _here()));

        var name = Piece(station, ThemeManager.WhiteKey);
        name.TextTrimming = TextTrimming.CharacterEllipsis;

        var placed = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left };

        foreach (var right in new Control[] { Piece(")", ThemeManager.WhiteKey), SystemLink.Of(link, system, flush: false), Piece(" (", ThemeManager.WhiteKey) })
        {
            DockPanel.SetDock(right, Dock.Right);
            placed.Children.Add(right);
        }

        placed.Children.Add(name);
        return placed;
    }

    /// <summary>Whether the answer is for several commodities at one station.</summary>
    private static bool Several(CommodityPosting posting) => posting.Query.More is { Count: > 0 };

    /// <summary>Whether any commodity was asked for by the tonne, so the answer has a load to price.</summary>
    private static bool ByTheTonne(CommodityPosting posting) => posting.Query.Wants.Any(want => want.Tonnes is not null);

    /// <summary>A figure in mono, right-aligned in its column.</summary>
    private static TextBlock Figure(string text, string key = ThemeManager.AKey, double size = TypeScale.Secondary) =>
        Typeset.Mono(
            text, size, key,
            trimming: TextTrimming.CharacterEllipsis,
            vertical: VerticalAlignment.Center,
            horizontal: HorizontalAlignment.Right,
            align: TextAlignment.Right);

    /// <summary>Removes a control from whatever panel currently holds it, if any.</summary>
    private static void Detach(Control control)
    {
        if (control.Parent is Avalonia.Controls.Panel parent)
        {
            parent.Children.Remove(control);
        }
    }

    private static Control Labelled(string label, Control box)
    {
        var stack = new StackPanel { Spacing = 3 };

        stack.Children.Add(FormField.Label(label, FieldNeed.Optional));
        stack.Children.Add(box);

        return stack;
    }
}
