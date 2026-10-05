using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Fleet › Crew: the hired pilots, their combat rank, fighter-bay duty and posted ship (#564).</summary>
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

    /// <summary>The table's columns: pilot, combat rank, fighter bay, posted to.</summary>
    private const string Columns = "*,160,170,*";

    private readonly Func<CommanderGameState?> _state;
    private readonly JournalClock _clock;
    private readonly StackPanel _body = new();
    private object? _seen;

    public CrewPage(Func<CommanderGameState?> state)
    {
        _state = state;
        _clock = new JournalClock(() => state()?.Session.LastEventAt);

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

    /// <summary>Redraws when the crew changes.</summary>
    public bool Tick()
    {
        var changed = _clock.Tick();

        if (ReferenceEquals(Stamp(), _seen))
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

        var crew = _state()?.Crew;

        _body.Children.Clear();
        _body.Children.Add(TitleText.Block(
            TitleText.Build("Crew", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Hired pilots")));

        if (crew is not { Any: true })
        {
            var message = LoadoutPages.Muted(Unseen);
            message.Margin = new Thickness(0, 18, 0, 0);
            _body.Children.Add(message);
            return;
        }

        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var member in crew.Members)
        {
            rows.Children.Add(Row(member));
        }

        _body.Children.Add(new StackPanel { Margin = new Thickness(0, 28, 0, 0), Children = { ColumnHeads(), rows } });
    }

    private static Control ColumnHeads()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), Height = 32 };

        foreach (var (text, column) in new[] { ("Pilot", 0), ("Combat rank", 1), ("Fighter bay", 2), ("Posted to", 3) })
        {
            var head = MaterialsPage.Chrome(text, ThemeManager.GreyKey);
            head.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(head, column);
            grid.Children.Add(head);
        }

        return new Border { Padding = new Thickness(14, 0), Child = grid };
    }

    private static Border Row(CrewMember member)
    {
        var name = new TextBlock
        {
            Text = member.Name,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        LoadoutPages.Themed(name, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var rank = Cell(member.CombatRank is { Length: > 0 } combat ? combat : "—", 1,
            member.CombatRank is { Length: > 0 } ? ThemeManager.AKey : ThemeManager.GreyKey);
        var duty = Cell(Duty(member), 2, member.Active ? ThemeManager.YellowKey : ThemeManager.GreyKey);
        var posted = Cell(Posting(member), 3, member.PostedTo is { Length: > 0 } ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(Columns),
                Children = { name, rank, duty, posted },
            },
        };
        LoadoutPages.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Cell(string text, int column, string key)
    {
        var block = MaterialsPage.Chrome(text, key);
        block.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(block, column);
        return block;
    }
}
