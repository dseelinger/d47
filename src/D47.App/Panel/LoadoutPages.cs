using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;

using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's pages: an index, then an item, then a slot — drawn once and shown for every
/// mode (Phase 26, "Ships"; Phase 27, "The same page, on foot").
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
        new(mode.SlotPrefix + row.Key, row.Word)
        {
            Level = mode.SlotPrefix,

            // A slot is where a blueprint is chosen, so its help is the blueprint page rather
            // than the fleet page the root declares. The mode says which, because the two modes
            // do not mean the same thing by a slot.
            Help = mode.SlotHelp,
        };

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
        if (line.Copy is { } copy)
        {
            // Beside the text for the same reason the stepper is: the line is one string that is
            // both shown and spoken, so it keeps its sentence and the control sits next to it.
            var copyable = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };

            copyable.Children.Add(Line(line));
            copyable.Children.Add(CopyGlyph(copy));

            return copyable;
        }

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

    /// <summary>
    /// The glyph that puts one value on the clipboard — a system name, so it can be pasted into
    /// the Galaxy Map's search.
    /// <para>
    /// <b>Asks its own visual root for the clipboard</b>, which is the call the transcript's Copy
    /// already makes and for the same reason: the headset's host window is never shown and its
    /// <c>TopLevel.Clipboard</c> is null. The button is then shut rather than throwing on a quad
    /// a metre away — a control that can be pressed and does nothing teaches the wrong thing
    /// about what the panel can do.
    /// </para>
    /// <para>
    /// The confirmation is on the glyph itself. It is a one-click action, and a failure — another
    /// application holding the clipboard — is otherwise indistinguishable from having worked.
    /// </para>
    /// </summary>
    private static Button CopyGlyph(LoadoutCopy copy)
    {
        var button = new Button
        {
            Content = "⧉",
            FontSize = TypeScale.Secondary,
            Padding = new Thickness(6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        ToolTip.SetTip(button, copy.Tip);

        button.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(button)?.Clipboard is not { } clipboard)
            {
                return;
            }

            try
            {
                await clipboard.SetTextAsync(copy.Value);
                button.Content = "✓";
            }
            catch (Exception)
            {
                button.Content = "✗";
            }
        };

        // Shut where there is nowhere to put it, decided when the line is drawn. Attached rather
        // than asked for here because a control has no visual root until it is in a tree.
        button.AttachedToVisualTree += (_, _) =>
            button.IsEnabled = TopLevel.GetTopLevel(button)?.Clipboard is not null;

        return button;
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
    /// <summary>
    /// The mark that says this module has been rolled.
    /// <para>
    /// <b>In the theme's accent</b> (asked for 2026-08-20), which is orange under Elite and
    /// whatever the selected theme names under any other — bound to the key rather than to a
    /// colour, so it follows a theme change without this knowing what the colour is.
    /// </para>
    /// <para>
    /// It shares that accent with the plan dot, and the earlier note here worried that two marks
    /// distinguished only by hue are one mark to a Commander who cannot separate the hues. They
    /// are not: <c>⚙</c> and <c>●</c> are different shapes in different places — the gear travels
    /// with the module's name and the dot sits in the gutter — so the hue is what they have in
    /// common rather than what tells them apart.
    /// </para>
    /// </summary>
    private static Run Gear()
    {
        var gear = new Run(" ⚙");

        Themed(gear, Run.ForegroundProperty, ThemeManager.AccentKey);

        return gear;
    }

    /// <summary>
    /// The coin: this module is behind a Powerplay pledge (Phase 38).
    /// <para>
    /// <b>The danger hue, because it is a gate.</b> The gear says what was done to a module and
    /// the dot says a plan exists; this says the Commander may not be able to buy the thing at
    /// all, which is the only one of the three that can stop a build.
    /// </para>
    /// <para>
    /// The character is <see cref="ShipsMode.Coin"/> — U+00A4 — chosen because it exists in every
    /// Latin font rather than because it is the prettiest coin available. See there.
    /// </para>
    /// </summary>
    private static Run Coin()
    {
        var coin = new Run($" {ShipsMode.Coin}");

        Themed(coin, Run.ForegroundProperty, ThemeManager.DangerKey);

        return coin;
    }

    /// <summary>
    /// The waiting question, as a banner with two answers (Phase 38).
    /// <para>
    /// <b>Buttons rather than a chooser.</b> A chooser is modal and would take the tab over the
    /// moment it was opened; this is a question the Commander may want to look at the build before
    /// answering, and a banner lets them.
    /// </para>
    /// <para>
    /// <paramref name="answered"/> is handed what happened so the page can say it — accepting and
    /// declining both produce a sentence, and a banner that vanished silently would leave a
    /// Commander unsure whether they had pressed anything.
    /// </para>
    /// </summary>
    internal static Control Notice(LoadoutNotice notice, Action<string> answered)
    {
        var text = new TextBlock
        {
            Text = notice.Text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        buttons.Children.Add(Press("Yes, revise my checklist", () => answered(notice.Yes())));
        buttons.Children.Add(Press("No, leave both alone", () => answered(notice.No())));

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel { Children = { text, buttons } },
        };

        Themed(border, Border.BorderBrushProperty, ThemeManager.AccentKey);
        Themed(border, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return border;
    }

    /// <summary>
    /// One gauge at the head of a slot list — a labelled horizontal bar (Phase 38).
    /// <para>
    /// <b>A bar and not a dial</b> (the Commander's call, 2026-08-20). Three reasons and all of
    /// them are about the headset: it reads at overlay resolution where small text on an arc does
    /// not, it survives a narrow panel, and it is a rectangle inside a rectangle rather than
    /// geometry that has to be drawn.
    /// </para>
    /// <para>
    /// <b>The marks are drawn on the bar and read under it.</b> A mark is a hairline at a
    /// position; what it means is in the reading below, because a label floating over a 6-pixel
    /// bar is unreadable on a monitor and gone entirely through a lens.
    /// </para>
    /// <para>
    /// <b>A modelled gauge is marked and is a different colour.</b> That is the condition on which
    /// these gauges were allowed to show planned figures at all — see <see cref="LoadoutGauge"/> —
    /// so the tilde and the hue are load-bearing rather than decoration.
    /// </para>
    /// </summary>
    internal static Control Gauge(LoadoutGauge gauge)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 6) };

        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        var name = new TextBlock
        {
            Text = gauge.Name,
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(name, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        heading.Children.Add(name);

        // The reading, in the gauge's own tone: red where the build does not fit, and the muted
        // Info hue where every figure in it was worked out rather than read off the game.
        var reading = new TextBlock
        {
            Text = gauge.Modelled ? $"~ {gauge.Reading}" : gauge.Reading,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(
            reading,
            TextBlock.ForegroundProperty,
            gauge.Tone == LoadoutTone.Danger ? ThemeManager.DangerKey
            : gauge.Modelled ? ThemeManager.InfoKey
            : ThemeManager.TextKey);

        heading.Children.Add(reading);
        stack.Children.Add(heading);

        stack.Children.Add(Bar(gauge));

        // What the marks are, and anything wrong with the figures. One line, muted, under the bar.
        var said = string.Join(
            " · ",
            gauge.Marks.Select(mark => mark.Label).Append(gauge.Note).Where(text => text is { Length: > 0 }));

        if (said.Length > 0)
        {
            stack.Children.Add(Muted(said));
        }

        return stack;
    }

    /// <summary>
    /// The bar itself: a track, a fill, and a hairline per mark.
    /// <para>
    /// <b>A Grid rather than a Canvas</b>, so the fill is a fraction of whatever width the panel
    /// happens to be — the same bar has to work in a window the Commander can drag and in an
    /// overlay quad that cannot be dragged at all. Columns in star widths do that with no layout
    /// pass of anybody's own.
    /// </para>
    /// <para>
    /// <b>Clamped, and deliberately not to 1.</b> A build 30% over its plant fills the bar and
    /// stops; the number beside it is what says by how much, and a bar that grew past its own
    /// track would just be a drawing bug.
    /// </para>
    /// </summary>
    private static Control Bar(LoadoutGauge gauge)
    {
        var fill = Math.Clamp(double.IsFinite(gauge.Fill) ? gauge.Fill : 0, 0, 1);

        var track = new Grid
        {
            Height = 8,
            ColumnDefinitions = new ColumnDefinitions
            {
                new(new GridLength(fill, GridUnitType.Star)),
                new(new GridLength(1 - fill, GridUnitType.Star)),
            },
        };

        var background = new Border { CornerRadius = new CornerRadius(2) };

        Themed(background, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Grid.SetColumnSpan(background, 2);
        track.Children.Add(background);

        var filled = new Border { CornerRadius = new CornerRadius(2) };

        Themed(
            filled,
            Border.BackgroundProperty,
            gauge.Tone == LoadoutTone.Danger ? ThemeManager.DangerKey
            : gauge.Modelled ? ThemeManager.InfoKey
            : ThemeManager.AccentKey);

        Grid.SetColumn(filled, 0);
        track.Children.Add(filled);

        // Each mark, as its own two-column grid over the same track. Layered rather than inserted
        // into the fill's columns, because a mark can sit before or after the fill and the two
        // must not have to agree on an order.
        foreach (var mark in gauge.Marks)
        {
            var at = Math.Clamp(double.IsFinite(mark.At) ? mark.At : 0, 0, 1);

            var over = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new(new GridLength(at, GridUnitType.Star)),
                    new(new GridLength(1 - at, GridUnitType.Star)),
                },
            };

            var hairline = new Border
            {
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            Themed(hairline, Border.BackgroundProperty, ThemeManager.TextKey);
            Grid.SetColumn(hairline, 0);
            over.Children.Add(hairline);

            Grid.SetColumnSpan(over, 2);
            track.Children.Add(over);
        }

        return track;
    }

    /// <summary>
    /// The columns every slot row and the header above them share
    /// (docs/plans/change-requests.md 38).
    /// <para>
    /// <b>Stars rather than shared sizes.</b> Each row is its own control in a stack, so nothing
    /// measures them together — proportions do line them up, and they do it at 512 pixels and at
    /// 2048 without a measure pass that has to visit every row before any of them can draw.
    /// </para>
    /// </summary>
    private static ColumnDefinitions Columns() => new("22,0.75*,1.45*,1.15*");

    /// <summary>
    /// The one header above a ship's slot list, because two columns that are not labelled are two
    /// columns a Commander has to work out (docs/plans/change-requests.md 38).
    /// <para>
    /// <b>Once, above the first group</b>, rather than once per group: the headings below it say
    /// which block of the outfitting screen each run of rows is, and repeating SLOT / CURRENT /
    /// PLAN four times would cost four rows to say nothing new.
    /// </para>
    /// </summary>
    internal static Control SlotHeader()
    {
        var line = new Grid
        {
            ColumnDefinitions = Columns(),
            Margin = new Thickness(12, 6, 12, 2),
        };

        var at = 1;

        foreach (var word in new[] { "SLOT", "CURRENT", "PLAN" })
        {
            var said = new TextBlock
            {
                Text = word,
                FontSize = TypeScale.Secondary,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Themed(said, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            Grid.SetColumn(said, at++);
            line.Children.Add(said);
        }

        return line;
    }

    /// <summary>
    /// A slot row: what the slot is, what is in it, and what the plan asks for
    /// (docs/plans/change-requests.md 38).
    /// <para>
    /// <b>A table, overturning the index this was</b> — and overturning it on the evidence the
    /// index produced. One line carrying both facts had to pick which to say, and every pick
    /// described something that was not there: a planned Shield Booster in an empty mount drew
    /// exactly like the five fitted ones beside it, and a distributor rolled the wrong way read as
    /// finished. Two columns is the answer that was unavailable while the row was one line.
    /// </para>
    /// <para>
    /// <b>Agreement collapses.</b> Where the hull already matches the plan the second column is a
    /// tick and stops, and where only the module agrees the plan column names the roll alone —
    /// repeating identical words in two columns asks the eye to compare two strings that were
    /// never going to differ.
    /// </para>
    /// <para>
    /// <b>Ellipsis rather than wrapping.</b> A row is one line: a wrapping row changes height,
    /// which moves every row under it and makes a list impossible to scan or to aim a ray at. Each
    /// cell is one <see cref="TextBlock"/> for the same reason it is one line — a stack of blocks
    /// inside a star column is measured with infinite width and would bleed into the next column
    /// rather than trimming at the edge of its own.
    /// </para>
    /// </summary>
    internal static Control SlotRow(LoadoutRow row, Action pressed)
    {
        var parts = row.Parts!;

        var line = new Grid
        {
            ColumnDefinitions = Columns(),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // The dot: there is work left in this slot. Its own mark rather than a column, and it
        // keeps the gutter position it has always had — kept in the layout when there is none, so
        // the slot names line up down the list rather than stepping in and out by the width of it.
        var mark = new TextBlock
        {
            Text = "●",
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = row.Marked ? 1 : 0,
        };

        Themed(mark, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
        Grid.SetColumn(mark, 0);
        line.Children.Add(mark);

        // The slot, muted: it is what the row is *about* rather than what the row says, and the
        // heading above already names the block it belongs to.
        var slot = new TextBlock
        {
            Text = parts.Slot,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 10, 0),
        };

        Themed(slot, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        Grid.SetColumn(slot, 1);
        line.Children.Add(slot);

        var current = Cell(parts.Current, parts.Vacant, row.Engineered, bold: true);

        Grid.SetColumn(current, 2);
        line.Children.Add(current);

        var plan = Planned(parts);

        Grid.SetColumn(plan, 3);
        line.Children.Add(plan);

        var button = new Button
        {
            Content = line,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 34,
            Padding = new Thickness(12, 6),
        };

        // The slot's name in full, for anything that cannot see the row — and now for the eye as
        // well, since the column is the short form. A row that says "Power Dist." must still be
        // able to say Power Distributor to a screen reader, to a test, and to a Commander who
        // hovers it.
        AutomationProperties.SetName(button, row.Word);
        ToolTip.SetTip(slot, row.Word);

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>
    /// The plan column: a tick where the hull already matches, the roll alone where only the
    /// module does, and nothing at all where there is no plan.
    /// <para>
    /// <b>An empty cell is the fourth state and needs no word for it.</b> The sketch this came
    /// from wrote "(no plan)" there; on a real hull that is filler on most of thirty-odd rows, and
    /// the column's own heading already says what a blank one under it means.
    /// </para>
    /// </summary>
    private static Control Planned(LoadoutParts parts)
    {
        if (parts.Plan is not { } plan)
        {
            return new TextBlock();
        }

        if (parts.Met)
        {
            var tick = new TextBlock
            {
                Text = "✓",
                FontSize = TypeScale.Body,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Themed(tick, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

            return tick;
        }

        // Where the module is already the right one, the plan is about the roll and says only
        // that: "7A Power Dist." twice over is the noise this change exists to remove.
        var agreed = plan.Module is { Length: > 0 } wanted
                     && string.Equals(wanted, parts.Current.Module, StringComparison.Ordinal);

        return Cell(
            agreed ? plan with { Module = null, Long = null } : plan,
            string.Empty,
            gear: false,
            bold: false);
    }

    /// <summary>
    /// One side of a slot row, drawn: the module, then what was rolled on it, then as much of what
    /// the roll did as the column has room for.
    /// </summary>
    /// <param name="vacant">
    /// What a silent side says. "empty" for the hull, and nothing for a plan — a slot with no plan
    /// is not an empty plan, it is a slot nobody has asked anything of.
    /// </param>
    /// <param name="gear">Whether a roll has been done here, which is only ever the hull's fact.</param>
    /// <param name="bold">Whether the module leads in bold, which the hull's side does and a plan does not.</param>
    private static Control Cell(LoadoutSide side, string vacant, bool gear, bool bold)
    {
        var cell = new TextBlock
        {
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 10, 0),
        };

        if (side.Silent)
        {
            cell.Text = vacant;
            Themed(cell, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

            return cell;
        }

        var head = Rolled(side);
        var module = side.Module ?? string.Empty;

        // **`Text` where one run would do.** A `TextBlock` carrying `Inlines` reports no `Text` at
        // all, so a cell drawn as inlines is invisible to anything reading the panel by text —
        // which is how the tests read it, and how the VR surface's capture check does. Inlines
        // only where there is genuinely more than one thing to draw.
        if (module.Length == 0)
        {
            cell.Text = head;
            cell.FontSize = TypeScale.Secondary;
            Themed(cell, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            Trailing(cell, null, string.Empty, head, side.Effects, bold);
            Hover(cell, side);

            return cell;
        }

        var inlines = new InlineCollection { new Run(module) { FontWeight = bold ? FontWeight.Bold : FontWeight.Normal } };

        // The gear travels with the last word of the name (remediation.md 17, item 10).
        if (gear)
        {
            inlines.Add(Gear());
        }

        // And the coin, for a module a Powerplay pledge is needed to buy (Phase 38).
        // Beside the gear rather than instead of it: a Prismatic Shield Generator can be both
        // gated and engineered, and they are different facts.
        if (side.Gated)
        {
            inlines.Add(Coin());
        }

        if (head.Length > 0 || side.Effects.Count > 0)
        {
            var rest = new Run(head.Length > 0 ? $" · {head}" : string.Empty)
            {
                FontSize = TypeScale.Secondary,
            };

            Themed(rest, Run.ForegroundProperty, ThemeManager.TextMutedKey);
            inlines.Add(rest);
            Trailing(cell, rest, module, head, side.Effects, bold);
        }

        if (inlines.Count == 1)
        {
            cell.Text = module;
            cell.FontWeight = bold ? FontWeight.Bold : FontWeight.Normal;
        }
        else
        {
            cell.Inlines = inlines;
        }

        Hover(cell, side);

        return cell;
    }

    /// <summary>
    /// Keeps the trailing run — the roll and what it did — to whatever the column has left, and
    /// <b>re-figures it whenever the cell changes width</b>, which is what "dynamic" has to mean on
    /// a panel that runs from 512 to 2048 logical pixels and is resized by hand.
    /// </summary>
    private static void Trailing(
        TextBlock cell,
        Run? run,
        string module,
        string head,
        IReadOnlyList<string> effects,
        bool bold)
    {
        if (effects.Count == 0)
        {
            return;
        }

        void Fit()
        {
            var said = Fitted(cell, module, head, effects, bold);

            if (run is null)
            {
                cell.Text = said;
            }
            else
            {
                run.Text = said.Length > 0 ? $" · {said}" : string.Empty;
            }
        }

        cell.SizeChanged += (_, _) => Fit();
        Fit();
    }

    /// <summary>
    /// The long name on the tooltip, so a short one is never the only one (the Commander's ruling,
    /// 2026-08-25). Nothing is hung where the two are the same string.
    /// </summary>
    private static void Hover(Control cell, LoadoutSide side)
    {
        if (side.Long is { Length: > 0 } spelled)
        {
            ToolTip.SetTip(cell, spelled);
        }
    }

    /// <summary>
    /// The roll itself — blueprint, grade, experimental — which never gets trimmed away.
    /// <para>
    /// <b>The module is not in it.</b> <see cref="D47.Core.Knowledge.ShortNames.Bare"/> has
    /// already struck the module off the blueprint's tail, so a row saying HRP says <i>Heavy
    /// Duty</i> beside it rather than <i>Heavy Duty Hull Reinforcement</i> — which is shorter, and
    /// comparable straight down the column, which is worth more than the width.
    /// </para>
    /// </summary>
    private static string Rolled(LoadoutSide side)
    {
        var said = new List<string>();

        if (side.Blueprint is { Length: > 0 } blueprint)
        {
            said.Add(side.Grade is { } grade and > 0
                ? $"{blueprint} G{grade.ToString(CultureInfo.InvariantCulture)}"
                : blueprint);
        }

        if (side.Experimental is { Length: > 0 } experimental)
        {
            said.Add(experimental);
        }

        return string.Join(" · ", said);
    }

    /// <summary>
    /// As many effects as the cell has room for, cut at a whole effect.
    /// <para>
    /// Measured rather than guessed: <see cref="FormattedText"/> is asked how wide each candidate
    /// would be, and the longest one that fits wins. Guessing by character count is wrong the
    /// moment the Commander changes the zoom, which this panel lets them do. Whole effects are
    /// dropped rather than the line being cut mid-word: "+3% pow…" says less than nothing.
    /// </para>
    /// </summary>
    private static string Fitted(
        TextBlock cell, string module, string head, IReadOnlyList<string> effects, bool bold)
    {
        // What is left after the module has taken its share. Below a floor there is no point
        // measuring: the roll itself is what matters and the cell's own ellipsis is already on it.
        var room = cell.Bounds.Width
                   - Wide(module, cell.FontFamily, TypeScale.Body, bold)
                   - 12;

        if (room < 60)
        {
            return head;
        }

        for (var count = effects.Count; count > 0; count--)
        {
            var candidate = head.Length > 0
                ? $"{head} · {string.Join(", ", effects.Take(count))}"
                : string.Join(", ", effects.Take(count));

            if (Wide(candidate, cell.FontFamily, TypeScale.Secondary, false) <= room)
            {
                return candidate;
            }
        }

        return head;
    }

    private static double Wide(string said, FontFamily family, double size, bool bold) =>
        said.Length == 0
            ? 0
            : new FormattedText(
                said,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(family, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
                size,
                Brushes.Black).Width;

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
    private IDisposable? _sized;
    private bool _settled;

    protected LoadoutPage(ILoadoutMode mode) => _mode = mode;

    protected ILoadoutMode Mode => _mode;

    /// <summary>
    /// Whether the surface drawing this page is the mini one — 512 by 280, six rows
    /// (docs/plans/change-requests.md 38).
    /// <para>
    /// <b>Read off the surface rather than passed in</b>, because mini is a property of the
    /// instantiation and a page outlives a switch into it and back: the Commander flips the window
    /// to mini with a ship already open, and the page it is holding has to answer differently
    /// without being rebuilt. This is the same lookup the settings view already makes for the same
    /// reason.
    /// </para>
    /// <para>
    /// False on a surface with no <see cref="PanelView"/> above it, which is every test that draws
    /// a page on its own — the full reading, which is the one those tests are about.
    /// </para>
    /// </summary>
    protected bool Mini { get; private set; }

    protected abstract void Refresh();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _mode.Changed += OnChanged;

        _sized = this.GetSelfAndVisualAncestors()
            .OfType<PanelView>()
            .FirstOrDefault()
            ?.GetObservable(PanelView.ModeProperty)
            .Subscribe(new AnonymousObserver<PanelMode>(OnSurface));

        _settled = true;

        // Being put back by the strip means having missed whatever happened while it was out, so
        // a page catches up rather than trusting what it last drew.
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _mode.Changed -= OnChanged;

        _sized?.Dispose();
        _sized = null;
        _settled = false;
    }

    /// <summary>
    /// The surface went mini, or came back. Nothing is redrawn on the way in — the subscription
    /// delivers the current mode immediately and the attach below is about to draw anyway.
    /// </summary>
    private void OnSurface(PanelMode mode)
    {
        var mini = mode == PanelMode.Mini;

        if (mini == Mini)
        {
            return;
        }

        Mini = mini;

        if (_settled)
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);
}

/// <summary>
/// The index (Phase 26, "The fleet, and the fleet you intend"; Phase 27, "The same page,
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

        // A question left over from one d47 asked out loud (Phase 38). At the head of the
        // index because that is where a Commander lands on the tab, and above the empty case
        // because a fleet d47 has not read yet can still have a question waiting about a build.
        if (Mode.Notice() is { } notice)
        {
            _list.Children.Add(LoadoutPages.Notice(notice, said =>
            {
                Refresh();
                _list.Children.Insert(0, LoadoutPages.Muted(said));
            }));
        }

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
/// One item's slots (Phase 26, "What is fitted and what you want").
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

        // The page, with a layer above it for the thing being carried. A Canvas so the ghost can
        // be put at a point rather than laid out, and not hit-testable so it never takes the drop
        // it is describing.
        _overlay = new Canvas { IsHitTestVisible = false };

        Content = new Avalonia.Controls.Panel { Children = { root, _overlay } };

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

        // The same question the index carries, here too (Phase 38): a Commander who has
        // drilled into the ship it is about is the likeliest one to answer it, and they would
        // otherwise have to go back a level to find out it had been asked.
        if (Mode.Notice() is { } notice)
        {
            _list.Children.Add(LoadoutPages.Notice(notice, said =>
            {
                Refresh();
                _summary.Text = said;
            }));
        }

        var rows = Mode.Slots(_item);

        if (rows.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(Mode.EmptySlots));
            return;
        }

        // **Mini gets neither of the two blocks below.** They are read once when the page opens
        // and then scrolled past, which is a fair trade on a window and not on six rows: a mini
        // surface that spends all six saying what a Python is has not answered the question it was
        // switched to (docs/plans/change-requests.md 38). The full reading is one flip away.
        var roomy = !Mini || rows.All(row => row.Parts is null);

        // What the ship is, before what is in it (remediation.md 13, item 2). Inside the
        // scroller rather than docked above it, or a hull's figures would cost the slot list the
        // same rows on every window.
        foreach (var line in roomy ? Mode.Details(_item) : [])
        {
            // Stepped rather than Line, for the copy glyph a whereabouts line carries: the system
            // a ship is parked in goes on the clipboard from here, to be pasted into the Galaxy
            // Map. A line with neither a step nor a copy is drawn exactly as before.
            _list.Children.Add(LoadoutPages.Stepped(line));
        }

        // Power and jump range, at the head of the slot list and under the hull's own figures
        // (Phase 38). Here rather than docked above the scroller for the same reason the
        // details are: two gauges would cost the slot list two rows on every window, and they are
        // read once when the page opens rather than watched while scrolling.
        foreach (var gauge in roomy ? Mode.Gauges(_item) : [])
        {
            _list.Children.Add(LoadoutPages.Gauge(gauge));
        }

        // **Mini shows only the rows that disagree** (the Commander's ruling, 2026-08-25).
        // Two columns do not fit 512 pixels thirty times over, and the alternative readings were
        // worse rather than smaller: current-only drops the one fact a Commander standing at a
        // workshop is there for, and the whole list squeezed is a list nobody can read. What is
        // left is the question they are actually asking — *what is still to do on this ship* —
        // which is also the shortest list there is.
        var slots = Mini && rows.Any(row => row.Parts is not null)
            ? [.. rows.Where(row => row.Parts is null || row.Marked)]
            : rows;

        if (slots.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted("Nothing outstanding on this ship."));
            return;
        }

        // The header, once, above the first slot row — and only where there are slot rows to head
        // (docs/plans/change-requests.md 38). A suit's list is still an index and has no columns
        // to label.
        if (slots.Any(row => row.Parts is not null))
        {
            _list.Children.Add(LoadoutPages.SlotHeader());
        }

        // A heading wherever the group changes, so a ship's slots read as the four blocks of the
        // outfitting screen rather than as thirty-odd names in journal order
        // (remediation.md 12, item 1). A mode that groups nothing draws nothing extra.
        var group = (string?)null;

        foreach (var row in slots)
        {
            if (row.Group is { Length: > 0 } heading && heading != group)
            {
                group = heading;
                _list.Children.Add(LoadoutPages.Heading(heading));
            }

            // A slot row draws as the loadout; anything else is still a line with a note.
            var control = row.Parts is not null
                ? LoadoutPages.SlotRow(row, () => _nav.Drill(LoadoutPages.SlotCrumb(Mode, row)))
                : LoadoutPages.Row(
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

        // What this row is, for the geometry that finds the row under the pointer. Read back off
        // the control rather than kept in a second list, so a row and its slot cannot come apart
        // across a Refresh — the children are rebuilt on every one of them.
        control.Tag = slot;

        control.AddHandler(
            InputElement.PointerPressedEvent,
            (_, args) =>
            {
                var point = args.GetCurrentPoint(control);

                if (!point.Properties.IsLeftButtonPressed
                    || !args.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    return;
                }

                _dragging = slot;
                _over = slot;
                args.Handled = true;

                Carry(row, args.GetPosition(this));
            },
            RoutingStrategies.Tunnel);

        // **The pointer is captured by the row the press landed on**, so every move and the
        // release itself arrive here rather than at the row under the cursor. That is the whole of
        // the reported fault: the release saw `from == slot`, concluded the plan had been dropped
        // where it started, and did nothing — on every slot kind, since the feature shipped.
        //
        // So the move is followed and the row under the pointer is worked out from geometry. That
        // also buys what was missing anyway: with no ghost and no cursor there was nothing on
        // screen to say a drag was happening, which is how a drag that never worked went unseen.
        control.AddHandler(
            InputElement.PointerMovedEvent,
            (_, args) =>
            {
                if (_dragging is not { } from)
                {
                    return;
                }

                var at = args.GetPosition(this);

                Carry(row, at);
                _over = SlotUnder(at) ?? from;

                Highlight(from);
            },
            RoutingStrategies.Tunnel);

        control.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, args) =>
            {
                if (_dragging is not { } from)
                {
                    return;
                }

                // try/finally, so the cursor and the ghost come back whatever happens in between.
                // A pointer left showing "copy" over a page that is no longer carrying anything is
                // worse than the missing feedback this was added to fix.
                try
                {
                    args.Handled = true;

                    var target = SlotUnder(args.GetPosition(this)) ?? _over ?? from;

                    if (target == from)
                    {
                        _summary.Text = $"{row.Word} is where that plan already is, so nothing was copied.";
                        return;
                    }

                    // **Explained rather than refused in silence** (remediation.md 17, item 8).
                    // This used to return here when `CanCopy` said no, on the grounds that the
                    // dimmed row had already said it — and the report was *"module drag and copy
                    // is not working"*, on a drag being turned down for a reason nothing ever said
                    // out loud. `SlotCopy`'s own documentation names the hazard exactly: *a drag
                    // that silently does nothing is indistinguishable from a broken feature*.
                    //
                    // `Copy` answers both cases in one call — it refuses by returning the sentence
                    // saying why — so there is no second code path to keep in step with the rules.
                    var said = Mode.Copy(_item, from, target);

                    // **After the redraw, not before it.** `Refresh` opens by writing the build's
                    // own summary into this very block, so the line used to be overwritten in the
                    // same gesture that produced it.
                    Refresh();

                    _summary.Text = said;
                }
                finally
                {
                    Release();
                }
            },
            RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// The ghost of the plan being carried, put beside the pointer — and the copy cursor, so the
    /// gesture says what it is while it is happening (asked for 2026-08-20).
    /// </summary>
    private void Carry(LoadoutRow row, Point at)
    {
        if (_ghost is null)
        {
            _ghost = new Border
            {
                Padding = new Thickness(8, 4),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Opacity = 0.85,
                IsHitTestVisible = false,
                Child = new TextBlock { FontSize = TypeScale.Secondary },
            };

            LoadoutPages.Themed(_ghost, Border.BackgroundProperty, ThemeManager.SurfaceKey);
            LoadoutPages.Themed(_ghost, Border.BorderBrushProperty, ThemeManager.AccentKey);

            _overlay.Children.Add(_ghost);
        }

        if (_ghost.Child is TextBlock said)
        {
            // What is being carried rather than where it came from: the aside is the plan, and the
            // plan is the thing being copied.
            said.Text = row.Aside is { Length: > 0 } plan ? plan : row.Word;
        }

        _ghost.IsVisible = true;

        // Beside the pointer rather than under it, so the ghost never hides the row it is about to
        // land on.
        Canvas.SetLeft(_ghost, at.X + 14);
        Canvas.SetTop(_ghost, at.Y + 12);

        Cursor = _copying ??= new Cursor(StandardCursorType.DragCopy);
    }

    /// <summary>Everything <see cref="Carry"/> turned on, turned off. Safe to call twice.</summary>
    private void Release()
    {
        _dragging = null;
        _over = null;

        if (_ghost is not null)
        {
            _ghost.IsVisible = false;
        }

        Cursor = Cursor.Default;

        foreach (var child in _list.Children)
        {
            child.Opacity = 1;
        }
    }

    /// <summary>
    /// Which row the pointer is over, by geometry rather than by hit-testing.
    /// <para>
    /// Hit-testing answers for the captured row whatever the pointer is over, which is the fault
    /// this exists to route around. Geometry also means a test can drive it: a position is a
    /// position, with no input pipeline to stand up.
    /// </para>
    /// </summary>
    private string? SlotUnder(Point at)
    {
        foreach (var child in _list.Children)
        {
            if (child.Tag is not string slot || child.TranslatePoint(default, this) is not { } origin)
            {
                continue;
            }

            if (at.X >= origin.X && at.X <= origin.X + child.Bounds.Width
                && at.Y >= origin.Y && at.Y <= origin.Y + child.Bounds.Height)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// What the rows look like while a plan is over them: dimmed where the drop would be refused,
    /// so the Commander learns the rule from the interface rather than from a message afterwards.
    /// </summary>
    private void Highlight(string from)
    {
        foreach (var child in _list.Children)
        {
            child.Opacity = child.Tag is string slot && slot == _over && slot != from
                ? Mode.CanCopy(_item, from, slot) ? 1 : 0.4
                : 1;
        }
    }

    /// <summary>The slot a plan is being dragged from, or null when nothing is in flight.</summary>
    private string? _dragging;

    /// <summary>The slot the pointer is over, which is where a drop lands.</summary>
    private string? _over;

    private readonly Canvas _overlay;
    private Border? _ghost;
    private Cursor? _copying;
}

/// <summary>
/// One slot (Phase 26, "What is fitted and what you want").
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
/// The gap between every plan and what the Commander is carrying (Phase 27, "Gap
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
