using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's pages: Fleet, then a ship, then a slot (list.md Phase 26, "Ships").
/// <para>
/// Three levels of one drill stack rather than three screens, so the reflow, the breadcrumb and
/// the phrases Phase 25 built cover all of them without any of them knowing.
/// </para>
/// <para>
/// <b>The ray points and the voice edits</b>, so every page here optimises for being <em>read</em>
/// rather than for manipulation density — one utterance does what six ray presses would, and the
/// say-line along the bottom is how a Commander learns the phrase for what they are looking at.
/// </para>
/// </summary>
public static class LoadoutPages
{
    /// <summary>The Loadout tab's Ships root. Suits and the gap analysis join it in Phase 27.</summary>
    public const string FleetRoot = "loadout.ships";

    /// <summary>How a ship's crumb is keyed, so a page can be rebuilt from the trail alone.</summary>
    public const string ShipPrefix = "loadout.ship:";

    /// <summary>And a slot's, below it.</summary>
    public const string SlotPrefix = "loadout.slot:";

    /// <summary>
    /// Draws whichever level a crumb names. Handed to <see cref="PanelView.Furnish"/>, so the
    /// drill strip asks for a page when it first shows one and keeps it afterwards.
    /// </summary>
    public static Control Build(
        NavCrumb crumb,
        ShipPlanService ships,
        ChecklistService checklists,
        Func<CommanderGameState?> state,
        PanelNavigator nav,
        PanelPrompts prompts)
    {
        if (crumb.Key.StartsWith(SlotPrefix, StringComparison.Ordinal))
        {
            var (buildId, slot) = SplitSlot(crumb.Key[SlotPrefix.Length..]);
            return new SlotPage(ships, checklists, state, prompts, buildId, slot);
        }

        if (crumb.Key.StartsWith(ShipPrefix, StringComparison.Ordinal))
        {
            return new ShipPage(ships, state, nav, prompts, crumb.Key[ShipPrefix.Length..]);
        }

        return new FleetPage(ships, nav, prompts);
    }

    /// <summary>The crumb for a ship, and for a slot of it.</summary>
    public static NavCrumb Ship(FleetEntry entry) =>
        new(ShipPrefix + (entry.Build?.Id ?? entry.Hull), entry.Name ?? entry.HullName);

    public static NavCrumb Slot(string buildId, string slot) =>
        new($"{SlotPrefix}{buildId}|{slot}", slot);

    private static (string BuildId, string Slot) SplitSlot(string key)
    {
        var at = key.IndexOf('|', StringComparison.Ordinal);

        return at < 0 ? (key, string.Empty) : (key[..at], key[(at + 1)..]);
    }

    // ------------------------------------------------------------------ shared drawing

    /// <summary>
    /// One pressable line of an index. <b>An index rather than a table</b>: one line each, a mark
    /// where a plan exists, and everything else in the pane that opens — which is what lets one
    /// layout survive from 512 to 2048 logical pixels.
    /// </summary>
    internal static Control Row(string text, string? aside, bool marked, Action pressed)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var body = new DockPanel();

        if (aside is { Length: > 0 })
        {
            var note = new TextBlock
            {
                Text = aside,
                FontSize = TypeScale.Secondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };

            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            DockPanel.SetDock(note, Dock.Right);
            body.Children.Add(note);
        }

