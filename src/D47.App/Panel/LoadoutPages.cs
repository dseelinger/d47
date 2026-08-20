using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's pages: an index, then an item, then a slot — drawn once and shown for every
/// mode (list.md Phase 26, "Ships"; Phase 27, "The same page, on foot").
/// <para>
/// Three levels of one drill stack rather than three screens, so the reflow, the breadcrumb and
/// the phrases Phase 25 built cover all of them without any of them knowing.
/// </para>
/// <para>
/// <b>Nothing here knows what a hull or a suit is.</b> An <see cref="ILoadoutMode"/> is the whole
/// of the difference between Ships and Suits, which is what makes "one page kind built once and
/// shown twice" true rather than aspirational — the alternative, two page sets that look the same
/// on the day they are written, is two page sets that stop looking the same a fortnight later.
/// </para>
/// <para>
/// <b>The ray points and the voice edits</b>, so every page here optimises for being <em>read</em>
/// rather than for manipulation density — one utterance does what six ray presses would, and the
/// say-line along the bottom is how a Commander learns the phrase for what they are looking at.
/// </para>
/// </summary>
public static class LoadoutPages
{
    /// <summary>The Loadout tab's Ships root.</summary>
    public const string FleetRoot = "loadout.ships";

    /// <summary>How a ship's crumb is keyed, so a page can be rebuilt from the trail alone.</summary>
    public const string ShipPrefix = "loadout.ship:";

    /// <summary>And a slot's, below it.</summary>
    public const string SlotPrefix = "loadout.slot:";

    /// <summary>The third mode: what every plan needs that the Commander is not carrying.</summary>
    public const string GapRoot = "loadout.gap";

    /// <summary>
    /// Draws whichever level a crumb names. Handed to <see cref="PanelView.Furnish"/>, so the
    /// drill strip asks for a page when it first shows one and keeps it afterwards.
    /// </summary>
    /// <param name="gap">
    /// Where the gap gets its arithmetic, or null for a surface with no on-foot half — the tab
    /// then has the one root Phase 26 gave it.
    /// </param>
    public static Control Build(
        NavCrumb crumb,
        IReadOnlyList<ILoadoutMode> modes,
        GapSource? gap,
        PanelNavigator nav,
        PanelPrompts prompts)
    {
        foreach (var mode in modes)
        {
            if (crumb.Key.StartsWith(mode.SlotPrefix, StringComparison.Ordinal))
            {
                var (item, slot) = SplitSlot(crumb.Key[mode.SlotPrefix.Length..]);
                return new SlotPage(mode, prompts, item, slot);
            }

            if (crumb.Key.StartsWith(mode.ItemPrefix, StringComparison.Ordinal))
            {
                return new ItemPage(mode, nav, crumb.Key[mode.ItemPrefix.Length..], prompts);
            }
        }

        if (crumb.Key == GapRoot && gap is not null)
        {
            return new GapPage(gap);
        }

        var root = modes.FirstOrDefault(mode => mode.RootKey == crumb.Key) ?? modes[0];

        return new IndexPage(root, nav, prompts);
    }

    /// <summary>The crumb for a ship, and for a slot of it. Kept from Phase 26.</summary>
    public static NavCrumb Ship(D47.Core.Ships.FleetEntry entry) =>
        new(ShipPrefix + (entry.Build?.Id ?? entry.Hull), entry.Name ?? entry.HullName);

    public static NavCrumb Slot(string buildId, string slot) =>
        new($"{SlotPrefix}{buildId}|{slot}", slot);

    /// <summary>The crumb for one row of an index, and for one slot below it.</summary>
    /// <summary>
    /// One item — a ship, a suit, a weapon.
    /// <para>
    /// Levelled, so choosing another one replaces it rather than nesting under it
    /// (remediation.md 11, item 5). A wide panel keeps the index on screen beside the item, so the
    /// list is still pressable while an item is open, and without this it produced
    /// <c>Ships › Tulimiekka › Reaper › Cartage</c> — a trail through three ships at once.
    /// </para>
    /// </summary>
    public static NavCrumb Crumb(ILoadoutMode mode, LoadoutRow row) =>
        new(mode.ItemPrefix + row.Key, row.Word) { Level = mode.ItemPrefix };

