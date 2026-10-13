using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The maintainer's page for testing stories: bench slots and jumping the current story. Reachable only while the bench is on.</summary>
public sealed class StoryBenchPage : UserControl
{
    public const string RootKey = "stories.bench";
    public const string PageName = "Story bench";
    public const string FinaleLabel = "Finale";
    public const string WritingNotice = "A chapter is being written. These buttons return once it is done.";

    public static NavCrumb Crumb => new(RootKey, PageName);

    private readonly AdventureSurface _surface;
    private readonly StoryBench _bench;
    private readonly StoryDirector _director;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _body = new() { Spacing = 8 };
    private readonly StatusLine _status = new();
    private bool _acting;
    private string? _jumpLabel;

    public StoryBenchPage(AdventureSurface surface, StoryBench bench, StoryDirector director, PanelPrompts prompts)
    {
        _surface = surface;
        _bench = bench;
        _director = director;
        _prompts = prompts;

        var (title, _) = RoutingKit.Title(PageName);
        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(_status, Dock.Top);
        root.Children.Add(title);
        root.Children.Add(_status);
        root.Children.Add(new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = root;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Switched += OnChanged;
        _bench.CheckpointsChanged += OnChanged;
        _director.Stories.Changed += OnChanged;
        _director.WritingChanged += OnChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _bench.Switched -= OnChanged;
        _bench.CheckpointsChanged -= OnChanged;
        _director.Stories.Changed -= OnChanged;
        _director.WritingChanged -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private bool Writing(string? commander) => _director.IsWriting(commander) || _director.IsRewriting(commander);

    private void Rebuild()
    {
        _body.Children.Clear();

        var commander = _surface.Commander();
        var busy = _acting || Writing(commander);

        if (Writing(commander))
        {
            _body.Children.Add(AdventuresPage.Muted(WritingNotice));
        }

        _body.Children.Add(AdventuresPage.Text("Slots", TypeScale.Small, ThemeManager.GreyKey));

        foreach (var slot in _bench.Summaries(commander))
        {
            _body.Children.Add(SlotRow(slot, commander, busy));
        }

        var create = new Button { Name = "NewSlot", Content = "New slot", HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !busy };
        create.Click += (_, _) => NewSlot();
        _body.Children.Add(create);

        _body.Children.Add(AdventuresPage.Text("Jump", TypeScale.Small, ThemeManager.GreyKey));
        _body.Children.Add(JumpSection(commander, busy));

        _body.Children.Add(AdventuresPage.Text("Checkpoints", TypeScale.Small, ThemeManager.GreyKey));
        _body.Children.Add(CheckpointSection(commander, busy));
    }

    internal static string Describe(BenchCheckpoint checkpoint) =>
        $"Chapter {checkpoint.Chapter.ToString(CultureInfo.InvariantCulture)} — {checkpoint.Stage} — {checkpoint.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";

    private StackPanel CheckpointSection(string? commander, bool busy)
    {
        var holder = new StackPanel { Spacing = 6 };

        if (_bench.Checkpoints(commander) is not { Count: > 0 } checkpoints)
        {
            holder.Children.Add(AdventuresPage.Muted("No checkpoints yet."));
            return holder;
        }

        foreach (var checkpoint in checkpoints)
        {
            var row = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(AdventuresPage.RowName(Describe(checkpoint)));

            var rewind = new Button { Name = "Rewind", Content = "Rewind", HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !busy };
            rewind.Click += (_, _) => Act(
                $"Rewinding to chapter {checkpoint.Chapter.ToString(CultureInfo.InvariantCulture)}…",
                () => Task.FromResult(_bench.Rewind(checkpoint.Chapter, _surface.Now())),
                $"Rewound to chapter {checkpoint.Chapter.ToString(CultureInfo.InvariantCulture)}. The story is paused; Resume begins the chapter again.");
            row.Children.Add(rewind);
            holder.Children.Add(row);
        }

        return holder;
    }

    private Control SlotRow(BenchSlotSummary slot, string? commander, bool busy)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 6) };
        stack.Children.Add(AdventuresPage.RowName(slot.Active ? $"{slot.Name} (active)" : slot.Name));
        stack.Children.Add(AdventuresPage.RowSecondary(Describe(slot)));

        if (!slot.Active)
        {
            var go = new Button
            {
                Name = "SwitchTo",
                Content = slot.Name == StoryBench.MyStory ? "Back to my story" : "Switch to",
                HorizontalAlignment = HorizontalAlignment.Left,
                IsEnabled = !busy,
            };
            var target = slot.Name == StoryBench.MyStory ? null : slot.Name;
            go.Click += (_, _) => Act(
                $"Switching to {slot.Name}…",
                () => Task.FromResult(_bench.SwitchTo(target, commander, _surface.Now())),
                $"Switched to {slot.Name}.");
            stack.Children.Add(go);
        }

        return stack;
    }

    internal static string Describe(BenchSlotSummary slot) =>
        slot.Title is null
            ? "No story."
            : $"{slot.Title} · {slot.Length} · {slot.CluesGiven.ToString(CultureInfo.InvariantCulture)} of {slot.Clues.ToString(CultureInfo.InvariantCulture)} clues given";

    /// <summary>The jump choices for a story: each clue not yet given, by number and day, then the finale; empty once the story is in its finale.</summary>
    internal static IReadOnlyList<(string Label, StoryJumpTarget Target)> JumpChoices(Story story)
    {
        var choices = new List<(string, StoryJumpTarget)>();

        if (!story.IsCurrent || story.FinaleFrom is not null)
        {
            return choices;
        }

        for (var clue = story.CluesGiven; clue < story.Pacing.ClueDays.Count; clue++)
        {
            choices.Add((
                $"Clue {(clue + 1).ToString(CultureInfo.InvariantCulture)} — day {story.Pacing.ClueDays[clue].ToString(CultureInfo.InvariantCulture)}",
                StoryJumpTarget.AtClue(clue)));
        }

        choices.Add((FinaleLabel, StoryJumpTarget.Finale));
        return choices;
    }

    private Control JumpSection(string? commander, bool busy)
    {
        var holder = new StackPanel { Spacing = 6 };

        if (_director.Stories.Current(commander) is not { } story || JumpChoices(story) is not { Count: > 0 } choices)
        {
            holder.Children.Add(AdventuresPage.Muted(
                _director.Stories.Current(commander) is null ? "No story is running." : "The story is already in its finale."));
            return holder;
        }

        var labels = choices.Select(choice => choice.Label).ToList();
        var (view, choice) = Choice.Build(labels, Math.Max(0, labels.IndexOf(_jumpLabel ?? string.Empty)), alwaysStepper: true);
        view.HorizontalAlignment = HorizontalAlignment.Left;
        view.IsEnabled = !busy;
        choice.SelectionChanged += (_, _) => _jumpLabel = choice.SelectedItem;

        var jump = new Button { Name = "Jump", Content = "Jump", HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !busy };
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

        holder.Children.Add(view);
        holder.Children.Add(jump);
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
