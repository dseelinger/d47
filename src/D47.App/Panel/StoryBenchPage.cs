using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The Commander's story runs, to switch between. While the bench is on it also jumps the current story, fires beats and rewinds.</summary>
public sealed class StoryBenchPage : UserControl, IPageSummary
{
    public const string RootKey = "stories.bench";
    public const string PageName = "Story bench";
    public const string FinaleLabel = "Finale";
    public const string WritingNotice = "A chapter is being written. These buttons return once it is done.";
    public const double ContentWidth = 1224;

    public static NavCrumb Crumb => new(RootKey, PageName);

    public const string SummaryText = "Your stories: switch between them, or play back every line.";

    public string Summary => SummaryText;

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    private const double ButtonWidth = 180;
    private const double BeatButtonWidth = 150;
    private const double Bar = 3;

    private readonly AdventureSurface _surface;
    private readonly StoryBench? _bench;
    private readonly StoryDirector _director;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _body = new() { Spacing = 28, MaxWidth = ContentWidth, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StatusLine _status = new();
    private bool _acting;
    private string? _jumpLabel;

    public StoryBenchPage(AdventureSurface surface, StoryBench? bench, StoryDirector director, PanelNavigator nav, PanelPrompts prompts)
    {
        _surface = surface;
        _bench = bench;
        _director = director;
        _nav = nav;
        _prompts = prompts;

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(14), Child = _body },
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_bench is not null)
        {
            _bench.CheckpointsChanged += OnChanged;
        }