    /// <summary>
    /// One slot of one item. Levelled for the same reason, one level down — and because changing
    /// item drops it, a slot of the ship you just left cannot stay on the trail.
    /// </summary>
    public static NavCrumb SlotCrumb(ILoadoutMode mode, LoadoutRow row) =>
        new(mode.SlotPrefix + row.Key, row.Word) { Level = mode.SlotPrefix };

    internal static (string Item, string Slot) SplitSlot(string key)
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
    internal static Control Row(
        string text, string? aside, bool marked, Action pressed, bool engineered = false)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // A grid rather than a dock, because a right-docked child takes as much width as it asks
        // for and the fill child gets what is left (remediation.md 14, item 1). A plan naming a
        // module, a blueprint, a grade and an experimental effect asked for all of it, so "Large
        // Hardpoint 1" was left with nothing — measured at zero pixels — and wrapped one
        // character per line into a row three hundred pixels tall.
        //
        // The name's column keeps a floor and the note gives, which is the right way round: the
        // note says what is in the slot and the name says which slot, and a row whose name cannot
        // be read is a row that cannot be pressed on purpose.
        var body = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star) { MinWidth = 120 },
                new ColumnDefinition(GridLength.Auto),
            ],
        };

        if (aside is { Length: > 0 })
        {
            var note = new TextBlock
            {
                Text = aside,
                FontSize = TypeScale.Secondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),

                // Wrapped, so a note too long for what is left of the row becomes two lines
                // rather than pushing the name out of the way.
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Right,
            };

            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            Grid.SetColumn(note, 2);
            body.Children.Add(note);

            // Capped against the row's own width, because an Auto column measures a wrapping
            // block as though it had forever: without this the note asks for its whole width on
            // one line, is given less, and is cut off mid-word rather than wrapping. A share
            // rather than a number, so it holds from a 512-pixel panel to a 2048-pixel one.
            body.SizeChanged += (_, size) =>
                note.MaxWidth = Math.Max(80, size.NewSize.Width * 0.6);
        }

        // The marks, and they are marks rather than columns: "this slot has a plan" and "this
        // module is engineered" are booleans, and a column for either would be a column that is
        // empty on most rows of most ships.
        //
        // **Two independent facts, and which is which has to be readable at a glance**
        // (remediation.md 15, item 10). The dot means a plan exists and the gear means a roll has
        // been done, so a row carries neither, either or both — in the reported screenshot the
        // Power Distributor was engineered with no plan while the Power Plant was both. Different
        // glyphs and different colours, because two marks distinguished only by hue are one mark
        // to a Commander who cannot separate the hues.
        //
        // **The gear goes after the name, and it goes inside it** (remediation.md 17, item 10).
        // Reported as *"Gear Glyph should appear to the right of the Module Name, not leftmost"*.
        //
        // An inline rather than a fourth column, because the name's column is the star one: a
        // glyph in a column of its own would sit against the note on the far side of the row, and
        // the gap between the name and its own mark would grow every time the window widened. An
        // inline travels with the last word — including when the name wraps, which a sibling in a
        // panel would not.
        if (engineered)
        {
            label.Inlines =
            [
                new Run(text),
                Gear(),
            ];
        }

        // The dot stays in the left gutter, which is the Commander's call rather than a
        // consequence: the two marks answer different questions, and a plan existing is a fact
        // about the *row* where a roll having been done is a fact about the module named in it.
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
            body.Children.Add(mark);
        }

        Grid.SetColumn(label, 1);

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

    /// <summary>One line of a page, drawn the way its tone says.</summary>
    /// <summary>
    /// A line, with its stepper beside it where it has one (remediation.md 15, item 4).
    /// <para>
    /// <b>Beside the text and not inside it.</b> The line is one string that is both shown and
    /// spoken — <see cref="Ships.SlotPlan.Describe"/> — so the grade stays part of the sentence and
    /// the control sits next to it, rather than the sentence being cut into pieces around a
    /// widget.
    /// </para>
    /// </summary>
    internal static Control Stepped(LoadoutLine line)
    {
        if (line.Step is not { } step)
        {
            return Line(line);
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(Line(line));

        // **The grade last, then the buttons that move it** (remediation.md 17, item 11). Reported
        // as *"it is not clear that the step controls for the engineering grade are associated with
        // it"* — and they were not: the number was inside the sentence, with a blueprint name, an
        // experimental effect and an engineer between it and the arrows at the end of the row.
        //
        // Drawn from the step's own value rather than lifted out of the sentence. Splitting
        // `Describe()`'s output to find "grade 3" in it would be a second parser of d47's own
        // prose, wrong the day a blueprint is named something with the word in it — so the
        // sentence is asked to leave the grade out and this puts it back, from the same field the
        // buttons move.
        var grade = new TextBlock
        {
            Text = $"Grade {step.Value.ToString(CultureInfo.InvariantCulture)}",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(grade);

        // Highest first, which is how the offer is ordered and how a Commander reads a grade: up
        // is better. The buttons stop where the recipe stops rather than at five.
        var at = step.Offered.ToList().IndexOf(step.Value);

        row.Children.Add(Nudge("▲", at > 0, () => step.Set(step.Offered[at - 1])));
        row.Children.Add(Nudge("▼", at >= 0 && at < step.Offered.Count - 1, () => step.Set(step.Offered[at + 1])));

        return row;
    }

    private static Button Nudge(string glyph, bool live, Action pressed)
    {
        var button = new Button
        {
            Content = glyph,
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(8, 2),
            IsEnabled = live,
            VerticalAlignment = VerticalAlignment.Center,
        };

        button.Click += (_, _) => pressed();

        return button;
    }

    internal static TextBlock Line(LoadoutLine line) => line.Tone switch
    {
        LoadoutTone.Heading => Heading(line.Text),
        LoadoutTone.Body => new TextBlock
        {
            Text = line.Text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        },
        LoadoutTone.Danger => Toned(line.Text, ThemeManager.DangerKey),

        // What was done to the module, in its own colour (remediation.md 15, item 10). Info
        // rather than Accent, because Accent already means "a plan exists" on the dot beside the
        // row and the two facts are independent — a module can be engineered with no plan.
        LoadoutTone.Engineered => Toned(line.Text, ThemeManager.InfoKey),
        _ => Muted(line.Text),
    };

    internal static TextBlock Muted(string text) => Toned(text, ThemeManager.TextMutedKey);

    internal static TextBlock Toned(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    internal static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = TypeScale.Body,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 12, 0, 4),
    };

    internal static Button Press(string label, Action pressed)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(12, 4),
            MinHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>
    /// The mark meaning a roll has been done, as an inline so it travels with the name it is
    /// about (remediation.md 17, item 10).
    /// <para>
    /// A leading space rather than a margin: an inline's spacing is part of the text, and a
    /// margin on a <see cref="Run"/> is not a thing the text layout would honour.
    /// </para>
    /// </summary>
    private static Run Gear()
    {
        var gear = new Run(" ⚙");

        Themed(gear, Run.ForegroundProperty, ThemeManager.TextMutedKey);

        return gear;
    }

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
/// A page that redraws itself when its mode changes underneath. The three levels all do, and
/// unsubscribing on detach is the part that is easy to forget once rather than three times.
/// <para>
/// <b>Attach to detach, and catch up on the way in</b> (remediation.md 13, item 1; the same fault
/// and the same fix as remediation.md 11, item 3). Subscribing in the constructor and
/// unsubscribing on detach is not a pair: the drill strip <em>caches</em> its levels, so a level
/// that scrolls out of the visible panes is detached and put back later — and in between it had
/// unsubscribed and gone deaf for the rest of the session. Dropping a hull then left it on the
/// fleet list, which is the one place a Commander looks to find out whether the drop took.
/// </para>
/// <para>
/// It only shows on a narrow panel, which is why it survived a batch that fixed the identical
/// thing on the checklist: at 1024 pixels the index stays beside the ship, never detaches, and
/// keeps hearing. One pane, and it does not.
/// </para>
/// </summary>
public abstract class LoadoutPage : UserControl
{
    private readonly ILoadoutMode _mode;

    protected LoadoutPage(ILoadoutMode mode) => _mode = mode;

    protected ILoadoutMode Mode => _mode;

    protected abstract void Refresh();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _mode.Changed += OnChanged;

        // Being put back by the strip means having missed whatever happened while it was out, so
        // a page catches up rather than trusting what it last drew.
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _mode.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);
}

