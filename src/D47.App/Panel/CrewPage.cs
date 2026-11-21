using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// Fleet › Crew: the hired pilots, their combat rank, fighter-bay duty and posted ship (#564).
/// </summary>
public sealed class CrewPage : UserControl
{
    public const string RootKey = "loadout.crew";

    /// <summary>The footer's phrase; the model answers it with <c>describe_crew</c>.</summary>
    public const string Phrase = "who is my crew";

    public const string Unseen =
        "I find no hired pilot in your journals. The game lists a pilot when you hire, fire or reassign one.";

    public const string OnDuty = "ON FIGHTER DUTY";

    public const string OffDuty = "OFF DUTY";

    public const string NotPosted = "NOT POSTED";

    public const string TableId = "Crew";

    /// <summary>The width the title and message keep; the table fills the page.</summary>
    public const double Cap = 1000;

    /// <summary>The table's columns, each sortable from its header.</summary>
    public static readonly IReadOnlyList<SortColumn<CrewMember>> ColumnHeadings =
    [
        SortColumn<CrewMember>.Text("Pilot", member => member.Name),
        SortColumn<CrewMember>.Figure("Combat rank", member => member.CombatRank, SortDirection.Descending),
        SortColumn<CrewMember>.Figure("Fighter bay", member => (int?)(member.Active ? 1 : 0), SortDirection.Descending),
        SortColumn<CrewMember>.Text("Posted to", member => member.PostedTo),
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly ArrangedTable<CrewMember> _table;
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();
    private readonly SpeakerPictures? _pictures;
    private object? _seen;

    /// <summary>Each hired pilot's row carries a picture chooser when <paramref name="pictures"/> is given.</summary>
    public CrewPage(
        Func<CommanderGameState?> state,
        SpeakerPictures? pictures = null,
        ColumnWidthMemory? columnWidths = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _state = state;
        _pictures = pictures;
        _table = new ArrangedTable<CrewMember>(TableId, Fields(), columnArrangements, columnWidths, headName: "CrewHead");
        _clock = new JournalClock(() => state()?.Session.LastEventAt);
        _table.Changed += Draw;

        Content = PageFooter.Shell(_body, Phrase, _clock);
        Draw();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _table.Reopen();
    }

    /// <summary>Redraws when the crew changes.</summary>
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

    /// <summary>The fighter-bay cell's text.</summary>
    public static string Duty(CrewMember member) => member.Active ? OnDuty : OffDuty;

    /// <summary>The posted-to cell's text.</summary>
    public static string Posting(CrewMember member) => member.PostedTo is { Length: > 0 } ship ? ship : NotPosted;

    private object? Stamp() => _state()?.Crew;

    private void Draw()
    {
        _seen = Stamp();

        _body.Children.Clear();
        _body.Children.Add(PageFooter.Capped(TitleText.Block(
            TitleText.Build("Crew", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Hired pilots")), Cap));

        DrawPilots();
    }

    private void DrawPilots()
    {
        var crew = _state()?.Crew;

        if (crew is not { Any: true })
        {
            var message = LoadoutPages.Muted(Unseen);
            message.Margin = new Thickness(0, 18, 0, 0);
            _body.Children.Add(PageFooter.Capped(message, Cap));
            return;
        }

        _table.Refresh();
        _body.Children.Add(new StackPanel { Margin = new Thickness(0, 28, 0, 0), Children = { LedgerTables.Arranger(_table, crew.Members) } });

        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 0), IsVisible = !ColumnFilter.HidesRows(_table) };

        if (ColumnFilter.Empty(_table, crew.Members) is { } empty)
        {
            rows.Children.Add(empty);
        }

        foreach (var member in _table.Shown(crew.Members))
        {
            rows.Children.Add(Row(member));
        }

        _body.Children.Add(LedgerTables.Sideways(_table, new StackPanel { Children = { ColumnHeads(), rows } }));
    }

    private Control ColumnHeads() =>
        new Border { Padding = new Thickness(14, 0), Child = LedgerTables.Head(_table, default) };

    private IReadOnlyList<TableField<CrewMember>> Fields() =>
    [
        StarSystemPage.Headed(ColumnHeadings[0], new TableColumn("Pilot", ColumnFit.Fill), NameCell),
        StarSystemPage.Headed(
            ColumnHeadings[1],
            new TableColumn("CombatRank"),
            member => Cell(member.CombatRankName ?? "—", member.CombatRankName is not null ? ThemeManager.AKey : ThemeManager.GreyKey))
            with { Filters = [ValueLists.One<CrewMember>("Combat rank", member => member.CombatRankName, "No combat rank")] },
        StarSystemPage.Headed(
            ColumnHeadings[2], new TableColumn("FighterBay"), member => Cell(Duty(member), member.Active ? ThemeManager.YellowKey : ThemeManager.GreyKey))
            with { Filters = [ValueLists.One<CrewMember>("Fighter bay", Duty, "No duty")] },
        StarSystemPage.Headed(
            ColumnHeadings[3],
            new TableColumn("PostedTo", ColumnFit.Fill),
            member => Cell(Posting(member), member.PostedTo is { Length: > 0 } ? ThemeManager.WhiteKey : ThemeManager.GreyKey))
            with { Filters = [ValueLists.One<CrewMember>("Posted to", Posting, "Not posted")] },
        LedgerTables.Added<CrewMember>(
            "HullLeft",
            new TableColumn("HullLeft"),
            member => TableCells.Flag(member.HullLeft),
            SortColumn<CrewMember>.Figure<int>("Ship changed", member => member.HullLeft ? 1 : 0, SortDirection.Descending))
            with { Filters = [ValueLists.Flag<CrewMember>("Ship changed", member => member.HullLeft, "Not known")] },
    ];

    private static Control NameCell(CrewMember member)
    {
        var name = new TextBlock
        {
            Text = member.Name,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return name;
    }

    private Border Row(CrewMember member)
    {
        var line = _table.Row(member);
        line.Height = TypeScale.MinimumTarget;

        var content = new StackPanel { Children = { line } };

        if (_pictures is not null)
        {
            content.Children.Add(new PictureChooser(_pictures, SpeakerPictures.Crew(member.CrewId), 120) { Margin = new Thickness(0, 0, 0, 8) });
        }

        var row = new Border { Padding = new Thickness(14, 0), Child = content };
        ThemeInk.Bind(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Cell(string text, string key)
    {
        var block = MaterialsPage.Chrome(text, key);
        block.HorizontalAlignment = HorizontalAlignment.Left;
        return block;
    }
}
