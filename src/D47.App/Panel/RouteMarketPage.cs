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

    public const int DefaultDistance = 50;

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

    public const string NothingName = "MarketNothingFound";

    /// <summary>The carriers-only Notice and the note stop at this width.</summary>
    private const double NoteWidth = 900;

    private const double CopyGap = 6;

    /// <summary>Every name a market trades, ordinary and rare.</summary>
    private static readonly Lazy<IReadOnlyList<string>> Commodities = new(() =>
        [.. MaterialCatalogue.All
            .Where(entry => entry.Ledger is MaterialLedger.Cargo or MaterialLedger.RareCargo)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)]);

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

    private IReadOnlyList<CommodityWant> _items = [];

    private bool _sorting;

    private readonly StackPanel _results = new() { Spacing = 6 };
    private readonly ColumnWidthMemory? _columnWidths;

    public RouteMarketPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        Func<string, Task<bool>>? copy = null,
        ColumnWidthMemory? columnWidths = null,
        Func<string?>? here = null,
        TimeSpan? suggestionPause = null,
        Func<BodySearchLink?>? bodySearch = null)
    {
        _columnWidths = columnWidths;
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

        var find = new Button { Content = "Find it" };
        var cancel = new Button { Content = "Cancel", IsVisible = false };

        RoutingKit.RunPlot(
            find,
            cancel,
            _status,
            Budget,
            "Reading the markets nearby…",
            Chosen,
            token => _registry.InvokeAsync("find_nearest_station", FormArguments(), token),
            _ => Refresh());

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
            .. Commodities.Value
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
            var heads = Heads(posting);
            var rows = new StackPanel { Spacing = 2 };
            var layout = new TableLayout(TableId, [.. heads.Select(head => head.Column)], _columnWidths, 10);

            rows.Children.Add(HeaderRow(posting, layout, heads));

            foreach (var offer in posting.Answer.Offers)
            {
                rows.Children.Add(OfferRow(offer, posting, layout));
            }

            stack.Children.Add(rows);
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

    /// <summary>One column of the answer table: its layout, its header, and whether its figures sit right.</summary>
    private sealed record Head(TableColumn Column, SortColumn<object> Sort, bool Right);

    private static List<Head> Heads(CommodityPosting posting)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        var best = buying ? SortDirection.Ascending : SortDirection.Descending;

        List<Head> heads = [new(new("Station", ColumnFit.Fill) { Min = 160 }, SortColumn<object>.Plain("Station"), false)];

        if (posting.Answer.OriginKnown)
        {
            heads.Add(new(new("Distance"), SortColumn<object>.Remote("Distance", SortDirection.Ascending), true));
        }

        if (Several(posting))
        {
            var second = buying ? "Price / stock" : "Pays / demand";

            foreach (var want in posting.Query.Wants)
            {
                heads.Add(new(new(want.Commodity), SortColumn<object>.Plain(want.Commodity) with { Second = second }, true));
            }

            heads.Add(new(
                new("Total"),
                SortColumn<object>.Remote("Total", best) with
                {
                    Second = posting.Query.Wants.Any(want => want.Tonnes is null) ? "1 T where blank" : null,
                },
                true));
        }
        else
        {
            heads.Add(new(new("Price"), SortColumn<object>.Remote(buying ? "Price" : "Pays", best), true));
            heads.Add(new(new("Stock"), SortColumn<object>.Plain(buying ? "Stock" : "Demand"), true));

            if (ByTheTonne(posting))
            {
                heads.Add(new(new("Load"), SortColumn<object>.Plain("The load"), true));
            }
        }

        heads.Add(new(new("Priced"), SortColumn<object>.Plain("Priced"), true));

        return heads;
    }

    /// <summary>The distance and price or total columns sort the whole answer, so a press asks again; the rest are plain labels.</summary>
    private Control HeaderRow(CommodityPosting posting, TableLayout layout, List<Head> heads)
    {
        var best = posting.Query.Side == TradeSide.Buying ? SortDirection.Ascending : SortDirection.Descending;

        var header = new SortHeader<object>(HeadName, [.. heads.Select(head => head.Sort)]);
        var distance = heads.FindIndex(head => head.Sort.Label == "Distance");
        var price = heads.FindIndex(head => head.Sort.Label is "Price" or "Pays" or "Total");

        // Ranked by the usual order until a column is pressed, so a first press reverses it.
        var current = posting.Query.OrderBy == CommodityOrder.Distance && distance >= 0
            ? new SortOrder(distance, posting.Query.Descending == true ? SortDirection.Descending : SortDirection.Ascending)
            : new SortOrder(price, posting.Query.Descending is { } descending
                ? descending ? SortDirection.Descending : SortDirection.Ascending
                : best);

        header.Show(current);
        header.Changed += () => SortBy(posting, header.Order, distance);

        // Inset by a list row's padding, so each header sits over its column.
        var row = layout.Header(header, [.. heads.Select(head => head.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left)]);
        row.Margin = new Thickness(12, 0);

        return row;
    }

    /// <summary>Asks the same question again, ranked by the pressed column; null is the usual order.</summary>
    private void SortBy(CommodityPosting posting, SortOrder? order, int distance)
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
            values["order_by"] = sort.Column == distance ? "distance" : "price";
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
        RoutingKit.Say(_status, "Reading the markets nearby…");

        try
        {
            using var inFlight = new CancellationTokenSource(Budget);
            var result = await _registry.InvokeAsync("find_nearest_station", arguments, inFlight.Token).ConfigureAwait(true);

            RoutingKit.Say(_status, result.Content, result.IsError);
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

    private Control OfferRow(CommodityOffer offer, CommodityPosting posting, TableLayout layout)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        List<Control> cells = [StationCell(offer.Market.Station, offer.Market.System, offer.Market.IsCarrier)];

        if (posting.Answer.OriginKnown)
        {
            cells.Add(Figure($"{offer.Distance.ToString("0.#", CultureInfo.InvariantCulture)} ly"));
        }

        if (Several(posting))
        {
            foreach (var lot in offer.Lots)
            {
                var held = offer.Market.Quote(lot.Commodity);
                var stock = held is null ? "?" : Count(buying ? held.Supply : held.Demand);

                cells.Add(new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 1,
                    Children = { Figure(Count(lot.UnitPrice)), Figure(stock, ThemeManager.GreyKey, TypeScale.Caption) },
                });
            }

            cells.Add(Figure(Count(offer.Total)));
        }
        else
        {
            var quote = offer.Market.Quote(posting.Query.Commodity);

            cells.Add(Figure(Count(offer.UnitPrice)));
            cells.Add(Figure(Count(buying ? quote?.Supply ?? 0 : quote?.Demand ?? 0)));

            if (ByTheTonne(posting))
            {
                cells.Add(Figure(Count(offer.Total)));
            }
        }

        // The Commander's own reading is named as theirs, because it is the one figure here with no caveat on it.
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
        cells.Add(priced);

        return ListRow.Dress(new Border { Child = layout.Row(cells) });
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

        var name = new Run(station);
        var place = new Run($" ({system})");
        ThemeInk.Bind(name, TextElement.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(place, TextElement.ForegroundProperty, RoutingKit.SystemKey(system, _here()));

        cells.Children.Add(new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Inlines = [name, place],
        });

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