/// <summary>
/// The index (list.md Phase 26, "The fleet, and the fleet you intend"; Phase 27, "The same page,
/// on foot").
/// <para>
/// <b>A root rather than a level</b>, and it earns being landed on by answering where each thing
/// is and how its plans stand before anything is drilled.
/// </para>
/// </summary>
public sealed class IndexPage : LoadoutPage
{
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _list = new() { Spacing = 3 };

    public IndexPage(ILoadoutMode mode, PanelNavigator nav, PanelPrompts prompts)
        : base(mode)
    {
        _nav = nav;
        _prompts = prompts;

        var intend = LoadoutPages.Press(mode.NewLabel, Intend);

        intend.Margin = new Thickness(0, 0, 0, 10);

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine(mode.SayAtIndex);

        DockPanel.SetDock(intend, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(intend);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        Content = root;

        Refresh();
    }

    protected override void Refresh()
    {
        _list.Children.Clear();

        var rows = Mode.Items();

        if (rows.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(Mode.EmptyIndex));
            return;
        }

        foreach (var row in rows)
        {
            _list.Children.Add(LoadoutPages.Row(
                row.Text,
                row.Aside,
                row.Marked,
                () => _nav.Drill(LoadoutPages.Crumb(Mode, row)),
                row.Engineered));
        }
    }

