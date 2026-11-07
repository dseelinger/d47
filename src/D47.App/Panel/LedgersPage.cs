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

/// <summary>Commander › Ledgers: one pressable row per ledger this surface declared under it; the open one is drawn below its row (#1291).</summary>
public sealed class LedgersPage : UserControl, IPageSummary
{
    public const string RootKey = "ledgers";

    public const string SummaryText = "Your record from the journals, one ledger at a time.";

    private const double NameWidth = 180;

    private const double RowHeight = 56;

    private static readonly (string Key, string Description)[] Known =
    [
        (BgsWorkPage.PageKey, "The work done for minor factions since the BGS tick."),
        (CombatStatsPage.PageKey, "Your kills and the credits they were awarded."),
        (CreditsPage.PageKey, "Where your credits came from and where they went."),
        (CodexPage.PageKey, "The codex entries you have logged, by region."),
        (SessionPage.RootKey, "What you have earned and done since the game started."),
    ];

    /// <param name="nav">The surface's navigator; a ledger is open while it is the trail's leaf.</param>
    /// <param name="content">Draws an open ledger's page.</param>
    /// <param name="open">The key of the ledger drawn open, or null.</param>
    public LedgersPage(PanelNavigator nav, Func<NavCrumb, Control>? content = null, string? open = null)
    {
        var list = new StackPanel { Spacing = Gaps.Tile };

        foreach (var row in Rows(nav))
        {
            var isOpen = content is not null && row.Crumb.Key == open;

            list.Children.Add(Row(row, isOpen, () => _ = isOpen ? nav.Back() : nav.Drill(row.Crumb)));

            if (isOpen)
            {
                list.Children.Add(content!(row.Crumb));
            }
        }

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(list);
    }

    /// <summary>A ledger's crumb: the level that is drawn as the Ledgers page with this ledger open.</summary>
    public static NavCrumb Ledger(string key, string word) => new(key, word) { Whole = true, Level = RootKey };

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

    private static Button Row(LedgerRow row, bool open, Action pressed)
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

        ThemeInk.Bind(description, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var chevron = new TextBlock
        {
            Text = open ? "−" : "›",
            FontSize = TypeScale.Figure,
            VerticalAlignment = VerticalAlignment.Center,
        };

        ThemeInk.Bind(chevron, TextBlock.ForegroundProperty, ThemeManager.AKey);

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
        }, open);

        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => pressed();

        return button;
    }
}
