using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>One ledger on the Ledgers list: the page it drills to and what it holds.</summary>
public sealed record LedgerRow(NavCrumb Crumb, string Description)
{
    public string Name => Crumb.Word;
}

/// <summary>Commander › Ledgers: one pressable row per ledger this surface declared under it (#1291).</summary>
public sealed class LedgersPage : UserControl, IPageSummary
{
    public const string RootKey = "ledgers";

    public const string SummaryText = "Your record from the journals, one ledger to a page.";

    private const double NameWidth = 180;

    private const double RowHeight = 56;

    private static readonly (string Key, string Description)[] Known =
    [
        (BgsWorkPage.PageKey, "The work done for minor factions since the BGS tick."),
        (CombatStatsPage.PageKey, "Your kills and the credits they were awarded."),
        (CreditsPage.PageKey, "Where your credits came from and where they went."),
        (CodexPage.PageKey, "The codex entries you have logged, by region."),
    ];

    public LedgersPage(PanelNavigator nav)
    {
        var list = new StackPanel { Spacing = Gaps.Tile };

        foreach (var row in Rows(nav))
        {
            list.Children.Add(Row(row, () => nav.Drill(row.Crumb)));
        }

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(list);
    }

    public static NavCrumb Root => new(RootKey, "Ledgers")
    {
        Help = D47.Core.Capabilities.Builtin.JournalCapability.Id,
    };

    public string Summary => SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The ledgers declared under Ledgers on <paramref name="nav"/>, in the list's fixed order.</summary>
    public static IReadOnlyList<LedgerRow> Rows(PanelNavigator nav)
    {
        var declared = nav.Destinations
            .Where(destination => destination.Root.Key == RootKey && destination.SubPage is not null)
            .Select(destination => destination.SubPage!)
            .ToDictionary(crumb => crumb.Key, StringComparer.Ordinal);

        return
        [
            .. Known
                .Where(known => declared.ContainsKey(known.Key))
                .Select(known => new LedgerRow(declared[known.Key], known.Description)),
        ];
    }

    private static Button Row(LedgerRow row, Action pressed)
    {
        var name = ListRow.Name(new TextBlock
        {
            Text = row.Name,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var description = new TextBlock
        {
            Text = row.Description,
            FontSize = TypeScale.Description,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        LoadoutPages.Themed(description, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var chevron = new TextBlock
        {
            Text = "›",
            FontSize = TypeScale.Figure,
            VerticalAlignment = VerticalAlignment.Center,
        };

        LoadoutPages.Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var cells = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{NameWidth},*,Auto"),
            ColumnSpacing = 16,
            Children = { name, description, chevron },
        };

        Grid.SetColumn(description, 1);
        Grid.SetColumn(chevron, 2);

        var button = ListRow.Dress(new Button
        {
            Name = "Ledger." + row.Crumb.Key,
            Content = cells,
            MinHeight = RowHeight,
            Padding = new Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        });

        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => pressed();

        return button;
    }
}