    private void Intend() => Mode.New(_prompts, Refresh);
}

/// <summary>
/// One item's slots (list.md Phase 26, "What is fitted and what you want").
/// <para>
/// <b>An index rather than a table</b>: one line per slot, a mark where a plan exists, and
/// everything else in the pane that opens. That is what lets one layout survive from 512 to 2048
/// logical pixels — a table wide enough to be worth having at 2048 is unreadable at 512.
/// </para>
/// </summary>
public sealed class ItemPage : LoadoutPage
{
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts? _prompts;
    private readonly string _item;
    private readonly Button? _drop;
    private readonly StackPanel _list = new() { Spacing = 3 };
    private readonly TextBlock _summary = new()
    {
        FontSize = TypeScale.Secondary,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
    };

    public ItemPage(ILoadoutMode mode, PanelNavigator nav, string item)
        : this(mode, nav, item, prompts: null)
    {
    }

    public ItemPage(ILoadoutMode mode, PanelNavigator nav, string item, PanelPrompts? prompts)
        : base(mode)
    {
        _nav = nav;
        _item = item;
        _prompts = prompts;

        LoadoutPages.Themed(_summary, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var promote = LoadoutPages.Press(mode.PromoteLabel, () => _summary.Text = Mode.Promote(_item));

        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { promote },
        };

        // Only where there is something to drop — an intended hull rather than an owned ship
        // (remediation.md 11, item 7). Built once here rather than in Refresh, because a build
        // does not stop being intended while the Commander is looking at it: buying the hull
        // arrives as a fresh page.
        if (mode.DropLabel(item) is { Length: > 0 } label)
        {
            _drop = LoadoutPages.Press(label, Drop);
            actions.Children.Add(_drop);
        }

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine(mode.SayAtItem);

        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(actions, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_summary);
        root.Children.Add(actions);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        Content = root;

        Refresh();
    }

