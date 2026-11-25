using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Astrometrics › Plan › Saved routes: the routes the shown Commander kept, each as it was plotted.</summary>
public sealed class SavedRoutesPage : UserControl, IPageSummary
{
    public const string TableId = "Routing.SavedRoutes";

    public const string SummaryText = "Routes you saved, kept as they were plotted.";

    public const string EmptyText = "No saved routes yet. Press Save this route on any plan's result, and it is kept here as it was plotted.";

    public const string ConfirmText = "Delete this saved route?";

    /// <summary>The key of the column the default order follows.</summary>
    public const string PlottedKey = "Plotted";

    public const string PreviousName = "SavedRoutesPrevious";

    public const string NextName = "SavedRoutesNext";

    public const string OpenPrefix = "SavedRoute.Open.";

    public const string DeletePrefix = "SavedRoute.Delete.";

    public const string ConfirmPrefix = "SavedRoute.ConfirmDelete.";

    public const string KeepPrefix = "SavedRoute.Keep.";

    private readonly SavedRouteBook _book;
    private readonly PanelNavigator _nav;
    private readonly bool _headset;
    private readonly ArrangedTable<SavedRoute> _columns;
    private readonly StackPanel _body = new() { Spacing = 8 };
    private string? _asking;
    private int _page;

    public SavedRoutesPage(
        SavedRouteBook book,
        PanelNavigator nav,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _book = book;
        _nav = nav;
        _headset = headset;

        _columns = new ArrangedTable<SavedRoute>(
            TableId, Fields(), columnArrangements, headset ? null : columnWidths, headset ? 12 : 16, PlottedKey, "SavedRoutesHead");
        _columns.Sorted += () => _page = 0;
        _columns.FilterChanged += () => _page = 0;
        _columns.Changed += Draw;

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _body } });
        Draw();
    }

    public string Summary => SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The planner's name as a Commander knows it.</summary>
    public static string KindName(RoutePlanKind kind) => kind switch
    {
        RoutePlanKind.Jump => "Neutron Plotter",
        RoutePlanKind.Riches => "Road to Riches",
        RoutePlanKind.Exobiology => "Exobiology",
        RoutePlanKind.Carrier => "Carrier Route",
        RoutePlanKind.Trade => "Trade",
        RoutePlanKind.Outfitting => "Outfitting",
        _ => kind.ToString(),
    };

    /// <summary>"7 saved routes".</summary>
    public static string CountText(int count) => count == 1 ? "1 saved route" : $"{count} saved routes";

    public static string PlottedText(DateTimeOffset at) =>
        at == default ? "—" : at.ToLocalTime().ToString("d MMM yyyy · HH:mm", CultureInfo.InvariantCulture);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _book.Changed += OnChanged;
        _columns.Reopen();
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _book.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Draw);

    private IReadOnlyList<TableField<SavedRoute>> Fields() =>
    [
        StarSystemPage.Headed(
            SortColumn<SavedRoute>.Text("Kind", route => KindName(route.Kind)),
            new TableColumn("Kind", ColumnFit.Content),
            route => CreditsKit.Line(KindName(route.Kind), Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey))
            with { Filters = [ValueLists.One<SavedRoute>("Kind", route => KindName(route.Kind), "No kind")] },
        StarSystemPage.Headed(
            SortColumn<SavedRoute>.Text("Name", route => route.Name),
            new TableColumn("Name", ColumnFit.Fill),
            NameCell),
        StarSystemPage.Headed(
            SortColumn<SavedRoute>.Figure(PlottedKey, route => (long?)route.PlottedAt.UtcTicks, SortDirection.Descending),
            new TableColumn(PlottedKey),
            route => CreditsKit.Mono(PlottedText(route.PlottedAt), TypeScale.Small, ThemeManager.WhiteKey, right: true),
            right: true),
        LedgerTables.Control(
            new TableColumn("Actions", ColumnFit.Fixed) { Fixed = _headset ? 250 : 270 },
            ActionsCell,
            SortColumn<SavedRoute>.Plain(string.Empty),
            right: true),
    ];

    private static TextBlock NameCell(SavedRoute route)
    {
        var name = CreditsKit.Line(route.Name, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);
        name.TextTrimming = Avalonia.Media.TextTrimming.None;
        name.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        return name;
    }

    private Control ActionsCell(SavedRoute route)
    {
        if (_asking == route.Id)
        {
            return Confirm(route);
        }

        var open = new Button { Name = OpenPrefix + route.Id, Content = "Open", Height = TypeScale.MinimumTarget };
        open.Click += (_, _) => _nav.Drill(RoutingPages.SavedRouteCrumb(route.Id, route.Name));
        Avalonia.Automation.AutomationProperties.SetName(open, "Open " + route.Name);

        var delete = new Button
        {
            Name = DeletePrefix + route.Id,
            Content = "Delete",
            Height = TypeScale.MinimumTarget,
            Classes = { Settings.SettingsView.DestructiveClass },
        };
        delete.Click += (_, _) =>
        {
            _asking = route.Id;
            Draw();
        };
        Avalonia.Automation.AutomationProperties.SetName(delete, "Delete " + route.Name);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { open, delete },
        };
    }

    private Control Confirm(SavedRoute route)
    {
        var delete = new Button
        {
            Name = ConfirmPrefix + route.Id,
            Content = "Delete",
            Height = TypeScale.MinimumTarget,
            Classes = { Settings.SettingsView.DestructiveClass },
        };
        delete.Click += (_, _) =>
        {
            _asking = null;
            _book.Delete(route.Id);
        };

        var keep = new Button { Name = KeepPrefix + route.Id, Content = "Keep", Height = TypeScale.MinimumTarget };
        keep.Click += (_, _) =>
        {
            _asking = null;
            Draw();
        };

        return new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                RoutingKit.Ink(ConfirmText, TypeScale.Small, ThemeManager.WhiteKey),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { delete, keep } },
            },
        };
    }

    private void Draw()
    {
        var routes = _book.For();

        if (_asking is not null && routes.All(route => route.Id != _asking))
        {
            _asking = null;
        }

        _body.Children.Clear();
        _columns.Refresh();

        if (routes.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(EmptyText));

            return;
        }

        _body.Children.Add(CreditsKit.Mono(CountText(routes.Count), TypeScale.Small, ThemeManager.GreyKey, right: false));

        var sorted = _columns.Shown(routes);
        IReadOnlyList<SavedRoute> page = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            page = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        var list = new StackPanel { Spacing = Gaps.Tile };
        list.Children.Add(LedgerTables.Head(_columns, new Thickness(LedgerTables.RowInset, 0)));

        if (ColumnFilter.Empty(_columns, routes) is { } empty)
        {
            list.Children.Add(empty);
        }

        foreach (var route in page)
        {
            var view = RowView(route);
            view.IsVisible = !ColumnFilter.HidesRows(_columns);
            list.Children.Add(view);
        }

        _body.Children.Add(LedgerTables.Arranger(_columns, routes));
        _body.Children.Add(LedgerTables.Sideways(_columns, list));

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            _body.Children.Add(Pager(sorted.Count));
        }
    }

    private Border RowView(SavedRoute route)
    {
        var grid = _columns.Row(route);
        grid.VerticalAlignment = VerticalAlignment.Center;

        return ListRow.Dress(new Border
        {
            Name = "SavedRoute." + route.Id,
            Child = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0, 0, 0),
        });
    }

    private Grid Pager(int total)
    {
        var readout = CreditsKit.Mono(FactionModel.PageReadout(_page, total), TypeScale.Tip, ThemeManager.GreyKey, right: false);
        readout.VerticalAlignment = VerticalAlignment.Center;

        var previous = new Button { Name = PreviousName, Content = "◄ PREVIOUS", Height = TypeScale.MinimumTarget, IsEnabled = _page > 0 };
        var next = new Button
        {
            Name = NextName,
            Content = "NEXT ►",
            Height = TypeScale.MinimumTarget,
            IsEnabled = FactionModel.ClampPage(_page + 1, total) > _page,
        };
        previous.Click += (_, _) =>
        {
            _page--;
            Draw();
        };
        next.Click += (_, _) =>
        {
            _page++;
            Draw();
        };

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { previous, next } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(readout);
        grid.Children.Add(tiles);

        return grid;
    }
}