        // The mark, and it is a mark rather than a column: "this slot has a plan" is a boolean,
        // and a column for it would be a column that is empty on most rows of most ships.
        if (marked)
        {
            var mark = new TextBlock
            {
                Text = "●",
                FontSize = TypeScale.Secondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };

            Themed(mark, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
            DockPanel.SetDock(mark, Dock.Left);
            body.Children.Add(mark);
        }

        body.Children.Add(label);

        var button = new Button
        {
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,

            // Tall enough for a ray at a metre. A row a Commander has to aim at is a row they
            // press twice, and the second press is on the one below it.
            MinHeight = 34,
            Padding = new Thickness(12, 6),
        };

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>
    /// The say-line along the bottom of a page: the phrase for what the Commander is looking at.
    /// <para>
    /// <b>How they learn it.</b> The ray points and the voice edits, so a page that offers no
    /// phrase is a page whose faster half is invisible.
    /// </para>
    /// </summary>
    internal static Control SayLine(string phrase)
    {
        var said = new TextBlock
        {
            Text = $"Say: “{phrase}”",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
        };

        Themed(said, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        return said;
    }

    internal static TextBlock Muted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        return block;
    }

    internal static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = TypeScale.Body,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 12, 0, 4),
    };

    internal static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));

    internal static ScrollViewer Scrolling(Control content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
}

/// <summary>
/// The fleet (list.md Phase 26, "The fleet, and the fleet you intend").
/// <para>
/// <b>A root rather than a level</b>, and it earns being landed on by answering where each ship is
/// and how its plans stand before anything is drilled.
/// </para>
/// </summary>
public sealed class FleetPage : UserControl
{
    private readonly ShipPlanService _ships;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _list = new() { Spacing = 3 };

    public FleetPage(ShipPlanService ships, PanelNavigator nav, PanelPrompts prompts)
    {
        _ships = ships;
        _nav = nav;
        _prompts = prompts;

        var intend = new Button
        {
            Content = "Plan a hull you do not own",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
        };

        intend.Click += (_, _) => Intend();

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("what have I planned");

        DockPanel.SetDock(intend, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(intend);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        Content = root;

        ships.Store.Changed += OnChanged;

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ships.Store.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        _list.Children.Clear();

        var fleet = _ships.Fleet();

        if (fleet.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(
                "I have not seen your fleet yet. Dock somewhere with a shipyard and I will read it "
                + "— or plan a hull you do not own, and buying one will point the plan at it."));

            return;
        }

        foreach (var entry in fleet)
        {
            var planned = entry.Planned;

            _list.Children.Add(LoadoutPages.Row(
                entry.Name is { Length: > 0 } name ? $"{name} ({entry.HullName})" : entry.HullName,

                // Where it is, and how its plans stand. The two questions this page exists to
                // answer before anything is drilled.
                planned > 0
                    ? $"{entry.Where()} · {planned} planned"
                    : entry.Where(),

                marked: planned > 0,
                () =>
                {
                    // A ship with no build yet gets one on the way in, so the page below always
                    // has something to plan against.
                    if (entry.Build is null && entry.Stored is { } stored)
                    {
                        _ships.BuildFor(stored.ShipId, stored.Type, stored.Name);
                    }

                    _nav.Drill(LoadoutPages.Ship(_ships.Fleet()
                        .First(again => again.Hull == entry.Hull && again.Name == entry.Name)));
                }));
        }
    }

    /// <summary>
    /// A hull the Commander does not own. Voice first, because a hull is a name and a name is far
    /// easier said than hunted for one key at a time.
    /// </summary>
    private void Intend()
    {
        _prompts.Enter(
            new EntryRequest(
                "loadout.intend",
                "Hull",
                "Which hull do you intend to buy?",
                "It is not in your fleet until you own one — acquiring it is the plan's first step.",
                string.Empty,
                EntrySurface.Voice,
                value => D47.Core.Knowledge.EliteSpecifications.Ship(value) is null
                    ? EntryVerdict.No($"I do not know a hull called “{value}”.")
                    : EntryVerdict.Ok),
            hull =>
            {
                _ships.Intend(hull);
                Refresh();
            });
    }
}