        _director.Stories.Changed += OnChanged;
        _director.WritingChanged += OnChanged;
        _surface.Book.StirringChanged += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_bench is not null)
        {
            _bench.CheckpointsChanged -= OnChanged;
        }

        _director.Stories.Changed -= OnChanged;
        _director.WritingChanged -= OnChanged;
        _surface.Book.StirringChanged -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private bool Writing(string? commander) => _director.IsWriting(commander) || _director.IsRewriting(commander);

    private void Rebuild()
    {
        _body.Children.Clear();
        _body.Children.Add(_status);

        var commander = _surface.Commander();
        var busy = _acting || Writing(commander);

        if (Writing(commander))
        {
            _body.Children.Add(WritingBlock(commander));
        }

        _body.Children.Add(RunList(commander, busy));

        if (_director.Stories.Current(commander) is { } running)
        {
            _body.Children.Add(BeatSheetSection(running));
        }

        if (_bench is null)
        {
            return;
        }

        _body.Children.Add(StarSystemPage.GroupHead(MaintainerHead, Mono(BenchFlag, TypeScale.Meta, ThemeManager.WarnKey)));

        var story = _director.Stories.Current(commander);
        var active = _surface.Book.Active(commander);

        if (story is null && active.Count == 0)
        {
            _body.Children.Add(Section(UnderWayHead, null, null, Slab(NothingUnderWay)));
            return;
        }

        _body.Children.Add(Section("Jump", JumpNote, null, JumpSection(commander, story, busy)));
        _body.Children.Add(Section("Objectives", BeatsNote, null, BeatSection(commander, active, busy)));
        _body.Children.Add(Section("Checkpoints", CheckpointsNote, null, CheckpointSection(commander, story, busy)));
    }

    internal const string BeatSheetHead = "Beat sheet";
    internal const string BeatSheetColumns = "*,*,*,*,*";

    internal static string BeatSheetNote(Story story) =>
        $"{story.Title} · {story.Pacing.Name}. A beat is reached once its stage has passed.";

    internal static string BeatSheetCount(IReadOnlyList<StoryBeatStanding> sheet) =>
        $"{sheet.Count(beat => beat.State == StoryBeatState.Reached).ToString(CultureInfo.InvariantCulture)} / {sheet.Count.ToString(CultureInfo.InvariantCulture)} reached";

    internal static string BeatStateWord(StoryBeatState state) => state switch
    {
        StoryBeatState.Reached => "✓ Reached",
        StoryBeatState.Current => "Current",
        _ => "Later",
    };

    internal const string MaintainerHead = "Testing tools";
    internal const string BenchFlag = "D47_STORY_BENCH=1";
    internal const string NoRuns = "No stories yet. Pick one in the Catalogue, and every line it speaks is kept here to play back.";
    internal const string OpenCatalogue = "Open the Catalogue";
    internal const string NoStoryName = "No story";
    internal const string NoStoryLine = "Every story shelved, your own Backstory back.";
    internal const string SwitchLabel = "Switch to";
    internal const string OpenLabel = "Open";
    internal const string NoLine = "—";
    internal const string JumpNote = "Moves the story so the chosen clue, or the finale, is due now, and writes its chapter.";
    internal const string BeatsNote = "Next objective fires the waiting objective without its journal event.";
    internal const string CheckpointsNote = "One is taken as each chapter begins. Rewind restores it and pauses the story.";
    internal const string UnderWayHead = "Jump · Beats · Checkpoints";
    internal const string NoBeatsNotice = "No chapter or Short Story is under way.";
    internal const string NoStoryNotice = "No story is running.";
    internal const string NoCheckpointsNotice = "No checkpoints yet.";
    internal const string StirringNotice = "Next objective returns once D47 has said the last one.";

    internal const string NothingUnderWay = "No story or Short Story is running. Jump, objectives and checkpoints show here once one starts.";

    private static StackPanel BeatSheetSection(Story story)
    {
        var sheet = StoryClues.BeatSheet(story);
        var head = StarSystemPage.GroupHead(BeatSheetHead, BeatSheetNote(story), BeatSheetCount(sheet));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(BeatSheetColumns), ColumnSpacing = 2, RowSpacing = 2 };
        var rows = (sheet.Count + 4) / 5;

        for (var row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var index = 0; index < sheet.Count; index++)
        {
            var cell = BeatCell(sheet[index]);
            Grid.SetColumn(cell, index % 5);
            Grid.SetRow(cell, index / 5);
            grid.Children.Add(cell);
        }

        return new StackPanel { Spacing = 8, Children = { head, grid } };
    }

    private static Border BeatCell(StoryBeatStanding beat)
    {
        var current = beat.State == StoryBeatState.Current;
        var later = beat.State == StoryBeatState.Later;
        var stateKey = current ? ThemeManager.CyanKey : later ? ThemeManager.GreyKey : ThemeManager.BlueKey;
        var numberKey = current ? ThemeManager.CyanKey : ThemeManager.GreyKey;

        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        top.Children.Add(Mono(beat.Number.ToString("00", CultureInfo.InvariantCulture), TypeScale.Meta, numberKey));
        top.Children.Add(Chrome(BeatStateWord(beat.State), 11, stateKey, FontWeight.SemiBold));

        var name = AdventuresPage.Text(beat.Name, 15, later ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
        var content = new StackPanel { Spacing = 4, Margin = new Thickness(9, 10, 12, 10), MinHeight = 44, Children = { top, name } };
        var cell = Row(content, first: true, barKey: current ? ThemeManager.CyanKey : null, groundKey: current ? ThemeManager.CyanGroundKey : ThemeManager.SlabKey, ruleKey: null);
        cell.Name = $"Beat_{beat.Key}";
        return cell;
    }

    private static StackPanel Section(string name, string? note, Control? end, Control body)
    {
        var head = StarSystemPage.GroupHead(name, note ?? string.Empty, null, end);
        return new StackPanel { Spacing = 8, Children = { head, body } };
    }

    private static Border Slab(string text)
    {
        var prose = AdventuresPage.Text(text, TypeScale.ControlLarge);
        var slab = new Border { Padding = new Thickness(14), Child = prose };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
        return slab;
    }

    private Border WritingBlock(string? commander)
    {
        var stack = new StackPanel { Spacing = 8 };

        if (_director.Progress(commander) is { } writing)
        {
            var progress = new WriteProgressBar("Writing the chapter", () => _director.Progress(commander));
            progress.Show(writing);
            stack.Children.Add(progress);
        }

        stack.Children.Add(AdventuresPage.Text(WritingNotice, TypeScale.ControlLarge));

        var slab = new Border { Padding = new Thickness(14, 12), Child = stack };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
        return slab;
    }

    private static TextBlock Chrome(string text, double size, string key, FontWeight weight = FontWeight.Medium) =>
        Typeset.Chrome(
            text.ToUpperInvariant(), size, key,
            weight: weight, tracking: size * Fonts.ChromeTracking, wrap: true, vertical: VerticalAlignment.Center);

    private static TextBlock Mono(string text, double size, string key) =>
        Typeset.Mono(text, size, key, vertical: VerticalAlignment.Center);

    private static Button Tile(string name, string label, double width, bool enabled)
    {
        var button = new Button
        {
            Name = name,
            Content = label,
            Width = width,
            MinHeight = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = enabled,
        };
        return button;
    }

    /// <summary>A row with an optional 3px bar and ground in <paramref name="barKey"/>, over a 1px rule unless it is the first.</summary>
    private static Border Row(Control content, bool first, string? barKey = null, string? groundKey = null, string? ruleKey = ThemeManager.Line2Key)
    {
        var inner = new Border { BorderThickness = new Thickness(Bar, 0, 0, 0), BorderBrush = Brushes.Transparent, Child = content };

        if (barKey is not null)
        {
            ThemeInk.Bind(inner, Border.BorderBrushProperty, barKey);
        }

        if (groundKey is not null)
        {
            ThemeInk.Bind(inner, Border.BackgroundProperty, groundKey);
        }

        var outer = new Border { BorderThickness = new Thickness(0, first || ruleKey is null ? 0 : 1, 0, 0), Child = inner };

        if (ruleKey is not null)
        {
            ThemeInk.Bind(outer, Border.BorderBrushProperty, ruleKey);
        }

        return outer;
    }

    /// <summary>Sets the inks that <see cref="ListRow.Name"/> and <see cref="ListRow.Sub"/> follow on <paramref name="row"/>.</summary>
    private static void Inks(Control row, string nameKey, string secondaryKey)
    {
        row.Bind(ListRow.NameBrushProperty, Application.Current!.Resources.GetResourceObservable(nameKey));
        row.Bind(ListRow.SecondaryBrushProperty, Application.Current!.Resources.GetResourceObservable(secondaryKey));
    }

    // ---- runs --------------------------------------------------------------------------------

    private const string ColumnSpec = "*,96,84,84,108,150,Auto";
    private const string CompactColumnSpec = "*,96,84,150,Auto";
    private const double StoryColumnMin = 160;
    private const double FullListWidth = 1130;

    private bool _compact;

    private string Spec => _compact ? CompactColumnSpec : ColumnSpec;

    private int EndColumn => _compact ? 4 : 6;

    private T[] Visible<T>(T[] full) => _compact ? [full[0], full[1], full[3], full[5]] : full;

    private ColumnDefinitions Columns()
    {
        var columns = new ColumnDefinitions(Spec);
        columns[0].Width = new GridLength(1, GridUnitType.Star);
        columns[0].MinWidth = StoryColumnMin;
        return columns;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var compact = e.NewSize.Width > 0 && e.NewSize.Width < FullListWidth;

        if (compact != _compact)
        {
            _compact = compact;
            Rebuild();
        }
    }

    internal static string Day(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string Minute(DateTimeOffset at) => at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    internal static string StateLine(Story run) => run.State switch
    {
        StoryState.Running => "Running",
        StoryState.Paused => "Paused",
        StoryState.Shelved => $"Shelved {Day(run.OffSpans.Count > 0 ? run.OffSpans[^1].From : run.PickedAt)}"
            + (run.Backstory is null ? string.Empty : " · Backstory saved"),
        StoryState.Finished => $"Finished {Day(run.StoppedAt ?? run.PickedAt)}",
        StoryState.Abandoned => $"Abandoned {Day(run.StoppedAt ?? run.PickedAt)}",
        _ => $"Ended {Day(run.StoppedAt ?? run.PickedAt)}",
    };

    private string LastLine(string? commander, Story run)
    {
        var lines = _director.Records?.Read(commander, run);

        return lines is { Count: > 0 } ? Minute(lines.Max(line => line.Sent)) : NoLine;
    }

    private StackPanel RunList(string? commander, bool busy)
    {
        var holder = new StackPanel { Spacing = 22 };
        var runs = _director.Stories.Runs(commander);

        if (runs.Count == 0)
        {
            holder.Children.Add(StarSystemPage.GroupHead("Your stories", string.Empty, "0"));
            holder.Children.Add(AdventuresPage.Muted(NoRuns));

            var open = Tile("OpenCatalogue", OpenCatalogue, ButtonWidth, true);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Click += (_, _) => _nav.SelectRoot(StoriesView.RootKey);
            holder.Children.Add(open);
            return holder;
        }

        holder.Children.Add(ColumnHeads());

        var active = runs.Where(run => run.IsCurrent).ToList();
        var shelved = runs.Where(run => run.State == StoryState.Shelved).ToList();
        var finished = runs.Where(run => run.State == StoryState.Finished).ToList();
        var stopped = runs.Where(run => run.State is StoryState.Abandoned or StoryState.Ended).ToList();

        var groups = new List<(string Name, int Count, List<Control> Rows)>
        {
            ("Active", active.Count, [.. active.Select(run => RunRow(commander, run, busy, active: true))]),
            ("Shelved", shelved.Count, [.. shelved.Select(run => RunRow(commander, run, busy, active: false))]),
            ("Finished", finished.Count, [.. finished.Select(run => RunRow(commander, run, busy, active: false))]),
            ("Stopped", stopped.Count, [.. stopped.Select(run => RunRow(commander, run, busy, active: false))]),
        };

        if (active.Count > 0)
        {
            groups[1].Rows.Add(NoStoryRow(commander, busy));
        }

        foreach (var (name, count, rows) in groups)
        {
            if (rows.Count == 0)
            {
                continue;
            }

            var group = new StackPanel { Spacing = 0 };
            group.Children.Add(StarSystemPage.GroupHead(name, string.Empty, count.ToString(CultureInfo.InvariantCulture)));

            foreach (var row in rows)
            {
                group.Children.Add(row);
            }

            holder.Children.Add(group);
        }

        return holder;
    }

    private Grid ColumnHeads()
    {
        var heads = Visible(["Story", "Length", "Chapters", "Clues", "Picked", "Last line"]);
        var grid = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = 16, Margin = new Thickness(14, 0, 0, 0) };

        for (var index = 0; index < heads.Length; index++)
        {
            var head = Chrome(heads[index], TypeScale.Meta, ThemeManager.GreyKey, FontWeight.SemiBold);
            Grid.SetColumn(head, index);
            grid.Children.Add(head);
        }

        return grid;
    }

    private Border RunRow(string? commander, Story run, bool busy, bool active)
    {
        var name = AdventuresPage.Text(run.Title, TypeScale.ControlLarge, ThemeManager.WhiteKey);
        var state = AdventuresPage.Text(StateLine(run), TypeScale.Meta, active ? ThemeManager.CyanKey : ThemeManager.GreyKey);
        var title = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, state } };

        var cells = Visible(new Control[]
        {
            title,
            Mono(run.Pacing.Name, ListRow.AsideSize, ThemeManager.AKey),
            Mono(run.Chapters.Count.ToString(CultureInfo.InvariantCulture), ListRow.AsideSize, ThemeManager.AKey),
            Mono($"{run.CluesGiven.ToString(CultureInfo.InvariantCulture)} / {run.Pacing.ClueDays.Count.ToString(CultureInfo.InvariantCulture)}", ListRow.AsideSize, ThemeManager.AKey),
            Mono(Day(run.PickedAt), ListRow.AsideSize, ThemeManager.AKey),
            Mono(LastLine(commander, run), ListRow.AsideSize, ThemeManager.AKey),
        });

        var end = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var open = Tile("OpenRun", OpenLabel, BeatButtonWidth, true);
        open.Click += (_, _) => _nav.Drill(new NavCrumb(StoryRunPage.KeyOf(run), run.Title) { Level = StoryRunPage.Prefix });

        if (active)
        {
            var label = Chrome("Active", TypeScale.ControlLarge, ThemeManager.CyanKey, FontWeight.SemiBold);
            label.Margin = new Thickness(0, 0, 14, 0);
            end.Children.Add(label);
            end.Children.Add(open);
        }
        else if (run.State == StoryState.Shelved)
        {
            var switchTo = Tile("SwitchTo", SwitchLabel, BeatButtonWidth, !busy);
            switchTo.Click += (_, _) => AskSwitch(commander, run);
            end.Children.Add(open);
            end.Children.Add(switchTo);
        }
        else
        {
            end.Children.Add(open);
            end.Children.Add(DeleteTile(run, busy));
        }

        return Line(cells, end, active);
    }

    private Button DeleteTile(Story run, bool busy)
    {
        var delete = Tile("DeleteRun", StoryRunDeletion.DeleteLabel, BeatButtonWidth, !busy);
        delete.Classes.Add(Settings.SettingsView.DestructiveClass);
        delete.Click += (_, _) => StoryRunDeletion.Ask(
            _prompts, _director, _surface.Commander, _surface.Now, run,
            (doing, work, done) => Act(doing, () => Task.FromResult(work()), done));
        return delete;
    }

    private Border NoStoryRow(string? commander, bool busy)
    {
        var name = AdventuresPage.Text(NoStoryName, TypeScale.ControlLarge, ThemeManager.WhiteKey);
        var line = AdventuresPage.Text(NoStoryLine, TypeScale.Meta, ThemeManager.GreyKey);
        var title = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, line } };

        var switchTo = Tile("SwitchToNoStory", SwitchLabel, BeatButtonWidth, !busy);
        switchTo.Click += (_, _) => AskSwitch(commander, null);

        return Line([title], switchTo, false, span: true);
    }

    private Border Line(Control[] cells, Control end, bool active, bool span = false)
    {
        var grid = new Grid { ColumnDefinitions = Columns(), ColumnSpacing = 16, MinHeight = 60 };

        if (span)
        {
            Grid.SetColumnSpan(cells[0], EndColumn);
        }

        for (var index = 0; index < cells.Length; index++)
        {
            Grid.SetColumn(cells[index], index);
            cells[index].VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(cells[index]);
        }

        Grid.SetColumn(end, EndColumn);
        end.HorizontalAlignment = HorizontalAlignment.Right;
        end.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(end);

        var content = new Border { Padding = new Thickness(9, 6, 0, 6), Child = grid };

        return Row(content, first: false, barKey: active ? ThemeManager.CyanKey : null, groundKey: active ? ThemeManager.CyanGroundKey : null);
    }

    internal static string SwitchContext(Story? from, Story? to) =>
        string.Join(
            "\n",
            new[]
            {
                from is null ? null : $"{from.Title} is shelved: its chapter under way is dropped and its clock stops. Its Backstory is kept with it.",
                to is null
                    ? "No story runs, and your own Backstory comes back."
                    : $"{to.Title} comes back paused, with the Backstory you left it with. Resume writes its chapter again from where you are.",
            }.Where(text => text is not null));

    private void AskSwitch(string? drawnFor, Story? to)
    {
        if (!string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
        {
            _status.Fail("The Commander changed. Open the page again.");
            return;
        }

        var from = _director.Stories.Current(drawnFor);

        _prompts.Choose(
            new ChoiceRequest(
                "stories.bench.switch",
                PageName,
                to is null ? $"Switch to {NoStoryName}?" : $"Switch to {to.Title}?",
                SwitchContext(from, to),
                [new ChoiceOption("switch", "Switch"), new ChoiceOption("cancel", "Cancel")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "switch" || !string.Equals(drawnFor, _surface.Commander(), StringComparison.Ordinal))
                {
                    return;
                }

                var now = _surface.Now();

                if (to is null)
                {
                    Act("Shelving the story…", () => Task.FromResult(_director.ShelveCurrent(drawnFor, now)), "Every story is shelved.");
                }
                else
                {
                    Act(
                        $"Switching to {to.Title}…",
                        () => _director.SwitchAsync(drawnFor, to.Id, now, CancellationToken.None),
                        $"{to.Title} is your story again. Resume it to write its chapter.");
                }
            });
    }

    // ---- beats -------------------------------------------------------------------------------

    internal static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    internal static string HeaderText(string kind, string? stage, string? step) =>
        string.Join(" · ", new[] { kind, stage, step }.Where(part => part is not null));

    private StackPanel BeatSection(string? commander, IReadOnlyList<AdventureStanding> active, bool busy)
    {
        var holder = new StackPanel { Spacing = 12 };

        if (active.Count == 0)
        {
            holder.Children.Add(AdventuresPage.Muted(NoBeatsNotice));
            return holder;
        }

        var current = _director.Stories.Current(commander);
        var chapters = current?.Chapters ?? [];
        var stage = current is null ? null : StoryClues.StageName(StoryClues.Stage(current));

        foreach (var standing in active)
        {
            var adventure = standing.Adventure;
            var number = adventure.StoryId is null ? -1 : chapters.ToList().IndexOf(adventure.Key);
            var kind = adventure.StoryId is null
                ? "Short Story"
                : number >= 0 ? $"Story chapter {(number + 1).ToString(CultureInfo.InvariantCulture)}" : "Story chapter";

            var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 6, 0, 8) };
            Inks(heading, ThemeManager.WhiteKey, ThemeManager.GreyKey);
            heading.Children.Add(ListRow.Name(new TextBlock { Text = adventure.Name, TextWrapping = TextWrapping.Wrap }));
            heading.Children.Add(Chrome(HeaderText(kind, number >= 0 ? stage : null, standing.Step()), ListRow.AsideSize, ThemeManager.AKey));

            var ladder = new StackPanel { Spacing = 2 };
            var stirring = _surface.Book.IsStirring(commander, adventure.Key);

            for (var index = 0; index < adventure.Beats.Count; index++)
            {
                ladder.Children.Add(BeatRow(commander, standing, index, busy, stirring));
            }

            holder.Children.Add(new StackPanel { Children = { heading, ladder } });
        }

        return holder;
    }

    private Border BeatRow(string? commander, AdventureStanding standing, int index, bool busy, bool stirring)
    {
        var adventure = standing.Adventure;
        var beat = adventure.Beats[index];
        var fired = index < standing.Fired.Count;
        var waiting = !fired && index == standing.Current;
        var later = !fired && !waiting;

        var state = Chrome(fired ? "✓ Fired" : waiting ? "Waiting" : "—", ListRow.AsideSize, fired ? ThemeManager.BlueKey : waiting ? ThemeManager.CyanKey : ThemeManager.Grey2Key, FontWeight.SemiBold);
        var function = Chrome(string.IsNullOrWhiteSpace(beat.Function) ? "—" : beat.Function, TypeScale.Meta, later ? ThemeManager.Grey2Key : ThemeManager.GreyKey);
        var title = AdventuresPage.Text(beat.Title, TypeScale.ControlLarge, later ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
        title.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120,130,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            MinHeight = TypeScale.MinimumTarget,
        };

        grid.Children.Add(state);
        grid.Children.Add(function);
        grid.Children.Add(title);
        Grid.SetColumn(function, 1);
        Grid.SetColumn(title, 2);

        if (fired)
        {
            var at = Mono(Time(standing.Fired[index]), ListRow.AsideSize, ThemeManager.GreyKey);
            Grid.SetColumn(at, 3);
            grid.Children.Add(at);
        }

        if (waiting)
        {
            var notes = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 4) };
            notes.Children.Add(AdventuresPage.Text(beat.HandOff(), TypeScale.ControlLarge, ThemeManager.GreyKey));

            if (stirring)
            {
                notes.Children.Add(AdventuresPage.Text(StirringNotice, TypeScale.ControlLarge, ThemeManager.Grey2Key));
            }

            var next = Tile("NextBeat", "Next objective", BeatButtonWidth, !busy && !stirring);
            var key = adventure.Key;
            var name = beat.Title;
            next.Click += (_, _) => Act(
                $"Stepping {name}…",
                () => Task.FromResult(_bench!.Step(commander, key, _surface.Now())),
                $"Stepped {name}. It is said after the usual settle.");

            Grid.SetRow(notes, 1);
            Grid.SetColumn(notes, 1);
            Grid.SetColumnSpan(notes, 2);
            Grid.SetRow(next, 1);
            Grid.SetColumn(next, 3);
            grid.Children.Add(notes);
            grid.Children.Add(next);
        }

        var content = new Border { Padding = new Thickness(9, 6, 12, 6), Child = grid };

        return Row(
            content,
            first: true,
            barKey: waiting ? ThemeManager.CyanKey : null,
            groundKey: waiting ? ThemeManager.CyanGroundKey : ThemeManager.TileKey,
            ruleKey: null);
    }

    // ---- checkpoints -------------------------------------------------------------------------

    internal static string ChapterName(BenchCheckpoint checkpoint) => $"Chapter {checkpoint.Chapter.ToString(CultureInfo.InvariantCulture)}";

    internal static string Stamp(BenchCheckpoint checkpoint) => checkpoint.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private StackPanel CheckpointSection(string? commander, Story? story, bool busy)
    {
        var holder = new StackPanel();

        if (_bench!.Checkpoints(commander) is not { Count: > 0 } checkpoints)
        {
            holder.Children.Add(AdventuresPage.Muted(NoCheckpointsNotice));
            return holder;
        }

        var current = story?.Chapters.Count;

        for (var index = 0; index < checkpoints.Count; index++)
        {
            var checkpoint = checkpoints[index];

            var name = ListRow.Name(new TextBlock { Text = ChapterName(checkpoint), VerticalAlignment = VerticalAlignment.Center });

            var stage = new TextBlock { FontSize = TypeScale.ControlLarge, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            ThemeInk.Bind(stage, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            stage.Inlines = new InlineCollection { new Run(checkpoint.Stage) };

            if (checkpoint.Chapter == current)
            {
                var here = new Run(" · current chapter");
                here.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.CyanKey));
                stage.Inlines.Add(here);
            }

            var stamp = Mono(Stamp(checkpoint), ListRow.AsideSize, ThemeManager.GreyKey);

            var rewind = Tile("Rewind", "Rewind", BeatButtonWidth, !busy);
            rewind.Classes.Add(Settings.SettingsView.DestructiveClass);
            rewind.Click += (_, _) => Act(
                $"Rewinding to chapter {checkpoint.Chapter.ToString(CultureInfo.InvariantCulture)}…",
                () => Task.FromResult(_bench!.Rewind(checkpoint.Chapter, _surface.Now())),
                $"Rewound to chapter {checkpoint.Chapter.ToString(CultureInfo.InvariantCulture)}. The story is paused; Resume begins the chapter again.");

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("150,*,Auto,150"),
                ColumnSpacing = 16,
                MinHeight = 56,
                Children = { name, stage, stamp, rewind },
            };
            Grid.SetColumn(stage, 1);
            Grid.SetColumn(stamp, 2);
            Grid.SetColumn(rewind, 3);

            var content = new Border { Padding = new Thickness(9, 6, 0, 6), Child = grid };
            Inks(content, ThemeManager.WhiteKey, ThemeManager.GreyKey);
            holder.Children.Add(Row(content, first: index == 0));
        }

        return holder;
    }

    // ---- jump --------------------------------------------------------------------------------

    /// <summary>The jump choices for a story: each clue not yet given, by number and day, then the finale; empty once the story is in its finale, and for a Goals story.</summary>
    internal static IReadOnlyList<(string Label, StoryJumpTarget Target)> JumpChoices(Story story)
    {
        var choices = new List<(string, StoryJumpTarget)>();

        if (!story.IsCurrent || story.FinaleFrom is not null || story.GoalsAtPick is not null)
        {
            return choices;
        }

        for (var clue = story.CluesGiven; clue < story.Pacing.ClueDays.Count; clue++)
        {
            choices.Add((
                $"Clue {(clue + 1).ToString(CultureInfo.InvariantCulture)} · day {story.Pacing.ClueDays[clue].ToString(CultureInfo.InvariantCulture)}",
                StoryJumpTarget.AtClue(clue)));
        }

        choices.Add((FinaleLabel, StoryJumpTarget.Finale));
        return choices;
    }

    private StackPanel JumpSection(string? commander, Story? story, bool busy)
    {
        var holder = new StackPanel { Spacing = 2 };

        if (story is null)
        {
            holder.Children.Add(AdventuresPage.Muted(NoStoryNotice));
            return holder;
        }

        var count = $"{story.CluesGiven.ToString(CultureInfo.InvariantCulture)} / {story.Pacing.ClueDays.Count.ToString(CultureInfo.InvariantCulture)}";
        holder.Children.Add(StatTile.Grid(
            [
                StatTile.Build("Story", story.Title, StatInk.Name),
                StatTile.Build("Length", story.Pacing.Name),
                StatTile.Build("Clues given", count, StatInk.Number),
                StatTile.Build("Stage", StoryClues.StageName(StoryClues.Stage(story))),
            ]));

        if (JumpChoices(story) is not { Count: > 0 } choices)
        {
            var why = StoryJump.Refusal(story, StoryJumpTarget.Finale) ?? "The story is already in its finale.";
            holder.Children.Add(new Border { Margin = new Thickness(0, 6, 0, 0), Child = AdventuresPage.Muted(why) });
            return holder;
        }

        var labels = choices.Select(choice => choice.Label).ToList();
        var (view, choice) = Choice.Build(labels, Math.Max(0, labels.IndexOf(_jumpLabel ?? string.Empty)), alwaysStepper: true);
        view.MaxWidth = double.PositiveInfinity;
        view.HorizontalAlignment = HorizontalAlignment.Stretch;
        view.IsEnabled = !busy;
        choice.SelectionChanged += (_, _) => _jumpLabel = choice.SelectedItem;

        var jump = Tile("Jump", "Jump", ButtonWidth, !busy);
        jump.Margin = new Thickness(2, 0, 0, 0);
        jump.Click += (_, _) =>
        {
            var picked = choices.FirstOrDefault(entry => entry.Label == choice.SelectedItem);

            if (picked.Label is null)
            {
                return;
            }

            Act(
                $"Jumping to {picked.Label}…",
                () => _director.JumpAsync(commander, picked.Target, _surface.Now(), CancellationToken.None),
                $"Jumped to {picked.Label}.");
        };

        DockPanel.SetDock(jump, Dock.Right);
        holder.Children.Add(new DockPanel { Margin = new Thickness(0, 4, 0, 0), Children = { jump, view } });
        return holder;
    }

    /// <summary>Runs <paramref name="work"/> on the pool, with every button off until it returns, and shows its refusal or <paramref name="done"/>.</summary>
    private void Act(string doing, Func<Task<string?>> work, string done)
    {
        if (_acting)
        {
            return;
        }

        _acting = true;
        _status.Say(doing);
        Rebuild();

        _ = Task.Run(async () =>
        {
            var refusal = await work().ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                _acting = false;

                if (refusal is null)
                {
                    _status.Say(done);
                }
                else
                {
                    _status.Fail(refusal);
                }

                Rebuild();
            });
        });
    }
}
