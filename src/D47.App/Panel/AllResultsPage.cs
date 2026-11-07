using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Search › All results: every result Find held when it was chosen, grouped by kind.</summary>
internal sealed class AllResultsPage : UserControl, IPageSummary
{
    public const string PageKey = "search.allresults";

    public const string RootName = "AllResultsPage";

    /// <summary>The most rows one group shows.</summary>
    public const int MostPerGroup = 20;

    private const double ColumnWidth = 900;
    private const double CompactHeight = 44;

    private readonly List<Border> _rows = [];

    public AllResultsPage(string query, IReadOnlyList<PanelFindResult> results, Action<PanelFindResult> open)
    {
        Name = RootName;
        Summary = $"{results.Count} {(results.Count == 1 ? "result" : "results")} for “{query}”, as Find held them.";

        var body = new StackPanel { Spacing = 28 };

        foreach (var group in Groups)
        {
            var rows = results.Where(group.Holds).Take(MostPerGroup).ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            var count = new TextBlock
            {
                Text = rows.Count.ToString(CultureInfo.InvariantCulture),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = ListRow.AsideSize,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            ThemeInk.Bind(count, TextBlock.ForegroundProperty, ThemeManager.AKey);

            var head = RoutingKit.Section(group.Head, count);
            head.Margin = default;

            var list = new StackPanel { Spacing = 2 };

            foreach (var result in rows)
            {
                var row = Row(result, group.Tall, open);
                _rows.Add(row);
                list.Children.Add(row);
            }

            body.Children.Add(new StackPanel { Spacing = 10, Children = { head, list } });
        }

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = Column(body),
        };

        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => _rows.FirstOrDefault()?.Focus(NavigationMethod.Directional));
    }

    public string Summary { get; }

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    private Border Row(PanelFindResult result, bool tall, Action<PanelFindResult> open)
    {
        var lines = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { ListRow.Name(new TextBlock { Text = result.Title, TextTrimming = TextTrimming.CharacterEllipsis }) },
        };

        if (tall)
        {
            lines.Children.Add(ListRow.Sub(new TextBlock { Text = result.Path, TextTrimming = TextTrimming.CharacterEllipsis }));
        }

        var dock = new DockPanel();

        if (tall && Kind(result.Kind) is { } word)
        {
            var kind = new TextBlock { Text = word, FontFamily = Fonts.ChromeFamily, FontSize = ListRow.AsideSize, FontWeight = FontWeight.Medium };
            ThemeInk.Bind(kind, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            Dock(dock, kind);
        }
        else if (result.LightYears is { } lightYears && result.Kind is PanelFindKind.Station or PanelFindKind.Carrier)
        {
            var distance = ListRow.Aside(new TextBlock { Text = lightYears.ToString("N1", CultureInfo.InvariantCulture) + " LY" });
            distance.FontFamily = new FontFamily(Fonts.MonoFamily);
            Dock(dock, distance);
        }

        dock.Children.Add(lines);

        var row = ListRow.Dress(new Border { Tag = result.Title, Child = dock });

        if (!tall)
        {
            row.MinHeight = CompactHeight;
            row.Padding = new Thickness(14, 0);
        }

        return ListRow.Opens(row, () =>
        {
            foreach (var other in _rows)
            {
                other.Classes.Set(ListRow.SelectedClass, ReferenceEquals(other, row));
            }

            open(result);
        });
    }

    /// <summary>Holds <paramref name="body"/> at the left, as wide as the page up to <see cref="ColumnWidth"/>.</summary>
    private static Grid Column(Control body)
    {
        var column = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(1, GridUnitType.Star) { MaxWidth = ColumnWidth }, new ColumnDefinition(0, GridUnitType.Auto)],
        };
        column.Children.Add(body);

        return column;
    }

    private static void Dock(DockPanel dock, TextBlock aside)
    {
        aside.VerticalAlignment = VerticalAlignment.Center;
        aside.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(aside, Avalonia.Controls.Dock.Right);
        dock.Children.Add(aside);
    }

    private static string? Kind(PanelFindKind kind) => kind switch
    {
        PanelFindKind.Page => "PAGE",
        PanelFindKind.SettingsPlace => "PLACE",
        PanelFindKind.SettingRow => "SETTING",
        PanelFindKind.HelpPage => "HELP",
        _ => null,
    };

    private sealed record Group(string Head, bool Tall, Func<PanelFindResult, bool> Holds);

    private static readonly Group[] Groups =
    [
        new("PAGES, SETTINGS AND HELP", true, r => r.Kind is PanelFindKind.Page or PanelFindKind.SettingsPlace or PanelFindKind.SettingRow or PanelFindKind.HelpPage),
        new("STAR SYSTEMS", false, r => r.Kind == PanelFindKind.StarSystem),
        new("STATIONS", false, r => r.Kind == PanelFindKind.Station),
        new("FLEET CARRIERS", false, r => r.Kind == PanelFindKind.Carrier),
        new("MINOR FACTIONS", false, r => r.Kind == PanelFindKind.Faction),
        new("ENGINEERS", false, r => r.Kind == PanelFindKind.Engineer),
        new("BOOKMARKS", true, r => r.Kind == PanelFindKind.Bookmark),
    ];
}
