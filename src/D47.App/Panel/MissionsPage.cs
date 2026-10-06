using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Missions: the live mission board, ranked as the spoken board ranks it, and the selected mission's detail.</summary>
public sealed class MissionsPage : UserControl, IPageSummary, IFilterablePage
{
    public const string RootKey = "missions";

    /// <summary>A <c>get_mission_board</c> command phrase.</summary>
    public const string Phrase = "read the mission board";

    public const string Nothing = "There are no missions on your board, Commander.";

    public const string NothingHint = "Missions on offer at a station are not in the journal. Accept one and it appears here.";

    public const string NoDetail = "Accepted before the oldest journal D47 has read. The game reports only its name and time left.";

    public const string RedirectedLine = "Redirected. The hand-in above is where the game now sends you.";

    public const string PlotTool = "plot_course";

    private const double ListWidth = 360;

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<DateTimeOffset> _now;
    private readonly CapabilityRegistry? _registry;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly bool _headset;
    private readonly double _gap;

    private MissionBoard? _seenBoard;
    private JournalLocation? _seenLocation;
    private long _seenMinute;
    private long? _selected;
    private string _query = string.Empty;

    public MissionsPage(
        Func<CommanderGameState?> state,
        Func<DateTimeOffset> now,
        CapabilityRegistry? registry = null,
        Func<string, Task<bool>>? copy = null,
        bool headset = false)
    {
        _state = state;
        _now = now;
        _registry = registry;
        _copy = copy;
        _headset = headset;
        _gap = headset ? 20 : 24;

        Margin = new Thickness(14);
        Draw();
    }

    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    public bool Filters => (_state()?.Missions.Missions.Count ?? 0) > 0;

    public void Filter(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed == _query)
        {
            return;
        }

