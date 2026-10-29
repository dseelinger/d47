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

/// <summary>The maintainer's page for testing stories: bench slots and jumping the current story. Reachable only while the bench is on.</summary>
public sealed class StoryBenchPage : UserControl, IPageSummary
{
    public const string RootKey = "stories.bench";
    public const string PageName = "Story bench";
    public const string FinaleLabel = "Finale";
    public const string WritingNotice = "A chapter is being written. These buttons return once it is done.";
    public const double ContentWidth = 900;

    public static NavCrumb Crumb => new(RootKey, PageName);

    public string Summary => "Test a story: switch slots, jump ahead, fire beats, rewind a chapter.";

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    private const double SlotButtonWidth = 180;
    private const double BeatButtonWidth = 150;
    private const double Bar = 3;

    private readonly AdventureSurface _surface;
    private readonly StoryBench _bench;
    private readonly StoryDirector _director;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _body = new() { Spacing = 28, MaxWidth = ContentWidth, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StatusLine _status = new();
    private bool _acting;
    private string? _jumpLabel;

    public StoryBenchPage(AdventureSurface surface, StoryBench bench, StoryDirector director, PanelPrompts prompts)
    {
        _surface = surface;
        _bench = bench;
        _director = director;
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
        _bench.Switched += OnChanged;
        _bench.CheckpointsChanged += OnChanged;
        _director.Stories.Changed += OnChanged;
        _director.WritingChanged += OnChanged;
        _surface.Book.StirringChanged += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _bench.Switched -= OnChanged;
        _bench.CheckpointsChanged -= OnChanged;
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

        var slots = _bench.Summaries(commander);
        _body.Children.Add(SlotSection(slots, commander, busy));

        var story = _director.Stories.Current(commander);
        var active = _surface.Book.Active(commander);

        if (story is null && active.Count == 0)
        {
            var slot = slots.FirstOrDefault(entry => entry.Active)?.Name ?? StoryBench.MyStory;
            _body.Children.Add(Section(UnderWayHead, null, null, Slab(NothingUnderWay(slot))));
            return;
        }

        _body.Children.Add(Section("Jump", JumpNote, null, JumpSection(commander, story, busy)));
        _body.Children.Add(Section("Beats", BeatsNote, null, BeatSection(commander, active, busy)));
        _body.Children.Add(Section("Checkpoints", CheckpointsNote, null, CheckpointSection(commander, story, busy)));
    }

    internal const string SlotsNote = "Each slot keeps its own story files. Leaving one switches its story off until you come back.";
    internal const string JumpNote = "Moves the story so the chosen clue, or the finale, is due now, and writes its chapter.";
    internal const string BeatsNote = "Next beat fires the waiting beat without its journal event.";
    internal const string CheckpointsNote = "One is taken as each chapter begins. Rewind restores it and pauses the story.";
    internal const string UnderWayHead = "Jump · Beats · Checkpoints";
    internal const string NoBeatsNotice = "No chapter or Short Story is under way.";
    internal const string NoStoryNotice = "No story is running.";
    internal const string NoCheckpointsNotice = "No checkpoints yet.";
    internal const string StirringNotice = "Next beat returns once D47 has said the last one.";

    internal static string NothingUnderWay(string slot) =>
        $"No story or Short Story is running in {slot}. Jump, beats and checkpoints show here once one starts.";

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

    // ---- beats -------------------------------------------------------------------------------

    internal static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    private StackPanel BeatSection(string? commander, IReadOnlyList<AdventureStanding> active, bool busy)
    {
        var holder = new StackPanel { Spacing = 12 };

        if (active.Count == 0)
        {
            holder.Children.Add(AdventuresPage.Muted(NoBeatsNotice));
            return holder;
        }

        var chapters = _director.Stories.Current(commander)?.Chapters ?? [];

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
            heading.Children.Add(Chrome(standing.Step() is { } step ? $"{kind} · {step}" : kind, ListRow.AsideSize, ThemeManager.AKey));

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

            var next = Tile("NextBeat", "Next beat", BeatButtonWidth, !busy && !stirring);
            var key = adventure.Key;
            var name = beat.Title;
            next.Click += (_, _) => Act(
                $"Stepping {name}…",
                () => Task.FromResult(_bench.Step(commander, key, _surface.Now())),
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

        if (_bench.Checkpoints(commander) is not { Count: > 0 } checkpoints)
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
                () => Task.FromResult(_bench.Rewind(checkpoint.Chapter, _surface.Now())),
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

    // ---- slots -------------------------------------------------------------------------------

    private StackPanel SlotSection(IReadOnlyList<BenchSlotSummary> slots, string? commander, bool busy)
    {
        var create = Tile("NewSlot", "New slot", BeatButtonWidth, !busy);
        create.Click += (_, _) => NewSlot();

        var rows = new StackPanel();

        for (var index = 0; index < slots.Count; index++)
        {
            rows.Children.Add(SlotRow(slots[index], commander, busy, first: index == 0));
        }

        return Section("Slots", SlotsNote, create, rows);
    }

    private Border SlotRow(BenchSlotSummary slot, string? commander, bool busy, bool first)
    {
        var name = ListRow.Name(new TextBlock { Text = slot.Name, TextWrapping = TextWrapping.Wrap });
        var detail = new TextBlock { FontSize = TypeScale.ControlLarge, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(detail, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        detail.Inlines = DescribeRuns(slot);

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, detail } };

        Control end;

        if (slot.Active)
        {
            end = Chrome("Active", ListRow.AsideSize, ThemeManager.CyanKey, FontWeight.SemiBold);
            ((TextBlock)end).TextAlignment = TextAlignment.Center;
            ((TextBlock)end).Width = SlotButtonWidth;
        }
        else
        {
            var go = Tile("SwitchTo", slot.Name == StoryBench.MyStory ? "Back to my story" : "Switch to", SlotButtonWidth, !busy);
            var target = slot.Name == StoryBench.MyStory ? null : slot.Name;
            go.Click += (_, _) => Act(
                $"Switching to {slot.Name}…",
                () => Task.FromResult(_bench.SwitchTo(target, commander, _surface.Now())),
                $"Switched to {slot.Name}.");
            end = go;
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            MinHeight = 60,
            Children = { text, end },
        };
        Grid.SetColumn(end, 1);

        var content = new Border { Padding = new Thickness(9, 6, 0, 6), Child = grid };
        Inks(content, slot.Active ? ThemeManager.CyanKey : ThemeManager.WhiteKey, ThemeManager.GreyKey);

        return slot.Active
            ? Row(content, first, ThemeManager.CyanKey, ThemeManager.CyanGroundKey)
            : Row(content, first);
    }

    /// <summary><see cref="Describe"/> as runs: the title in White, the numbers in mono.</summary>
    internal static InlineCollection DescribeRuns(BenchSlotSummary slot)
    {
        if (slot.Title is null)
        {
            return [new Run("No story.")];
        }

        static Run Piece(string text, string? fontFamily = null, string? key = null)
        {
            var run = new Run(text);

            if (fontFamily is not null)
            {
                run.FontFamily = new FontFamily(fontFamily);
            }

            if (key is not null)
            {
                run.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(key));
            }

            return run;
        }

        return
        [
            Piece(slot.Title, key: ThemeManager.WhiteKey),
            Piece($" · {slot.Length} · "),
            Piece(slot.CluesGiven.ToString(CultureInfo.InvariantCulture), Fonts.MonoFamily),
            Piece(" of "),
            Piece(slot.Clues.ToString(CultureInfo.InvariantCulture), Fonts.MonoFamily),
            Piece(" clues given"),
        ];
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

        var jump = Tile("Jump", "Jump", SlotButtonWidth, !busy);
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

    private void NewSlot() => _prompts.Enter(
        new EntryRequest(
            "stories.bench.new",
            "New slot",
            "Name the slot",
            "1 to 40 letters, digits, spaces, hyphens or underscores.",
            string.Empty,
            EntrySurface.Voice,
            value => string.IsNullOrWhiteSpace(value) ? EntryVerdict.No("Give the slot a name.") : EntryVerdict.Ok),
        name =>
        {
            if (_bench.Create(name) is { } refusal)
            {
                _status.Fail(refusal);
            }
            else
            {
                _status.Say($"Made the slot {name.Trim()}.");
            }

            Rebuild();
        });

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
