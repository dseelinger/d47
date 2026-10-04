using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// Fleet › Materials › one ship material: what it is, who needs it, how to farm it, the nearest places a
/// galaxy search finds it and what a material trader gives for it.
/// </summary>
public sealed class MaterialDetailPage : UserControl
{
    public const string NeededHint = "Live plans that still need it.";

    public const string NearestHint = "A live search of the galaxy from where you are.";

    public const string TraderHint = "What you give, and what you get.";

    public const string TurnItOn =
        "Turn on “Look things up in the galaxy” in Settings and D47 will search for the nearest places it's found.";

    public const string Searching = "Searching the galaxy…";

    public const string Unanswered = "The galaxy search did not answer. It runs again when you change system.";

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    private readonly GapSource _gap;
    private readonly PanelNavigator _nav;
    private readonly MaterialEntry _material;
    private readonly ContentControl _sidebar = new();
    private readonly StackPanel _body = new();

    /// <summary>The system the last search started from, or null when none has started.</summary>
    private string? _searchedFrom;

    private bool _searched;
    private CancellationTokenSource? _search;
    private MaterialPlaces? _places;
    private string? _failure;

    public MaterialDetailPage(GapSource gap, PanelNavigator nav, MaterialEntry material, JournalClock clock)
    {
        _gap = gap;
        _nav = nav;
        _material = material;

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase(material), clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var columns = new DockPanel();
        DockPanel.SetDock(_sidebar, Dock.Left);
        columns.Children.Add(_sidebar);
        columns.Children.Add(LoadoutPages.Scrolling(_body));
        root.Children.Add(columns);

        Content = root;
    }

    /// <summary>The footer's phrase.</summary>
    public static string Phrase(MaterialEntry material) =>
        $"how much {material.Name.ToLowerInvariant()} do I have";