        _query = trimmed;
        Draw();
    }

    /// <summary>Redraws when the board or the location has changed, or the clock has passed a minute boundary.</summary>
    public bool Tick()
    {
        var state = _state();

        if (ReferenceEquals(state?.Missions, _seenBoard)
            && ReferenceEquals(state?.Location, _seenLocation)
            && Minute(_now()) == _seenMinute)
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>The header line: the count, how many hand in here, and the reward total.</summary>
    public static string Summarise(MissionBoard board, JournalLocation location)
    {
        var count = board.Missions.Count;

        if (count == 0)
        {
            return "No missions accepted.";
        }

        var said = count == 1 ? "1 mission." : $"{Number(count)} missions.";
        var here = board.Missions.Count(mission => MissionBoard.HandsInHere(mission, location));

        if (here > 0)
        {
            said += here == 1 ? " 1 hands in here." : $" {Number(here)} hand in here.";
        }

        if (board.Rewards() is { } rewards)
        {
            said += $" {(rewards.Partial ? "At least " : string.Empty)}{Number(rewards.Total)} credits in rewards.";
        }

        return said;
    }

    /// <summary>The group head a band is drawn under.</summary>
    public static string BandName(MissionBand band) => band switch
    {
        MissionBand.ExpiringSoon => "Expiring within the hour",
        MissionBand.HandInHere => "Hand in here",
        _ => "Everything else",
    };

    /// <summary>How long ago, in words: <c>6 hours ago</c>.</summary>
    public static string Ago(TimeSpan age) =>
        age.TotalMinutes < 1 ? "just now"
        : age.TotalHours < 1 ? Plural((int)age.TotalMinutes, "minute")
        : age.TotalDays < 1 ? Plural((int)age.TotalHours, "hour")
        : Plural((int)age.TotalDays, "day");

    private static string Plural(int count, string unit) =>
        count == 1 ? $"1 {unit} ago" : $"{Number(count)} {unit}s ago";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static long Minute(DateTimeOffset time) => time.UtcTicks / TimeSpan.TicksPerMinute;

    private bool Matches(Mission mission) =>
        _query.Length == 0
        || new[] { mission.Title, mission.Faction, mission.DestinationStation, mission.DestinationSystem }
            .Any(text => text?.Contains(_query, StringComparison.OrdinalIgnoreCase) == true);

    private void Draw()
    {
        var state = _state();
        var board = state?.Missions ?? MissionBoard.Empty;
        var location = state?.Location ?? JournalLocation.Unknown;
        var now = _now();

        _seenBoard = state?.Missions;
        _seenLocation = state?.Location;
        _seenMinute = Minute(now);

        var summary = Summarise(board, location);

        if (summary != Summary)
        {
            Summary = summary;
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }

        if (board.Missions.Count == 0)
        {
            Content = Empty();
            return;
        }

        var shown = board.Ranked(now, location).Where(Matches).ToList();

        if (_selected is not { } id || shown.All(mission => mission.Id != id))
        {
            _selected = shown.FirstOrDefault()?.Id;
        }

        var selected = shown.FirstOrDefault(mission => mission.Id == _selected);

        var list = List(shown, now, location);
        var detail = selected is null ? new StackPanel() : Detail(selected, now, location);

        var hint = Hint();
        hint.Margin = new Thickness(0, 18, 0, 0);

        Control left;
        Control right;

        if (_headset)
        {
            DockPanel.SetDock(hint, Dock.Bottom);
            left = new DockPanel { Children = { hint, LoadoutPages.Scrolling(list) } };
            right = LoadoutPages.Scrolling(detail);
        }
        else
        {
            left = new StackPanel { Children = { list, hint } };
            right = detail;
        }

        var rule = new Border
        {
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(_gap, 0, 0, 0),
            Child = right,
        };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.Line2Key);
        Grid.SetColumn(rule, 1);

        var panes = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(new GridLength(ListWidth)), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = _gap,
            Children = { left, rule },
        };

        Content = _headset
            ? panes
            : new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panes,
            };
    }

    private Control Empty()
    {
        var hint = Hint();
        hint.Margin = new Thickness(0, 18, 0, 0);

        return new StackPanel
        {
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 20, 0, 0),
            Children =
            {
                Prose(Nothing, TypeScale.Body, ThemeManager.WhiteKey),
                Spaced(Prose(NothingHint, TypeScale.Tip, ThemeManager.GreyKey), 10),
                hint,
            },
        };
    }

    private static TextBlock Spaced(TextBlock block, double top)
    {
        block.Margin = new Thickness(0, top, 0, 0);
        return block;
    }

    private static TextBlock Hint() => Prose($"Say: “{Phrase}”", TypeScale.Tip, ThemeManager.GreyKey);

    private StackPanel List(IReadOnlyList<Mission> shown, DateTimeOffset now, JournalLocation location)
    {
        var list = new StackPanel();

        foreach (var group in shown.GroupBy(mission => MissionBoard.BandOf(mission, now, location)))
        {
            var head = ListRow.Head(BandName(group.Key));

            if (list.Children.Count > 0)
            {
                head.Margin = new Thickness(0, 12, 0, 2);
            }

            list.Children.Add(head);

            var rows = new StackPanel { Spacing = Gaps.Tile };

            foreach (var mission in group)
            {
                rows.Children.Add(Row(mission, mission.Id == _selected, now, location));
            }

            list.Children.Add(rows);
        }

        return list;
    }

    private Button Row(Mission mission, bool selected, DateTimeOffset now, JournalLocation location)
    {
        var name = ListRow.Name(new TextBlock { Text = mission.Title, TextWrapping = TextWrapping.Wrap });

        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        if (!mission.HasDetail)
        {
            left.Children.Add(Chrome("No detail", ListRow.SubSize, FontWeight.Medium, 0.04, selected ? ThemeManager.BrownKey : ThemeManager.GreyKey));
        }
        else if (HandIn(mission) is { } handIn)
        {
            var key = selected ? ThemeManager.BrownKey
                : Here(mission, location) ? ThemeManager.CyanKey
                : ThemeManager.AKey;
            left.Children.Add(Chrome(handIn, ListRow.SubSize, FontWeight.Medium, 0.04, key));
        }

        var right = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(right, 1);

        if (Countdown(mission, now, selected, 14) is { } countdown)
        {
            countdown.HorizontalAlignment = HorizontalAlignment.Right;
            right.Children.Add(countdown);
        }

        if (mission.Reward is { } reward)
        {
            right.Children.Add(Mono(
                $"{Number(reward)} CR", TypeScale.Meta, selected ? ThemeManager.BrownKey : ThemeManager.GreyKey, HorizontalAlignment.Right));
        }

        var button = new Button
        {
            Tag = mission.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Children = { left, right },
            },
        };

        ListRow.Dress(button, selected);

        button.Click += (_, _) =>
        {
            _selected = mission.Id;
            Draw();
        };

        return button;
    }

    /// <summary><c>STATION · SYSTEM</c>, or whichever of the two is known.</summary>
    private static string? HandIn(Mission mission)
    {
        string[] parts = [.. new[] { mission.DestinationStation, mission.DestinationSystem }.OfType<string>()];
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    private static bool Here(Mission mission, JournalLocation location) =>
        mission.DestinationSystem is { Length: > 0 } system
        && string.Equals(system, location.StarSystem, StringComparison.OrdinalIgnoreCase);

    private static bool Soon(Mission mission, DateTimeOffset now) =>
        mission.Expiry is { } expiry && expiry > now && expiry - now < MissionBoard.SoonWindow;

    /// <summary>The time left in mono, <c>warn</c> under an hour; <c>EXPIRED</c> in red chrome; null without an expiry.</summary>
    private static TextBlock? Countdown(Mission mission, DateTimeOffset now, bool selected, double size)
    {
        if (mission.Expiry is not { } expiry)
        {
            return null;
        }

        if (expiry <= now)
        {
            return Chrome("Expired", TypeScale.Small, FontWeight.SemiBold, Fonts.ChromeTracking, selected ? ThemeManager.KnockKey : ThemeManager.RedKey);
        }

        var key = selected ? ThemeManager.KnockKey : Soon(mission, now) ? ThemeManager.WarnKey : ThemeManager.AKey;

        return Mono(MissionCountdown.Format(expiry, now), size, key);
    }

    private Control Detail(Mission mission, DateTimeOffset now, JournalLocation location)
    {
        var band = MissionBoard.BandOf(mission, now, location);
        var detail = new StackPanel();

        var crumb = TitleText.Context($"Commander › Missions › {BandName(band)}");
        crumb.Margin = new Thickness(0, 10, 0, 14);
        detail.Children.Add(crumb);

        var title = TitleText.Build(mission.Title, 24, TitleRank.Screen);
        title.TextWrapping = TextWrapping.Wrap;
        detail.Children.Add(TitleText.Block(title, figure: Actions(mission, location)));

        var cells = new List<(Control Cell, int Span)>
        {
            (TimeLeft(mission, now), 1),
        };

        if (mission.HasDetail)
        {
            cells.Add((Block("Reward", mission.Reward is { } reward
                ? Mono($"{Number(reward)} CR", 22, ThemeManager.AKey)
                : Prose("—", TypeScale.Small, ThemeManager.GreyKey)), 1));
            cells.Add((Block("Accepted", Accepted(mission.AcceptedAt!.Value, now)), 1));
            cells.Add((Block("Hand in at", Destination(mission, location)), 2));
            cells.Add((Block("Given by", Prose(mission.Faction ?? "—", TypeScale.Small, ThemeManager.WhiteKey, FontWeight.SemiBold)), 1));

            if (mission.TargetFaction is { } target)
            {
                cells.Add((Block("Against", Prose(target, TypeScale.Small, ThemeManager.RedKey, FontWeight.SemiBold)), 2));
            }

            if ((mission.CommodityLocalised ?? mission.Commodity) is { } commodity)
            {
                cells.Add((Block("Cargo", Counted(mission.Count ?? mission.Cargo?.Total, commodity)), 1));
            }

            if (mission.PassengerMission)
            {
                cells.Add((Block("Passengers", Counted(mission.PassengerCount, "aboard")), 1));
            }
        }

        var grid = DataGrid(cells);
        grid.Margin = new Thickness(0, 20, 0, 0);
        detail.Children.Add(grid);

        if (!mission.HasDetail)
        {
            var strip = new Border
            {
                Margin = new Thickness(0, Gaps.Tile, 0, 0),
                Padding = new Thickness(14, 12),
                Child = Prose(NoDetail, TypeScale.Small, ThemeManager.WhiteKey),
            };
            Themed(strip, Border.BackgroundProperty, ThemeManager.SlabKey);
            detail.Children.Add(strip);
            return detail;
        }

        if (mission.Cargo is { } cargo)
        {
            detail.Children.Add(Delivery(cargo));
        }

        if (mission.Redirected)
        {
            detail.Children.Add(Spaced(Prose(RedirectedLine, TypeScale.Tip, ThemeManager.GreyKey), 18));
        }

        return detail;
    }

    /// <summary>HAND IN HERE where the Commander is docked at it; else the copy glyph and the plot tile for another system.</summary>
    private Control? Actions(Mission mission, JournalLocation location)
    {
        if (!mission.HasDetail)
        {
            return null;
        }

        if (MissionBoard.HandsInHere(mission, location))
        {
            return Chrome("Hand in here", TypeScale.Small, FontWeight.SemiBold, Fonts.ChromeTracking, ThemeManager.CyanKey);
        }

        if (mission.DestinationSystem is not { Length: > 0 } system)
        {
            return null;
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (_copy is { } copy)
        {
            actions.Children.Add(CopyGlyph.For(system, copy));
        }

        if (_registry is { } registry)
        {
            var plot = new Button { Content = "Plot to hand-in", Height = TypeScale.MinimumTarget };
            plot.Click += (_, _) => _ = Plot(registry, system);
            actions.Children.Add(plot);
        }

        return actions.Children.Count == 0 ? null : actions;
    }

    private static Task<ToolResult> Plot(CapabilityRegistry registry, string system) =>
        registry.InvokeAsync(
            PlotTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system }),
            CancellationToken.None,
            ToolCaller.Commander);

    private static Border TimeLeft(Mission mission, DateTimeOffset now)
    {
        var label = Soon(mission, now) ? "Time left · under an hour" : "Time left";

        return Block(label, Countdown(mission, now, selected: false, 22) ?? Prose("—", TypeScale.Small, ThemeManager.GreyKey));
    }

    private static TextBlock Accepted(DateTimeOffset at, DateTimeOffset now)
    {
        var time = Fonts.Mono(at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture));
        time.FontSize = TypeScale.Small;
        Themed(time, Run.ForegroundProperty, ThemeManager.AKey);

        var age = new Run($" · {Ago(now - at)}");
        Themed(age, Run.ForegroundProperty, ThemeManager.GreyKey);

        return new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            Inlines = [time, age],
        };
    }

    private static TextBlock Destination(Mission mission, JournalLocation location)
    {
        var block = new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        if (mission.DestinationStation is { } station)
        {
            block.Inlines!.Add(Ink(station, ThemeManager.WhiteKey));
        }

        if (mission.DestinationSystem is { } system)
        {
            if (block.Inlines!.Count > 0)
            {
                block.Inlines.Add(Ink(" · ", ThemeManager.GreyKey));
            }

            block.Inlines.Add(Ink(system, Here(mission, location) ? ThemeManager.CyanKey : ThemeManager.AKey));
        }

        if (block.Inlines!.Count == 0)
        {
            block.Inlines.Add(Ink("—", ThemeManager.GreyKey));
        }

        return block;
    }

    private static Run Ink(string text, string key)
    {
        var run = new Run(text);
        Themed(run, Run.ForegroundProperty, key);
        return run;
    }

    /// <summary>The count in mono, then the words, both in A.</summary>
    private static TextBlock Counted(int? count, string words)
    {
        var block = new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.AKey);

        if (count is { } number)
        {
            block.Inlines!.Add(Fonts.Mono(Number(number)));
            block.Inlines.Add(new Run(" "));
        }

        block.Inlines!.Add(new Run(words));
        return block;
    }

    /// <summary>A <c>slab</c> data block: a grey chrome label over its value.</summary>
    private static Border Block(string label, Control value)
    {
        var block = new Border
        {
            Padding = new Thickness(14, 12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new StackPanel
            {
                Spacing = 4,
                Children = { Chrome(label, TypeScale.Meta, FontWeight.Medium, Fonts.ChromeTracking, ThemeManager.GreyKey), value },
            },
        };
        Themed(block, Border.BackgroundProperty, ThemeManager.SlabKey);
        return block;
    }

    /// <summary>Three equal columns, 2px apart, each cell placed after the last and wrapped to a new row where it does not fit.</summary>
    private static Grid DataGrid(IReadOnlyList<(Control Cell, int Span)> cells)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = Gaps.Tile,
            RowSpacing = Gaps.Tile,
        };

        var row = 0;
        var column = 0;

        foreach (var (cell, span) in cells)
        {
            if (column + span > 3)
            {
                row++;
                column = 0;
            }

            if (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            Grid.SetColumnSpan(cell, span);
            grid.Children.Add(cell);

            column += span;
        }

        return grid;
    }

    private static Control Delivery(MissionCargo cargo)
    {
        var heading = TitleText.Build("Delivery", TypeScale.Section, TitleRank.Group);
        heading.VerticalAlignment = VerticalAlignment.Bottom;

        var aside = TitleText.Context("From the cargo depot");
        aside.VerticalAlignment = VerticalAlignment.Bottom;
        aside.Margin = new Thickness(12, 0, 0, 0);

        var head = TitleText.GroupRow(new StackPanel { Orientation = Orientation.Horizontal, Children = { heading, aside } });
        head.Margin = new Thickness(0, 24, 0, 8);

        var delivered = new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Tip,
            Inlines =
            [
                new Run("Delivered "),
                Mono(Number(cargo.Delivered)),
                new Run(" of "),
                Mono(Number(cargo.Total)),
            ],
        };
        Themed(delivered, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var collected = new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Inlines = [new Run("Collected "), Mono(Number(cargo.Collected))],
        };
        Themed(collected, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        DockPanel.SetDock(collected, Dock.Right);

        var line = new DockPanel { Children = { collected, delivered } };

        var gauge = Gauge.Track(cargo.Total > 0 ? (double)cargo.Delivered / cargo.Total : 0, Gauge.FillKey(GaugeFill.Progress));

        var strip = new Border
        {
            Padding = new Thickness(14, 12),
            Child = new StackPanel { Spacing = 10, Children = { line, gauge } },
        };
        Themed(strip, Border.BackgroundProperty, ThemeManager.SlabKey);

        return new StackPanel { Children = { head, strip } };

        static Run Mono(string text)
        {
            var run = Fonts.Mono(text);
            Themed(run, Run.ForegroundProperty, ThemeManager.AKey);
            return run;
        }
    }

    private static TextBlock Chrome(string text, double size, FontWeight weight, double tracking, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = size,
            FontWeight = weight,
            LetterSpacing = size * tracking,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Mono(string text, double size, string key, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = size,
            HorizontalAlignment = alignment,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Prose(string text, double size, string key, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = size,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
