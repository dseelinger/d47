using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Commanders: every Commander in the journals, with a switch to each.</summary>
public sealed class CommandersPage : UserControl
{
    public const string RootKey = "commanders";

    public const string Helper =
        "D47 starts with the commander who last logged in. Switch to another and D47 stays with them until you switch again or another commander logs in.";

    public const string NoneYet = "No commander has been found in the journals yet.";

    public const string TableId = "Commanders";

    /// <summary>The table's columns, each sortable from its header.</summary>
    public static readonly IReadOnlyList<SortColumn<CommanderSighting>> Heads =
    [
        SortColumn<CommanderSighting>.Text("NAME", sighting => CommanderRoster.Called(sighting.Name)),
        SortColumn<CommanderSighting>.Text("SHIP", sighting => sighting.Ship),
        SortColumn<CommanderSighting>.Text("LAST LOCATION", sighting => sighting.StarSystem),
        SortColumn<CommanderSighting>.Figure("LAST DETECTED", sighting => (DateTimeOffset?)sighting.LastSeen, SortDirection.Descending),
    ];

    /// <summary>The width the title, helper and tally keep; the table fills the page.</summary>
    public const double Cap = 1100;

    private readonly ArrangedTable<CommanderSighting> _table;
    private readonly CommanderRoster _roster;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly StackPanel _body = new();
    private (object? Sightings, string? Shown, int Files, bool OffDuty) _seen;

    public CommandersPage(
        CommanderRoster roster,
        ColumnWidthMemory? columnWidths = null,
        Func<string, Task<bool>>? copy = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _roster = roster;
        _copy = copy;
        _table = new ArrangedTable<CommanderSighting>(TableId, Fields(), columnArrangements, columnWidths, 16, "LastDetected", "CommandersHead");
        _table.Changed += Draw;

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children =
            {
                LoadoutPages.Scrolling(_body),
            },
        };

        Draw();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _table.Reopen();
    }

    /// <summary>Redraws when the list or the shown Commander has changed since the last draw.</summary>
    public bool Tick()
    {
        if (_roster.Stamp.Equals(_seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>The line under the list: <c>3 commanders · found in 41 journal files</c>.</summary>
    public static string Tally(int commanders, int files) => string.Create(
        CultureInfo.InvariantCulture,
        $"{commanders:N0} {(commanders == 1 ? "commander" : "commanders")} · found in {files:N0} journal {(files == 1 ? "file" : "files")}");

    private void Draw()
    {
        _seen = _roster.Stamp;
        _body.Children.Clear();

        _body.Children.Add(PageFooter.Capped(TitleText.Screen("Commanders"), Cap));

        var helper = Text(Helper, Fonts.ProseFamily, TypeScale.Body, ThemeManager.GreyKey);
        helper.TextWrapping = TextWrapping.Wrap;
        helper.Margin = new Thickness(0, 14, 0, 14);
        _body.Children.Add(PageFooter.Capped(helper, Cap));

        var commanders = _roster.Commanders;

        if (commanders.Count == 0)
        {
            _body.Children.Add(PageFooter.Capped(Text(NoneYet, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey), Cap));
            return;
        }

        _table.Refresh();
        _body.Children.Add(LedgerTables.Arranger(_table, commanders));

        var rows = new StackPanel { Spacing = 2, IsVisible = !ColumnFilter.HidesRows(_table) };
        rows.Children.Add(Header());

        if (ColumnFilter.Empty(_table, commanders) is { } empty)
        {
            rows.Children.Add(empty);
        }

        foreach (var sighting in _table.Shown(commanders))
        {
            rows.Children.Add(Row(sighting, Current(sighting)));
        }

        _body.Children.Add(LedgerTables.Sideways(_table, rows));

        var tally = Text(Tally(commanders.Count, _roster.FilesExamined), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey);
        tally.Margin = new Thickness(0, 14, 0, 0);
        _body.Children.Add(PageFooter.Capped(tally, Cap));
    }

    private bool Current(CommanderSighting sighting) =>
        string.Equals(sighting.FrontierId, _roster.ShownFrontierId, StringComparison.Ordinal);

    private TableGrid Header()
    {
        var grid = _table.Header();
        grid.Margin = new Thickness(14, 0, 4, 6);

        return grid;
    }

    private IReadOnlyList<TableField<CommanderSighting>> Fields() =>
    [
        StarSystemPage.Headed(Heads[0], new TableColumn("Name", ColumnFit.Fill), NameCell),
        StarSystemPage.Headed(Heads[1], new TableColumn("Ship"), sighting => Text(sighting.Ship ?? "—", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey))
            with { Filters = [ValueLists.One<CommanderSighting>("Ship", sighting => sighting.Ship, "No ship seen")] },
        StarSystemPage.Headed(Heads[2], new TableColumn("LastLocation", ColumnFit.Fill), LocationCell)
            with { Filters = [ValueLists.One<CommanderSighting>("Last location", sighting => sighting.StarSystem, "No location seen")] },
        StarSystemPage.Headed(Heads[3], new TableColumn("LastDetected"), sighting => Text(CommanderRoster.Detected(sighting.LastSeen), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey)),
        LedgerTables.Control<CommanderSighting>(new TableColumn("Switch", ColumnFit.Fixed) { Fixed = 140 }, ActionCell),
    ];

    private Control NameCell(CommanderSighting sighting)
    {
        var name = Text(CommanderRoster.Called(sighting.Name), Fonts.ChromeFamily, TypeScale.ControlLarge, Current(sighting) ? ThemeManager.CyanKey : ThemeManager.WhiteKey);
        name.FontWeight = FontWeight.SemiBold;

        return name;
    }

    private Control LocationCell(CommanderSighting sighting) =>
        CopyGlyph.LinkBeside(
            Text(sighting.StarSystem ?? "—", Fonts.ChromeFamily, TypeScale.ControlLarge, Current(sighting) ? ThemeManager.CyanKey : ThemeManager.AKey),
            sighting.StarSystem,
            _copy);

    private Control ActionCell(CommanderSighting sighting)
    {
        if (Current(sighting))
        {
            var mark = Text("✓ CURRENT", Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.CyanKey);
            mark.FontWeight = FontWeight.SemiBold;
            mark.LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking;
            mark.HorizontalAlignment = HorizontalAlignment.Center;

            return mark;
        }

        var button = new Button
        {
            Name = "CommanderSwitch",
            Content = "SWITCH",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        AutomationProperties.SetName(button, $"Switch to {sighting.Name}");
        button.Click += (_, _) => _roster.Pick(sighting);

        return button;
    }

    private Border Row(CommanderSighting sighting, bool current)
    {
        var cells = _table.Row(sighting);
        cells.VerticalAlignment = VerticalAlignment.Center;

        var row = new Border
        {
            MinHeight = 52,
            Padding = new Thickness(14, 4, 4, 4),
            Child = cells,
        };
        ThemeInk.Bind(row, Border.BackgroundProperty, current ? ThemeManager.CyanGroundKey : ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Text(string text, string family, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }

}