    /// <summary>A trader line as the data grid shows it: <c>raw-6</c> is Category 6, <c>manufactured-heat</c> is Heat.</summary>
    public static string LineName(MaterialEntry material)
    {
        if (material.Line is not { Length: > 0 } line)
        {
            return material.Category ?? "—";
        }

        var dash = line.IndexOf('-', StringComparison.Ordinal);
        var tail = dash < 0 ? line : line[(dash + 1)..];

        if (line.StartsWith("raw-", StringComparison.Ordinal))
        {
            return $"Category {tail}";
        }

        var words = tail.Split('-', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', words.Select((word, i) =>
            i == 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word));
    }

    /// <summary>The hint in place of the trader section for a material no trader deals in.</summary>
    public static string NoTrader(string ledger) => $"No material trader deals in {ledger} materials.";

    /// <summary>Redraws against the live plans, the live inventory and the last search.</summary>
    public void Refresh()
    {
        var report = _gap.Report();
        var ledgers = _gap.Tracker(report).Ship;
        var card = ledgers.FirstOrDefault(c =>
            c.Rows.Any(row => string.Equals(row.Material.Symbol, _material.Symbol, StringComparison.OrdinalIgnoreCase)));
        var ledger = card?.Name ?? _material.Category ?? "Material";

        _sidebar.Content = MaterialsPage.SidebarOf(report, ledgers, _gap.View, Back);

        Search();

        var detail = MaterialGuide.For(_material.Symbol, _gap.State());

        _body.Children.Clear();
        _body.Children.Add(Heading(ledger, card));
        _body.Children.Add(Facts(ledger, detail));

        var needs = report.Builds
            .Select(build => (Build: build, Line: build.Lines.FirstOrDefault(line =>
                string.Equals(line.Material.Symbol, _material.Symbol, StringComparison.OrdinalIgnoreCase))))
            .Where(pair => pair.Line is not null)
            .ToList();

        if (needs.Count > 0)
        {
            var rows = Rows();

            foreach (var (build, line) in needs)
            {
                rows.Children.Add(NeedRow(build, line!));
            }

            _body.Children.Add(Section("Needed by plans", NeededHint, rows));
        }

        if (detail is { HowToFarm.Count: > 0 })
        {
            _body.Children.Add(Section("How to farm it", null, Slab(Prose(string.Join(" ", detail.HowToFarm)))));
        }

        if (MaterialGuide.Searchable(_material))
        {
            _body.Children.Add(Section("Nearest to you", NearestHint, Nearest()));
        }

        var trades = MaterialGuide.TradesInto(_material);

        if (trades.Count > 0)
        {
            var rows = Rows();

            foreach (var trade in trades)
            {
                rows.Children.Add(TradeRow(trade));
            }

            _body.Children.Add(Section("At a material trader", TraderHint, rows));
        }
        else
        {
            _body.Children.Add(MaterialsPage.Hint(NoTrader(ledger), new Thickness(0, 28, 0, 0)));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _gap.Changed -= OnChanged;
        _gap.Changed += OnChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _gap.Changed -= OnChanged;
        Cancel();
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    /// <summary>Goes back to the Materials page, on <paramref name="view"/>.</summary>
    internal void Back(string view)
    {
        _gap.View = view;
        _nav.JumpTo(0);
    }

    /// <summary>
    /// Starts a search when galaxy search is on and none has run from the current system; cancels one when it
    /// is off. The search runs on the pool and posts its answer back.
    /// </summary>
    private void Search()
    {
        if (!MaterialGuide.Searchable(_material))
        {
            return;
        }

        if (_gap.Galaxy() is not { } galaxy)
        {
            Cancel();
            return;
        }

        var state = _gap.State();
        var system = state?.Location.StarSystem;

        if (_searched && string.Equals(system, _searchedFrom, StringComparison.Ordinal))
        {
            return;
        }

        Cancel();

        _searched = true;
        _searchedFrom = system;
        _places = null;
        _failure = null;

        var search = _search = new CancellationTokenSource(Budget);
        var symbol = _material.Symbol;

        _ = Task.Run(async () =>
        {
            MaterialPlaces? found = null;
            string? failure = null;

            try
            {
                found = await MaterialGuide.NearestAsync(symbol, state, galaxy, search.Token, near: system)
                    .ConfigureAwait(false);
            }
            catch (GalaxyUnavailableException)
            {
                failure = Unanswered;
            }
            catch (OperationCanceledException)
            {
                failure = Unanswered;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!ReferenceEquals(search, _search))
                {
                    return;
                }

                _places = found;
                _failure = failure;
                Refresh();
            });
        });
    }

    private void Cancel()
    {
        if (_search is { } running)
        {
            _search = null;
            running.Cancel();
        }

        _searched = false;
        _searchedFrom = null;
        _places = null;
        _failure = null;
    }

    private Control Heading(string ledger, MaterialCard? card)
    {
        var crumbs = new WrapPanel();

        crumbs.Children.Add(Crumb("Materials", () => Back(MaterialsPage.NeededView)));
        crumbs.Children.Add(TitleText.Context(" › "));

        if (card is not null)
        {
            crumbs.Children.Add(Crumb(ledger, () => Back(MaterialsPage.ViewOf(card))));
        }
        else
        {
            crumbs.Children.Add(TitleText.Context(ledger));
        }

        crumbs.Children.Add(TitleText.Context(" ›"));

        var title = TitleText.Build(_material.Name, TypeScale.Title, TitleRank.Screen);

        return new StackPanel { Spacing = 2, Children = { crumbs, title } };
    }

    private static TextBlock Crumb(string text, Action press)
    {
        var crumb = TitleText.Context(text);
        MaterialsPage.Pressable(crumb, press);
        return crumb;
    }

    /// <summary>Ledger, category, grade, and held against the cap with a gauge.</summary>
    private static Grid Facts(string ledger, MaterialDetail? detail)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 2,
            Margin = new Thickness(0, 18, 0, 0),
        };

        var material = detail?.Entry;

        grid.Children.Add(Cell(0, "Ledger", Value(ledger)));
        grid.Children.Add(Cell(1, "Category", Value(material is null ? "—" : LineName(material))));
        grid.Children.Add(Cell(
            2,
            "Grade",
            MaterialsPage.Mono(detail?.Grade is { } grade ? $"G{MaterialsPage.Count(grade)}" : "—", ThemeManager.AKey)));

        var held = detail?.Held ?? 0;
        var reading = detail?.Capacity is { } cap
            ? $"{MaterialsPage.Count(held)} / {MaterialsPage.Count(cap)}"
            : MaterialsPage.Count(held);

        var heading = new DockPanel();
        var label = MaterialsPage.Chrome("Held", ThemeManager.GreyKey);
        var figure = MaterialsPage.Mono(reading, ThemeManager.YellowKey);
        DockPanel.SetDock(figure, Dock.Right);
        heading.Children.Add(figure);
        heading.Children.Add(label);

        var bar = MaterialsPage.CapacityBar(detail?.Capacity is { } limit and > 0 ? (double)held / limit : 0);
        bar.Margin = new Thickness(0, 8, 0, 0);

        var gauge = new StackPanel { Children = { heading, bar } };
        var tile = Tile(gauge);
        Grid.SetColumn(tile, 3);
        grid.Children.Add(tile);

        return grid;
    }

    private static Border Cell(int column, string label, Control value)
    {
        value.HorizontalAlignment = HorizontalAlignment.Left;
        value.Margin = new Thickness(0, 4, 0, 0);

        var tile = Tile(new StackPanel { Children = { MaterialsPage.Chrome(label, ThemeManager.GreyKey), value } });
        Grid.SetColumn(tile, column);
        return tile;
    }

    private static Border Tile(Control child)
    {
        var tile = new Border { Padding = new Thickness(14, 12), Child = child };
        LoadoutPages.Themed(tile, Border.BackgroundProperty, ThemeManager.TileKey);
        return tile;
    }

    private static TextBlock Value(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        return block;
    }

    private static Control Section(string head, string? hint, Control content)
    {
        var name = TitleText.Build(head, TypeScale.Section, TitleRank.Group);
        name.VerticalAlignment = VerticalAlignment.Center;

        var heading = new WrapPanel { ItemSpacing = 12, LineSpacing = 4, Children = { name } };

        if (hint is not null)
        {
            var caption = MaterialsPage.Hint(hint, default);
            caption.VerticalAlignment = VerticalAlignment.Center;
            heading.Children.Add(caption);
        }

        content.Margin = new Thickness(0, 10, 0, 0);

        return new StackPanel
        {
            Margin = new Thickness(0, 28, 0, 0),
            Children = { TitleText.GroupRow(heading), content },
        };
    }

    private static StackPanel Rows() => new() { Spacing = 2 };

    private static Border Slab(Control child)
    {
        var slab = new Border { Padding = new Thickness(14, 12), Child = child };
        LoadoutPages.Themed(slab, Border.BackgroundProperty, ThemeManager.TileKey);
        return slab;
    }

    private static TextBlock Prose(string text, string key = ThemeManager.WhiteKey)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    /// <summary>A 44px row on Tile, its columns laid out by <paramref name="columns"/>.</summary>
    private static Border Row(string columns, params Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };

        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        var row = new Border { Height = TypeScale.MinimumTarget, Padding = new Thickness(14, 0), Child = grid };
        MaterialsPage.Hover(row);
        return row;
    }

    /// <summary><c>build · kind | NEED n | SHORT n</c> or <c>✓ MET</c>.</summary>
    private static Border NeedRow(GapBuild build, GapBuildLine line)
    {
        var name = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var title = new Avalonia.Controls.Documents.Run(build.Name);
        LoadoutPages.Themed(title, Avalonia.Controls.Documents.TextElement.ForegroundProperty, ThemeManager.WhiteKey);
        var kind = new Avalonia.Controls.Documents.Run(" · " + MaterialsPage.KindOf(build).ToUpperInvariant())
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.MetaSmall,
        };
        LoadoutPages.Themed(kind, Avalonia.Controls.Documents.TextElement.ForegroundProperty, ThemeManager.GreyKey);
        name.Inlines = [title, kind];

        var need = MaterialsPage.Mono($"NEED {MaterialsPage.Count(line.Needed)}", ThemeManager.WhiteKey);

        var shortfall = line.Short > 0
            ? MaterialsPage.Mono($"SHORT {MaterialsPage.Count(line.Short)}", ThemeManager.AKey)
            : MaterialsPage.Mono(MaterialsPage.Met, ThemeManager.BlueKey);

        return Row("*,110,110", name, need, shortfall);
    }

    /// <summary>The search results nearest first, the turn-it-on line, or what the search said.</summary>
    private Control Nearest()
    {
        if (_gap.Galaxy() is null)
        {
            return Slab(Prose(TurnItOn, ThemeManager.GreyKey));
        }

        if (_failure is { } failure)
        {
            return Slab(Prose(failure, ThemeManager.GreyKey));
        }

        if (_places is not { } places)
        {
            return Slab(Prose(Searching, ThemeManager.GreyKey));
        }

        if (places.Places.Count == 0)
        {
            return Slab(Prose(places.Message ?? "The search found nowhere nearby.", ThemeManager.GreyKey));
        }

        var rows = Rows();

        foreach (var place in places.Places.OrderBy(place => place.Distance ?? double.MaxValue))
        {
            var where = new TextBlock
            {
                Text = place.Place.ToUpperInvariant(),
                FontFamily = Fonts.ChromeFamily,
                FontSize = TypeScale.Control,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            LoadoutPages.Themed(where, TextBlock.ForegroundProperty, ThemeManager.AKey);

            var note = MaterialsPage.Chrome(
                places.Kind == MaterialPlaceKind.Body ? $"{_material.Name} · {place.Note}" : place.Note,
                ThemeManager.GreyKey);

            var distance = MaterialsPage.Mono(
                place.Distance is { } ly ? ly.ToString("N1", CultureInfo.InvariantCulture) + " LY" : "—",
                ThemeManager.WhiteKey);

            rows.Children.Add(Row("*,200,110", where, note, distance));
        }

        return rows;
    }

    /// <summary><c>kind | give | › | get</c>, quantities in mono A.</summary>
    private Border TradeRow(MaterialTradeIn trade)
    {
        var kind = MaterialsPage.Chrome(
            trade.Direction switch
            {
                TradeDirection.Up => "Trade up",
                TradeDirection.Down => "Trade down",
                _ => "Across categories",
            },
            ThemeManager.GreyKey);

        var from = trade.From?.Name ?? $"Any {_material.Category} G{MaterialsPage.Count(trade.FromGrade)}";

        var arrow = MaterialsPage.Chrome("›", ThemeManager.GreyKey);

        return Row(
            "160,*,30,*",
            kind,
            Quantity(trade.Give, from),
            arrow,
            Quantity(trade.Get, _material.Name));
    }

    private static StackPanel Quantity(int count, string name)
    {
        var number = MaterialsPage.Mono(MaterialsPage.Count(count), ThemeManager.AKey);
        var times = MaterialsPage.Mono("×", ThemeManager.AKey);
        times.FontSize = TypeScale.MetaSmall;

        var what = new TextBlock
        {
            Text = name,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(what, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { number, times, what },
        };
    }
}
