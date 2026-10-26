using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Fleet › Stored modules: every module in storage, grouped by the system holding it (#563).</summary>
public sealed class StoredModulesPage : UserControl, IFilterablePage
{
    public const string RootKey = "loadout.stored";

    /// <summary>The footer's phrase; the model answers it with <c>get_stored_modules</c>.</summary>
    public const string Phrase = "where is my fuel scoop";

    public const string Unseen =
        "I have not seen your stored modules yet. The game lists them when you open Outfitting at a station.";

    public const string Nothing = "Nothing is in storage.";

    public const string Free = "FREE";

    public const string InTransit = "In transit";

    public const string TableId = "StoredModules";

    private static readonly TableColumn[] ColumnList =
    [
        new("Module", ColumnFit.Fill),
        new("Class"),
        new("TransferCost"),
        new("TransferTime"),
    ];

    /// <summary>The modules held in one system. <see cref="Here"/> is the system the snapshot was taken in.</summary>
    public sealed record Group(
        string System,
        string? Station,
        bool Here,
        bool InCurrentSystem,
        IReadOnlyList<StoredModule> Modules);

    public static NavCrumb Crumb => new(RootKey, "Stored modules");

    /// <summary>One module in a table row, with whether its group is the snapshot's system.</summary>
    public readonly record struct Held(StoredModule Module, bool Here);

    /// <summary>The table's columns, each sortable from its header; the sort orders rows within each group.</summary>
    public static readonly IReadOnlyList<SortColumn<Held>> ColumnHeadings =
    [
        SortColumn<Held>.Text("Module", row => row.Module.Name),
        SortColumn<Held>.Figure("Class", row => ClassRank(row.Module.Class), SortDirection.Descending),
        SortColumn<Held>.Figure(
            "Transfer cost",
            row => row.Module.TransferCost is > 0 and var cost ? cost : row.Here ? 0L : (long?)null,
            SortDirection.Ascending),
        SortColumn<Held>.Figure(
            "Transfer time",
            row => row.Module.TransferTime is > 0 and var seconds ? seconds : (int?)null,
            SortDirection.Ascending),
    ];

    /// <summary>The default order: by module name.</summary>
    public static readonly SortOrder ByName = new(0, SortDirection.Ascending);

    /// <summary>Size and rating as one number, so 5A outranks 5B and 6E outranks 5A; null where there is no class.</summary>
    public static int? ClassRank(string? moduleClass)
    {
        if (moduleClass is not { Length: > 1 } || !int.TryParse(moduleClass[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
        {
            return null;
        }

        var rating = "EDCBA".IndexOf(char.ToUpperInvariant(moduleClass[^1]), StringComparison.Ordinal);

        return rating < 0 ? null : (size * 10) + rating + 1;
    }

    private readonly Func<CommanderGameState?> _state;
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();
    private readonly SortHeader<Held> _head = new("StoredHead", ColumnHeadings, ByName);
    private readonly ColumnWidthMemory? _columnWidths;
    private TableLayout _table;
    private object? _seen;
    private string _query = string.Empty;

    public StoredModulesPage(Func<CommanderGameState?> state, ColumnWidthMemory? columnWidths = null)
    {
        _state = state;
        _columnWidths = columnWidths;
        _table = new TableLayout(TableId, ColumnList, columnWidths);
        _clock = new JournalClock(() => state()?.Session.LastEventAt);
        _head.Changed += Draw;

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new PageFooter(Phrase, _clock) { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(LoadoutPages.Scrolling(new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 1000 }, new ColumnDefinition(GridLength.Auto)],
            Children = { _body },
        }));

        Content = root;
        Draw();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _head.Reset();
    }

    public bool Filters => Store is { IsKnown: true, Modules.Count: > 0 };

    public string FilterPlaceholder => "Filter by module name";

    public void Filter(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (string.Equals(trimmed, _query, StringComparison.Ordinal))
        {
            return;
        }

        _query = trimmed;
        Draw();
    }

    /// <summary>Redraws when the stored modules or the current system have changed.</summary>
    public bool Tick()
    {
        var changed = _clock.Tick();

        if (Equals(Stamp(), _seen))
        {
            return changed;
        }

        Draw();
        return true;
    }

    /// <summary>The empty state while a filter hides every module.</summary>
    public static string NoMatch(string query) => $"No stored module matches “{query}”.";

    /// <summary>
    /// The stored modules whose name contains <paramref name="query"/>, one group per system: the snapshot's
    /// system first, then the nearest by transfer time, with modules in transit last.
    /// </summary>
    public static IReadOnlyList<Group> Groups(CommanderGameState? state, string query = "")
    {
        if (state is null)
        {
            return [];
        }

        var store = state.Modules;
        var current = state.Location.StarSystem;
        var wanted = query.Trim();

        var shown = store.Modules
            .Where(module => wanted.Length == 0 || module.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var held = shown
            .Where(module => !module.InTransit)
            .GroupBy(module => module.StarSystem, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var here = Same(group.Key, store.SnapshotSystem);

                return new Group(
                    group.Key,
                    here ? store.SnapshotStation : group.Select(module => module.StationName).FirstOrDefault(name => name is not null),
                    here,
                    Same(group.Key, current),
                    [.. group.OrderBy(module => module.Name, StringComparer.CurrentCultureIgnoreCase)]);
            })
            .OrderByDescending(group => group.Here)
            .ThenBy(group => group.Modules.Min(module => module.TransferTime ?? int.MaxValue))
            .ThenBy(group => group.System, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var moving = shown.Where(module => module.InTransit).ToList();

        if (moving.Count > 0)
        {
            held.Add(new Group(
                InTransit,
                null,
                false,
                false,
                [.. moving.OrderBy(module => module.Name, StringComparer.CurrentCultureIgnoreCase)]));
        }

        return held;
    }

    /// <summary>The group head's line after the system: station, "here" for the snapshot's system, and the count.</summary>
    public static string Detail(Group group)
    {
        var count = group.Modules.Count;
        var parts = new List<string>();

        if (group.Station is { Length: > 0 } station)
        {
            parts.Add(station);
        }

        if (group.Here)
        {
            parts.Add("here");
        }

        parts.Add($"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "MODULE" : "MODULES")}");

        return string.Join(" · ", parts);
    }

    /// <summary>The transfer cost: credits, FREE where the module is here, or a dash where the journal gave none.</summary>
    public static string Cost(StoredModule module, bool here) =>
        module.TransferCost is > 0 and var cost ? $"{cost.ToString("N0", CultureInfo.InvariantCulture)} CR"
        : here ? Free
        : "—";

    /// <summary>The transfer time as HH:MM, or a dash where there is none.</summary>
    public static string Time(StoredModule module) =>
        module.TransferTime is > 0 and var seconds
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600:00}:{seconds / 60 % 60:00}")
            : "—";

    private ModuleStore Store => _state()?.Modules ?? ModuleStore.Empty;

    private object Stamp()
    {
        var state = _state();
        return (state?.Modules, state?.Location.StarSystem);
    }

    private static bool Same(string? left, string? right) =>
        left is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private void Draw()
    {
        _seen = Stamp();

        var state = _state();
        var groups = Groups(state, _query);
        var count = groups.Sum(group => group.Modules.Count);

        _table = new TableLayout(TableId, ColumnList, _columnWidths);
        _body.Children.Clear();
        _body.Children.Add(TitleText.Block(
            TitleText.Build("Stored modules", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("In storage, by where it's stored"),
            TitleText.Figure("Modules", count.ToString(CultureInfo.InvariantCulture))));

        if (!Store.IsKnown)
        {
            _body.Children.Add(Message(Unseen));
            return;
        }

        if (Store.Modules.Count == 0)
        {
            _body.Children.Add(Message(Nothing));
            return;
        }

        if (groups.Count == 0)
        {
            _body.Children.Add(Message(NoMatch(_query)));
            return;
        }

        foreach (var group in groups)
        {
            _body.Children.Add(Section(group));
        }
    }

    private Control Section(Group group)
    {
        var name = TitleText.Build(group.System, TypeScale.Section, TitleRank.Group);
        name.VerticalAlignment = VerticalAlignment.Center;
        LoadoutPages.Themed(
            name,
            TextBlock.ForegroundProperty,
            group.InCurrentSystem ? ThemeManager.CyanKey
            : group.System == InTransit ? ThemeManager.GreyKey
            : ThemeManager.AKey);

        var detail = new TextBlock
        {
            Text = Detail(group),
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };
        LoadoutPages.Themed(detail, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var head = new WrapPanel { ItemSpacing = 12, LineSpacing = 4, Children = { name, detail } };

        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var row in _head.Sort([.. group.Modules.Select(module => new Held(module, group.Here))]))
        {
            rows.Children.Add(Row(row.Module, row.Here, _table));
        }

        return new StackPanel
        {
            Margin = new Thickness(0, 28, 0, 0),
            Children = { TitleText.GroupRow(head), ColumnHeads(), rows },
        };
    }

    private Control ColumnHeads()
    {
        var grid = _table.Header(
            _head,
            [HorizontalAlignment.Left, HorizontalAlignment.Left, HorizontalAlignment.Right, HorizontalAlignment.Right]);
        grid.MinHeight = TypeScale.MinimumTarget;

        return new Border { Padding = new Thickness(14, 0), Child = grid };
    }

    private static Border Row(StoredModule module, bool here, TableLayout table)
    {
        var name = new TextBlock
        {
            Text = module.Name,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var rating = MaterialsPage.Mono(module.Class ?? "—", module.Class is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
        rating.HorizontalAlignment = HorizontalAlignment.Left;

        var costText = Cost(module, here);
        var cost = MaterialsPage.Mono(costText, costText == "—" ? ThemeManager.GreyKey : ThemeManager.AKey);

        var time = MaterialsPage.Mono(Time(module), ThemeManager.WhiteKey);

        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            Child = table.Row([name, rating, cost, time]),
        };
        LoadoutPages.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Message(string text)
    {
        var block = LoadoutPages.Muted(text);
        block.Margin = new Thickness(0, 18, 0, 0);
        return block;
    }
}