    /// <summary>
    /// Drops the plan, having asked (remediation.md 11, item 7).
    /// <para>
    /// Asked as a chooser rather than a dialog, because a popup cannot exist in the VR path at
    /// all — and asked at all because this is the one control on the page with no way back: a
    /// planned hull is authored, so dropping it discards work rather than a derived view of it.
    /// </para>
    /// <para>
    /// Straight through where the page was built without prompts, which is a designer and a test
    /// rather than anything a Commander uses.
    /// </para>
    /// </summary>
    private void Drop()
    {
        if (_prompts is null)
        {
            Dropped();
            return;
        }

        _prompts.Choose(
            new ChoiceRequest(
                "loadout.drop",
                "Drop",
                _drop?.Content as string ?? "Drop this",
                Mode.Summary(_item) is { } what
                    ? $"{what} There is no way back from this one."
                    : "There is no way back from this one.",
                [new ChoiceOption("keep", "Keep it"), new ChoiceOption("drop", "Drop it")],
                "keep",
                ChoiceSurface.Layer)
            {
                CurrentWord = "chosen now",
            },
            option =>
            {
                if (option.Key == "drop")
                {
                    Dropped();
                }
            });
    }

    /// <summary>
    /// Drops it and says so on the summary line, which is where <c>Promote</c> already reports.
    /// <para>
    /// The Commander is left here rather than sent back, because the sentence is worth reading:
    /// dropping a build that had already put lines on the checklist says that those lines are
    /// still there. Refreshing turns the rest of the page into "that build is not there any
    /// more", so the level says plainly that it is spent and Back is the obvious next press.
    /// </para>
    /// </summary>
    private void Dropped()
    {
        var said = Mode.Drop(_item);

        Refresh();

        _summary.Text = said;
    }

    protected override void Refresh()
    {
        _list.Children.Clear();

        if (Mode.Summary(_item) is not { } summary)
        {
            _list.Children.Add(LoadoutPages.Muted("That build is not there any more."));
            return;
        }

        _summary.Text = summary;

        // What the ship is, before what is in it (remediation.md 13, item 2). Inside the
        // scroller rather than docked above it, or a hull's figures would cost the slot list the
        // same rows on every window.
        foreach (var line in Mode.Details(_item))
        {
            _list.Children.Add(LoadoutPages.Line(line));
        }

        var rows = Mode.Slots(_item);

        if (rows.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(Mode.EmptySlots));
            return;
        }


        // A heading wherever the group changes, so a ship's slots read as the four blocks of the
        // outfitting screen rather than as thirty-odd names in journal order
        // (remediation.md 12, item 1). A mode that groups nothing draws nothing extra.
        var group = (string?)null;

        foreach (var row in rows)
        {
            if (row.Group is { Length: > 0 } heading && heading != group)
            {
                group = heading;
                _list.Children.Add(LoadoutPages.Heading(heading));
            }

            var control = LoadoutPages.Row(
                row.Text,
                row.Aside,
                row.Marked,
                () => _nav.Drill(LoadoutPages.SlotCrumb(Mode, row)),
                row.Engineered);

            Draggable(control, row);

            _list.Children.Add(control);
        }
    }

    /// <summary>
    /// Ctrl and the left button held, dragged from one slot row to another, copies the plan
    /// (remediation.md 15, item 1).
    /// <para>
    /// <b>Pointer events rather than the drag-and-drop framework.</b> The copy never leaves this
    /// page — there is no other window to drop on and nothing to negotiate a format with — so the
    /// framework would buy a clipboard round trip and an API that has already moved once, in
    /// exchange for nothing. Two events and a field do it, and they can be raised by a test.
    /// </para>
    /// <para>
    /// <b>Ctrl rather than a bare drag</b>, because a slot row's first job is to be pressed: a
    /// plain drag would turn every mis-aimed click into a plan being moved.
    /// </para>
    /// <para>
    /// <b>An invalid target refuses during the drag rather than after the drop.</b> The row under
    /// the pointer is asked whether it would take the plan and greyed where it would not, so a
    /// Plasma Accelerator that does not come small enough shows that while the mouse is still
    /// down — not in a dialog a second later.
    /// </para>
    /// </summary>
    private void Draggable(Control control, LoadoutRow row)
    {
        var slot = LoadoutPages.SplitSlot(row.Key).Slot;

        control.AddHandler(
            InputElement.PointerPressedEvent,
            (_, args) =>
            {
                var point = args.GetCurrentPoint(control);

                if (point.Properties.IsLeftButtonPressed
                    && args.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    _dragging = slot;
                    args.Handled = true;
                }
            },
            RoutingStrategies.Tunnel);

        control.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, args) =>
            {
                if (_dragging is not { } from || from == slot)
                {
                    _dragging = null;
                    return;
                }

                _dragging = null;
                args.Handled = true;

                // Refused rather than explained, matching what the row showed during the drag.
                if (!Mode.CanCopy(_item, from, slot))
                {
                    return;
                }

                _summary.Text = Mode.Copy(_item, from, slot);
                Refresh();
            },
            RoutingStrategies.Tunnel);

        // What the row looks like while a plan is over it: dimmed where it would refuse, so the
        // Commander learns the rule from the interface rather than from a message.
        control.PointerEntered += (_, _) =>
        {
            if (_dragging is { } from && from != slot)
            {
                control.Opacity = Mode.CanCopy(_item, from, slot) ? 1 : 0.4;
            }
        };

        control.PointerExited += (_, _) => control.Opacity = 1;
    }

    /// <summary>The slot a plan is being dragged from, or null when nothing is in flight.</summary>
    private string? _dragging;
}