/// <summary>
/// One ship's slots (list.md Phase 26, "What is fitted and what you want").
/// <para>
/// <b>An index rather than a table</b>: one line per slot, a mark where a plan exists, and
/// everything else in the pane that opens. That is what lets one layout survive from 512 to 2048
/// logical pixels — a table wide enough to be worth having at 2048 is unreadable at 512.
/// </para>
/// </summary>
public sealed class ShipPage : UserControl
{
    private readonly ShipPlanService _ships;
    private readonly Func<CommanderGameState?> _state;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly string _buildId;
    private readonly StackPanel _list = new() { Spacing = 3 };
    private readonly TextBlock _summary = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
    };

    public ShipPage(
        ShipPlanService ships,
        Func<CommanderGameState?> state,
        PanelNavigator nav,
        PanelPrompts prompts,
        string buildId)
    {
        _ships = ships;
        _state = state;
        _nav = nav;
        _prompts = prompts;
        _buildId = buildId;

        LoadoutPages.Themed(_summary, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var promote = new Button
        {
            Content = "Put this build on my checklist",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
        };

        promote.Click += (_, _) =>
        {
            _summary.Text = _ships.Promote(_buildId);
        };

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("put that on my checklist");

        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(promote, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_summary);
        root.Children.Add(promote);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        Content = root;

        ships.Store.Changed += OnChanged;

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ships.Store.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        _list.Children.Clear();

        if (_ships.Store.Find(_buildId) is not { } build)
        {
            _list.Children.Add(LoadoutPages.Muted("That build is not there any more."));
            return;
        }

        _summary.Text = build.IsOwned
            ? $"{build.Describe()}. One build per ship: a slot holds one plan."
            : $"{build.Describe()}. Buying one will point this plan at it.";

        // Every slot the journal reports for the ship being flown, and every slot planned for any
        // other. A ship in another dock reports no modules at all, which is why the planned ones
        // have to stand on their own rather than being drawn as annotations on a fitted list.
        var fitted = Fitted(build);
        var slots = new List<string>();

        slots.AddRange(fitted.Select(module => module.Slot));

        foreach (var plan in build.Slots)
        {
            if (!slots.Any(slot => string.Equals(slot, plan.Slot, StringComparison.OrdinalIgnoreCase)))
            {
                slots.Add(plan.Slot);
            }
        }

        if (slots.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(
                "Nothing is planned, and I cannot see this ship's modules — Elite only reports the "
                + "loadout of the ship you are sitting in. Plan a slot and it will appear here."));

            return;
        }

        foreach (var slot in slots)
        {
            var plan = build.For(slot);
            var module = fitted.FirstOrDefault(candidate =>
                string.Equals(candidate.Slot, slot, StringComparison.OrdinalIgnoreCase));

            _list.Children.Add(LoadoutPages.Row(
                slot,
                plan is not null ? plan.Describe() : Describe(module),
                marked: plan is not null,
                () => _nav.Drill(LoadoutPages.Slot(_buildId, slot))));
        }
    }

    private IReadOnlyList<ShipModule> Fitted(ShipBuild build)
    {
        var loadout = _state()?.Ship;

        return loadout is { IsKnown: true } && loadout.ShipId == build.ShipId
            ? loadout.Modules
            : [];
    }

    private static string? Describe(ShipModule? module) =>
        module is null ? null : ChecklistNaming.Readable(module.Item);
}

/// <summary>
/// One slot (list.md Phase 26, "What is fitted and what you want").
/// <para>
/// <b>Fitted and planned are two blocks and never one merged line</b>, because a plan is a second
/// thing the Commander wants rather than an edit to the truth.
/// </para>
/// <para>
/// <b>A plan carries the journal's verdict with its date and no checkbox.</b>
/// <see cref="ChecklistEvaluator"/> already answers null for <em>nothing can be said right now</em>,
/// which is what a ship you are not flying looks like — so the page says <em>not fitted, as of
/// three days ago</em> rather than showing a blank that implies disagreement.
/// </para>
/// </summary>
public sealed class SlotPage : UserControl
{
    private readonly ShipPlanService _ships;
    private readonly ChecklistService _checklists;
    private readonly Func<CommanderGameState?> _state;
    private readonly PanelPrompts _prompts;
    private readonly string _buildId;
    private readonly string _slot;
    private readonly StackPanel _body = new() { Spacing = 4 };

