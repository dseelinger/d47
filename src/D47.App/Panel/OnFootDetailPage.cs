using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Fleet › Materials › one on-foot resource: its kind, what the backpack and ship locker hold, who needs it,
/// where it is found and what a Bartender gives for it.
/// </summary>
public sealed class OnFootDetailPage : UserControl
{
    public const string BartenderHint = "What you give, and what you get.";

    /// <summary>In place of the exchange rows when nothing held pays for one.</summary>
    public const string NothingPays = "Nothing you hold pays for one at a Bartender.";

    private readonly GapSource _gap;
    private readonly PanelNavigator _nav;
    private readonly MaterialEntry _material;
    private readonly ContentControl _sidebar = new();
    private readonly StackPanel _body = new();

    public OnFootDetailPage(GapSource gap, PanelNavigator nav, MaterialEntry material, JournalClock clock)
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

    /// <summary>The footer's phrase; it is one of <c>find_micro_resource</c>'s examples.</summary>
    public static string Phrase(MaterialEntry material) => $"where do I find {material.Name.ToLowerInvariant()}";

    /// <summary>The hint in place of the exchange rows for a resource the Bartender does not exchange.</summary>
    public static string NoBartender(MaterialEntry material) => $"The Bartender does not exchange {material.Name}.";

    /// <summary>A list from the table joined for one cell, or a dash when it is empty.</summary>
    public static string Joined(IReadOnlyList<string> values) => values.Count > 0 ? string.Join(" · ", values) : "—";

    /// <summary>Redraws against the live plans and the live inventory.</summary>
    public void Refresh()
    {
        var report = _gap.Report();
        var ledgers = _gap.Tracker(report).Ship;
        var suit = _gap.State()?.Suit ?? SuitInventory.Empty;

        _sidebar.Content = MaterialsPage.SidebarOf(
            report, ledgers, suit, MaterialsPage.NeedsOf(_gap.Tracker(report)), _gap.View, Back);

        _body.Children.Clear();
        _body.Children.Add(Heading());

        if (MicroResourceGuide.For(_material.Symbol, suit) is not { } detail)
        {
            return;
        }

        _body.Children.Add(Facts(detail));

        var needs = report.Builds
            .Select(build => (Build: build, Line: build.Lines.FirstOrDefault(line =>
                string.Equals(line.Material.Symbol, _material.Symbol, StringComparison.OrdinalIgnoreCase))))
            .Where(pair => pair.Line is not null)
            .ToList();

        if (needs.Count > 0)
        {
            var rows = MaterialDetailPage.Rows();

            foreach (var (build, line) in needs)
            {
                rows.Children.Add(MaterialDetailPage.NeedRow(build, line!));
            }

            _body.Children.Add(MaterialDetailPage.Section("Needed by plans", MaterialDetailPage.NeededHint, rows));
        }

        if (detail.Settlements.Count + detail.Buildings.Count + detail.Containers.Count > 0)
        {
            var found = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 2 };
            found.Children.Add(MaterialDetailPage.Cell(0, "Settlements", Wrapped(detail.Settlements)));
            found.Children.Add(MaterialDetailPage.Cell(1, "Buildings", Wrapped(detail.Buildings)));
            found.Children.Add(MaterialDetailPage.Cell(2, "Containers", Wrapped(detail.Containers)));

            _body.Children.Add(MaterialDetailPage.Section("Where it's found", null, found));
        }

        if (_material.BarterCost is null)
        {
            _body.Children.Add(MaterialsPage.Hint(NoBartender(_material), new Thickness(0, 28, 0, 0)));
            return;
        }

        Control exchanges;

        if (detail.Exchanges.Count == 0)
        {
            exchanges = MaterialDetailPage.Slab(MaterialDetailPage.Prose(NothingPays, ThemeManager.GreyKey));
        }
        else
        {
            var rows = MaterialDetailPage.Rows();

            foreach (var exchange in detail.Exchanges)
            {
                rows.Children.Add(ExchangeRow(exchange));
            }

            exchanges = rows;
        }

        _body.Children.Add(MaterialDetailPage.Section("At a Bartender", BartenderHint, exchanges));
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
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    /// <summary>Goes back to the Materials page, on <paramref name="view"/>.</summary>
    internal void Back(string view)
    {
        _gap.View = view;
        _nav.JumpTo(0);
    }

    /// <summary><c>MATERIALS › SHIP LOCKER ›</c> after the view it was opened from, <c>MATERIALS › ON FOOT ›</c> otherwise.</summary>
    private Control Heading()
    {
        var crumbs = new WrapPanel();

        crumbs.Children.Add(MaterialDetailPage.Crumb("Materials", () => Back(MaterialsPage.NeededView)));
        crumbs.Children.Add(TitleText.Context(" › "));

        var view = _gap.View;

        crumbs.Children.Add(view switch
        {
            MaterialsPage.BackpackView => MaterialDetailPage.Crumb("Backpack", () => Back(view)),
            MaterialsPage.LockerView => MaterialDetailPage.Crumb("Ship locker", () => Back(view)),
            _ => TitleText.Context("On foot"),
        });

        crumbs.Children.Add(TitleText.Context(" ›"));

        var title = TitleText.Build(_material.Name, TypeScale.Title, TitleRank.Screen);

        return new StackPanel { Spacing = 2, Children = { crumbs, title } };
    }

    /// <summary>Kind, the backpack count with no cap, and the ship locker count against its cap with a gauge.</summary>
    private static Grid Facts(MicroResourceDetail detail)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,2*"),
            ColumnSpacing = 2,
            Margin = new Thickness(0, 18, 0, 0),
        };

        grid.Children.Add(MaterialDetailPage.Cell(0, "Kind", MaterialDetailPage.Value(detail.Kind)));
        grid.Children.Add(MaterialDetailPage.Cell(
            1, "In backpack", MaterialsPage.Mono(MaterialsPage.Thousands(detail.Backpack), ThemeManager.AKey)));

        var heading = new DockPanel();
        var figure = MaterialsPage.Mono(
            $"{MaterialsPage.Thousands(detail.Locker)} / {MaterialsPage.Thousands(detail.LockerCap)}",
            ThemeManager.YellowKey);
        DockPanel.SetDock(figure, Dock.Right);
        heading.Children.Add(figure);
        heading.Children.Add(MaterialsPage.Chrome("In ship locker", ThemeManager.GreyKey));

        var bar = MaterialsPage.CapacityBar(detail.LockerCap > 0 ? (double)detail.Locker / detail.LockerCap : 0);
        bar.Margin = new Thickness(0, 8, 0, 0);

        var tile = MaterialDetailPage.Tile(new StackPanel { Children = { heading, bar } });
        Grid.SetColumn(tile, 2);
        grid.Children.Add(tile);

        return grid;
    }

    private static TextBlock Wrapped(IReadOnlyList<string> values)
    {
        var block = MaterialDetailPage.Value(Joined(values));
        block.TextTrimming = Avalonia.Media.TextTrimming.None;
        block.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        return block;
    }

    /// <summary><c>SWAP | give × offered | › | get × this</c>, quantities in mono A.</summary>
    private Border ExchangeRow(MicroResourceExchange exchange) =>
        MaterialDetailPage.Row(
            "160,*,30,*",
            MaterialsPage.Chrome("Swap", ThemeManager.GreyKey),
            MaterialDetailPage.Quantity(exchange.Give, exchange.Offered.Name),
            MaterialsPage.Chrome("›", ThemeManager.GreyKey),
            MaterialDetailPage.Quantity(exchange.Get, _material.Name));
}