/// <summary>
/// One slot (list.md Phase 26, "What is fitted and what you want").
/// <para>
/// <b>Fitted and planned are two blocks and never one merged line</b>, because a plan is a second
/// thing the Commander wants rather than an edit to the truth.
/// </para>
/// <para>
/// <b>A plan carries the journal's verdict with its date and no checkbox.</b> The evaluator
/// already answers null for <em>nothing can be said right now</em>, which is what a ship you are
/// not flying — or a suit you are not wearing — looks like, so the page says <em>not fitted, as of
/// three days ago</em> rather than showing a blank that implies disagreement.
/// </para>
/// </summary>
public sealed class SlotPage : LoadoutPage
{
    private readonly PanelPrompts _prompts;
    private readonly string _item;
    private readonly string _slot;
    private readonly StackPanel _body = new() { Spacing = 4 };

    public SlotPage(ILoadoutMode mode, PanelPrompts prompts, string item, string slot)
        : base(mode)
    {
        _prompts = prompts;
        _item = item;
        _slot = slot;

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine(mode.SayAtSlot(slot));

        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

    protected override void Refresh()
    {
        _body.Children.Clear();

        _body.Children.Add(LoadoutPages.Heading("Fitted"));

        foreach (var line in Mode.Fitted(_item, _slot))
        {
            _body.Children.Add(LoadoutPages.Line(line));
        }

        _body.Children.Add(LoadoutPages.Heading("Planned"));

        foreach (var line in Mode.Planned(_item, _slot))
        {
            // Stepped, because the grade on this page is a control: moving it re-costs the block
            // below without leaving the page (remediation.md 15, item 4).
            _body.Children.Add(LoadoutPages.Stepped(line));
        }

        Buttons(Mode.HasPlan(_item, _slot));
    }

    private void Buttons(bool planned)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                LoadoutPages.Press(
                    planned ? "Change the plan" : "Plan this slot",
                    () => Mode.Ask(_item, _slot, _prompts, Refresh)),
            },
        };

        if (planned)
        {
            row.Children.Add(LoadoutPages.Press("Clear it", () =>
            {
                Mode.Clear(_item, _slot);
                Refresh();
            }));
        }

        _body.Children.Add(row);
    }
}

/// <summary>
/// The gap between every plan and what the Commander is carrying (list.md Phase 27, "Gap
/// analysis").
/// <para>
/// <b>A third root-only mode reading across both the others</b>, because a Commander gathering
/// materials does not care which ship wanted them. <b>Not called a wishlist</b>: a wishlist is a
/// list of things you want, which is what the plans are — this is the arithmetic between what they
/// need and what you are holding.
/// </para>
/// <para>
/// <b>The ledgers are never totalled together</b>, and the one figure that spans everything counts
/// units still to find, which is a shopping list rather than a balance.
/// </para>
/// </summary>
public sealed class GapPage : UserControl
{
    private readonly GapSource _gap;
    private readonly StackPanel _body = new() { Spacing = 4 };
    private readonly Button _filter;