    public SlotPage(
        ShipPlanService ships,
        ChecklistService checklists,
        Func<CommanderGameState?> state,
        PanelPrompts prompts,
        string buildId,
        string slot)
    {
        _ships = ships;
        _checklists = checklists;
        _state = state;
        _prompts = prompts;
        _buildId = buildId;
        _slot = slot;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine($"plan grade 5 dirty drives on {slot}");

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        ships.Store.Changed += OnChanged;

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ships.Store.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        _body.Children.Clear();

        if (_ships.Store.Find(_buildId) is not { } build)
        {
            _body.Children.Add(LoadoutPages.Muted("That build is not there any more."));
            return;
        }

        Fitted(build);
        Planned(build);
    }

    /// <summary>What is actually there, from the journal. The truth block.</summary>
    private void Fitted(ShipBuild build)
    {
        _body.Children.Add(LoadoutPages.Heading("Fitted"));

        var loadout = _state()?.Ship;

        if (loadout is not { IsKnown: true } || loadout.ShipId != build.ShipId)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "Elite reports the loadout of the ship you are sitting in and no other, so I cannot "
                + "say what is in this slot right now."));

            return;
        }

        var module = loadout.Modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Slot, _slot, StringComparison.OrdinalIgnoreCase));

        if (module is null)
        {
            _body.Children.Add(LoadoutPages.Muted("Nothing."));
            return;
        }

        _body.Children.Add(new TextBlock
        {
            Text = ChecklistNaming.Readable(module.Item),
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        });

        if (module.Blueprint is { Length: > 0 } blueprint)
        {
            var grade = module.BlueprintLevel is { } level ? $"grade {level} " : string.Empty;

            _body.Children.Add(LoadoutPages.Muted(
                $"{grade}{ChecklistNaming.Readable(blueprint)}"
                + (module.Experimental is { Length: > 0 } effect ? $", {effect}" : string.Empty)));
        }
    }

    /// <summary>
    /// What the Commander wants, with the journal's verdict and what it costs. Its own block, and
    /// never merged into the one above.
    /// </summary>
    private void Planned(ShipBuild build)
    {
        _body.Children.Add(LoadoutPages.Heading("Planned"));

        var plan = build.For(_slot);

        if (plan is null)
        {
            _body.Children.Add(LoadoutPages.Muted("Nothing planned for this slot."));
            Buttons(planned: false);
            return;
        }

        _body.Children.Add(new TextBlock
        {
            Text = plan.Describe(),
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        });

        Verdict(build, plan);
        Cost(build, plan);
        Buttons(planned: true);
    }

    /// <summary>
    /// The journal's verdict, as of when it was taken. <b>No checkbox</b>: a derived item's
    /// progress is a diff against live state, and a tick here would be undone or left standing
    /// and lying by the next read.
    /// </summary>
    private void Verdict(ShipBuild build, SlotPlan plan)
    {
        if (build.Scope is not { } scope)
        {
            return;
        }

        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, plan.Slot)
        {
            Detail = plan.Blueprint,
            Grade = plan.Grade,
            Engineer = plan.Engineer,
        };

        var item = new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = scope,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = plan.Describe(),
            Intent = intent,
            Hull = build.Hull,
        };

        var verdict = ChecklistEvaluator.Evaluate(item, _state());

        var said = verdict?.Reason
                   ?? "Nothing can be said about this right now — Elite reports the loadout of the "
                      + "ship you are sitting in and no other.";

        var line = LoadoutPages.Muted(said);

        if (verdict is { } answered && ChecklistNextAction.IsWrong(answered.State))
        {
            LoadoutPages.Themed(line, TextBlock.ForegroundProperty, ThemeManager.DangerKey);
        }

        _body.Children.Add(line);
    }

    /// <summary>
    /// What this plan costs, on the slot. <b>Per plan</b>, because that is the question a
    /// Commander looking at one slot is asking — the shared-cap arithmetic across every plan at
    /// once is what the shortfall page is for.
    /// </summary>
    private void Cost(ShipBuild build, SlotPlan plan)
    {
        if (build.Scope is not { } scope)
        {
            return;
        }

        var items = EngineeringPlan.Items(
            scope, build.Hull, [plan.ToRequest()], _checklists.SlotFor);

        var costing = EngineeringPlan.Cost(items, _state());

        foreach (var gate in costing.Gates)
        {
            var blocked = LoadoutPages.Muted(gate);

            LoadoutPages.Themed(blocked, TextBlock.ForegroundProperty, ThemeManager.DangerKey);
            _body.Children.Add(blocked);
        }

        if (costing.Ingredients.Count == 0)
        {
            return;
        }

        _body.Children.Add(LoadoutPages.Heading("What it costs"));

        foreach (var ingredient in costing.Ingredients.OrderByDescending(entry => entry.Short))
        {
            // Held, needed and short, all three. "Short 12" alone is a number a Commander cannot
            // check, and the arithmetic is exact rather than estimated: an application costs one
            // of each ingredient and the roll count is a published function of grade and rank.
            _body.Children.Add(LoadoutPages.Muted(
                $"{ingredient.Material.Name}: {ingredient.Held} of {ingredient.Needed}"
                + (ingredient.Short > 0 ? $", {ingredient.Short} short" : string.Empty)
                + (ingredient.ExceedsCapacity ? " — more than one trip" : string.Empty)));
        }
    }

    private void Buttons(bool planned)
    {
        var plan = new Button
        {
            Content = planned ? "Change the plan" : "Plan this slot",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
        };

        plan.Click += (_, _) => Ask();

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { plan },
        };

        if (planned)
        {
            var clear = new Button
            {
                Content = "Clear it",
                Padding = new Thickness(12, 4),
                MinHeight = 30,
            };

            clear.Click += (_, _) =>
            {
                _ships.Clear(_buildId, _slot);
                Refresh();
            };

            row.Children.Add(clear);
        }

        _body.Children.Add(row);
    }

    /// <summary>
    /// Asks for the blueprint, then the grade. Voice first for the name and the keyboard for the
    /// number, which is the per-call-site declaration Phase 25 asks for: a blueprint is a phrase
    /// and a grade is a digit.
    /// </summary>
    private void Ask()
    {
        _prompts.Enter(
            new EntryRequest(
                "loadout.blueprint",
                "Blueprint",
                $"What do you want on {_slot}?",
                "A blueprint by name. It does not reach your checklist until you promote the build.",
                _ships.Store.Find(_buildId)?.For(_slot)?.Blueprint ?? string.Empty,
                EntrySurface.Voice),
            blueprint => _prompts.Enter(
                new EntryRequest(
                    "loadout.grade",
                    "Grade",
                    $"Which grade of {blueprint}?",
                    "1 to 5, or leave it empty for any grade — which is a real answer rather than "
                    + "an unknown.",
                    string.Empty,
                    EntrySurface.Keyboard,
                    value => value.Trim().Length == 0
                             || (int.TryParse(value.Trim(), out var grade) && grade is >= 1 and <= 5)
                        ? EntryVerdict.Ok
                        : EntryVerdict.No("A grade is 1 to 5, or nothing at all for any.")),
                grade =>
                {
                    _ships.Plan(_buildId, new SlotPlan(
                        _slot,
                        string.IsNullOrWhiteSpace(blueprint) ? null : blueprint.Trim(),
                        int.TryParse(grade.Trim(), out var level) ? level : null));

                    Refresh();
                }));
    }
}
