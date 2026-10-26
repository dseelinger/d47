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
        "I have not seen you hire any crew this session. The game lists them when you hire, fire or reassign a pilot.";

    public const string OnDuty = "ON FIGHTER DUTY";

    public const string OffDuty = "OFF DUTY";

    public const string NotPosted = "NOT POSTED";

    public const string TableId = "Crew";

    private static readonly TableColumn[] ColumnList =
    [
        new("Pilot", ColumnFit.Fill),
        new("CombatRank"),
        new("FighterBay"),
        new("PostedTo", ColumnFit.Fill),
    ];

    private static readonly string[] CombatLadder =
        ["Harmless", "Mostly Harmless", "Novice", "Competent", "Expert", "Master", "Dangerous", "Deadly", "Elite"];

    /// <summary>The table's columns, each sortable from its header.</summary>
    public static readonly IReadOnlyList<SortColumn<CrewMember>> ColumnHeadings =
    [
        SortColumn<CrewMember>.Text("Pilot", member => member.Name),
        SortColumn<CrewMember>.Figure("Combat rank", member => RankOf(member.CombatRank), SortDirection.Descending),
        SortColumn<CrewMember>.Figure("Fighter bay", member => (int?)(member.Active ? 1 : 0), SortDirection.Descending),
        SortColumn<CrewMember>.Text("Posted to", member => member.PostedTo),
    ];

    /// <summary>The combat rank's place on the ladder, Harmless lowest; null where it is unrecorded or unknown.</summary>
    public static int? RankOf(string? combatRank)
    {
        if (combatRank is not { Length: > 0 })
        {
            return null;
        }

        var place = Array.FindIndex(CombatLadder, name => combatRank.StartsWith(name, StringComparison.OrdinalIgnoreCase));

        return place < 0 ? null : place;
    }

    private readonly Func<CommanderGameState?> _state;
    private readonly SortHeader<CrewMember> _head = new("CrewHead", ColumnHeadings);
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();
    private readonly SpeakerPictures? _pictures;
    private readonly ColumnWidthMemory? _columnWidths;
    private object? _seen;

    /// <summary>Each hired pilot's row carries a picture chooser when <paramref name="pictures"/> is given.</summary>
    public CrewPage(Func<CommanderGameState?> state, SpeakerPictures? pictures = null, ColumnWidthMemory? columnWidths = null)
    {
        _state = state;
        _pictures = pictures;
        _columnWidths = columnWidths;
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
        _body.Children.Add(TitleText.Block(
            TitleText.Build("Crew", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Hired pilots")));

        DrawPilots();
    }

    private void DrawPilots()
    {
        var crew = _state()?.Crew;

        if (crew is not { Any: true })
        {
            var message = LoadoutPages.Muted(Unseen);
            message.Margin = new Thickness(0, 18, 0, 0);
            _body.Children.Add(message);
            return;
        }

        var table = new TableLayout(TableId, ColumnList, _columnWidths);
        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var member in _head.Sort(crew.Members))
        {
            rows.Children.Add(Row(member, _pictures, table));
        }

        _body.Children.Add(new StackPanel { Margin = new Thickness(0, 28, 0, 0), Children = { ColumnHeads(table), rows } });
    }

    private Control ColumnHeads(TableLayout table)
    {
        var grid = table.Header(_head);
        grid.MinHeight = TypeScale.MinimumTarget;

        return new Border { Padding = new Thickness(14, 0), Child = grid };
    }

    private static Border Row(CrewMember member, SpeakerPictures? pictures, TableLayout table)
    {
        var name = new TextBlock
        {
            Text = member.Name,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var rank = Cell(member.CombatRank is { Length: > 0 } combat ? combat : "—",
            member.CombatRank is { Length: > 0 } ? ThemeManager.AKey : ThemeManager.GreyKey);
        var duty = Cell(Duty(member), member.Active ? ThemeManager.YellowKey : ThemeManager.GreyKey);
        var posted = Cell(Posting(member), member.PostedTo is { Length: > 0 } ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

        var line = table.Row([name, rank, duty, posted]);
        line.Height = TypeScale.MinimumTarget;

        var content = new StackPanel { Children = { line } };

        if (pictures is not null)
        {
            content.Children.Add(new PictureChooser(pictures, SpeakerPictures.Crew(member.CrewId), 120) { Margin = new Thickness(0, 0, 0, 8) });
        }

        var row = new Border { Padding = new Thickness(14, 0), Child = content };
        LoadoutPages.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Cell(string text, string key)
    {
        var block = MaterialsPage.Chrome(text, key);
        block.HorizontalAlignment = HorizontalAlignment.Left;
        return block;
    }
}