    private bool _includeIntended = true;

    public GapPage(GapSource gap)
    {
        _gap = gap;

        gap.Changed += OnChanged;

        _filter = LoadoutPages.Press(string.Empty, () =>
        {
            _includeIntended = !_includeIntended;
            Refresh();
        });

        _filter.Margin = new Thickness(0, 0, 0, 10);

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine("what do my plans still need");

        DockPanel.SetDock(_filter, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_filter);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_body));

        Content = root;

        Refresh();
    }

    /// <summary>Redraws against the live plans. The tab calls this when it is shown.</summary>
    public void Refresh()
    {
        _body.Children.Clear();

        var report = _gap.Of(_includeIntended);

        // The filter, and it says which question it is answering rather than merely which state it
        // is in: counting hulls nobody owns is honest about the whole ambition, and excluding them
        // answers what can be finished now. Both are real questions.
        _filter.Content = _includeIntended
            ? "Counting what you do not own yet — show only what you can finish now"
            : "Only what you own — count the ones you intend to buy too";

        if (report.Plans == 0)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "Nothing is planned yet. Plan a slot on a ship, or a grade on a suit, and what it "
                + "needs shows up here."));

            return;
        }

        if (report.IsEmpty)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "You are carrying everything your plans need. Nothing to go and find."));

            return;
        }

        _body.Children.Add(new TextBlock
        {
            Text = $"{report.UnitsToFind.ToString(CultureInfo.InvariantCulture)} units still to find, "
                   + $"across {report.Plans.ToString(CultureInfo.InvariantCulture)} plan"
                   + (report.Plans == 1 ? string.Empty : "s") + ".",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        });

        _body.Children.Add(LoadoutPages.Muted(
            "A count of things to go and get, never a balance — the ledgers below have separate "
            + "caps and no exchange between them, so they are never added up."));

        foreach (var gate in report.Gates)
        {
            _body.Children.Add(LoadoutPages.Toned(gate, ThemeManager.DangerKey));
        }

        foreach (var ledger in report.Ledgers)
        {
            _body.Children.Add(LoadoutPages.Heading(
                $"{ledger.Name} — {ledger.UnitsToFind.ToString(CultureInfo.InvariantCulture)} to find"));

            foreach (var line in ledger.Lines)
            {
                Draw(line);
            }
        }

        foreach (var unknown in report.Uncovered)
        {
            _body.Children.Add(LoadoutPages.Muted(unknown));
        }
    }

    /// <summary>
    /// Redrawn on the way in as well as on a change, because the third thing this page reads is
    /// the Commander's own inventory — which moves without either store having said anything.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _gap.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    private void Draw(GapLine line)
    {
        _body.Children.Add(new TextBlock
        {
            Text = $"{line.Material.Name}: {line.Short.ToString(CultureInfo.InvariantCulture)} short "
                   + $"({line.Held.ToString(CultureInfo.InvariantCulture)} of "
                   + $"{line.Needed.ToString(CultureInfo.InvariantCulture)})",
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });

        if (line.ExceedsCapacity)
        {
            _body.Children.Add(LoadoutPages.Toned(
                $"You can only hold {line.Capacity?.ToString(CultureInfo.InvariantCulture)}. That is "
                + "at least two trips whatever happens.",
                ThemeManager.DangerKey));
        }

        // Trade second and never instead: the headline stays the honest raw shortfall.
        if (line.Trade is { } trade)
        {
            _body.Children.Add(LoadoutPages.Muted(trade.Describe()));
        }

        // What wants it. This is what makes the roll-up navigable instead of merely a total.
        if (line.Wanted.Count > 0)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "Wanted by: " + string.Join(", ", line.Wanted.Select(demand => demand.Describe()))));
        }
    }
}
