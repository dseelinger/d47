using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Loadout;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's pages: an index, then an item, then a slot — drawn once and shown for every mode
/// (Phase 26, "Ships"; Phase 27, "The same page, on foot").
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
    public const string CarrierRoot = "loadout.carrier";

    public const string GapRoot = "loadout.gap";

    /// <summary>Draws whichever level a crumb names.</summary>
    /// <param name="gap">
    /// Where the gap gets its arithmetic, or null for a surface with no on-foot half — the tab then has
    /// the one root Phase 26 gave it.
    /// </param>
    public static Control Build(
        NavCrumb crumb,
        IReadOnlyList<ILoadoutMode> modes,
        GapSource? gap,
        CarrierSource? carrier,
        PanelNavigator nav,
        PanelPrompts prompts,
        Func<string, Task<bool>>? copy = null)
    {
        if (crumb.Key == CarrierRoot && carrier is not null)
        {
            return new CarrierPage(carrier, copy: copy);
        }

        foreach (var mode in modes)
        {
            if (crumb.Key.StartsWith(mode.SlotPrefix, StringComparison.Ordinal))
            {
                var (item, slot) = SplitSlot(crumb.Key[mode.SlotPrefix.Length..]);
                return new SlotPage(mode, prompts, item, slot, copy);
            }

            if (crumb.Key.StartsWith(mode.ItemPrefix, StringComparison.Ordinal))
            {
                return new ItemPage(mode, nav, crumb.Key[mode.ItemPrefix.Length..], prompts, copy);
            }
        }

        if (crumb.Key == GapRoot && gap is not null)
        {
            return new GapPage(gap);
        }

        var root = modes.FirstOrDefault(mode => mode.RootKey == crumb.Key) ?? modes[0];

        return new IndexPage(root, nav, prompts);
    }

    /// <summary>The crumb for a ship, and for a slot of it.</summary>
    public static NavCrumb Ship(D47.Core.Ships.FleetEntry entry) =>
        new(ShipPrefix + (entry.Build?.Id ?? entry.Hull), entry.Name ?? entry.HullName);

    public static NavCrumb Slot(string buildId, string slot) =>
        new($"{SlotPrefix}{buildId}|{slot}", slot);

    /// <summary>The crumb for one row of an index, and for one slot below it.</summary>
    public static NavCrumb Crumb(ILoadoutMode mode, LoadoutRow row) =>
        new(mode.ItemPrefix + row.Key, row.Word) { Level = mode.ItemPrefix };

    /// <summary>One slot of one item.</summary>
    public static NavCrumb SlotCrumb(ILoadoutMode mode, LoadoutRow row) =>
        new(mode.SlotPrefix + row.Key, row.Word)
        {
            Level = mode.SlotPrefix,

            // A slot is where a blueprint is chosen, so its help is the blueprint page rather than the fleet
            // page the root declares.
            Help = mode.SlotHelp,
        };

    internal static (string Item, string Slot) SplitSlot(string key)
    {
        var at = key.IndexOf('|', StringComparison.Ordinal);

        return at < 0 ? (key, string.Empty) : (key[..at], key[(at + 1)..]);
    }

    // ------------------------------------------------------------------ shared drawing

    /// <summary>One pressable line of an index.</summary>
    /// <param name="showing">
    /// Whether this is the row the other pane is currently drawing, in which case it is outlined
    /// (#110).
    /// </param>
    internal static Control Row(
        string text,
        string? aside,
        bool marked,
        Action pressed,
        bool engineered = false,
        bool showing = false)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // A grid rather than a dock, because a right-docked child takes as much width as it asks for and the
        // fill child gets what is left (remediation.md 14, item 1).
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

                // Wrapped, so a note too long for what is left of the row becomes two lines rather than
                // pushing the name out of the way.
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Right,
            };

            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            Grid.SetColumn(note, 2);
            body.Children.Add(note);

            // Capped against the row's own width, because an Auto column measures a wrapping block as though
            // it had forever: without this the note asks for its whole width on one line, is given less, and
            // is cut off mid-word rather than wrapping.
            body.SizeChanged += (_, size) =>
                note.MaxWidth = Math.Max(80, size.NewSize.Width * 0.6);
        }

        // The marks, and they are marks rather than columns: "this slot has a plan" and "this module is
        // engineered" are booleans, and a column for either would be a column that is empty on most rows of
        // most ships. **Two independent facts, and which is which has to be readable at a glance**
        // (remediation.md 15, item 10).
        if (engineered)
        {
            label.Inlines =
            [
                new Run(text),
                Gear(),
            ];
        }

        // The dot stays in the left gutter, which is the Commander's call rather than a consequence: the two
        // marks answer different questions, and a plan existing is a fact about the *row* where a roll having
        // been done is a fact about the module named in it.
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

            // Tall enough for a ray at a metre.
            MinHeight = 34,
            Padding = new Thickness(12, 6),
        };

        if (showing)
        {
            // The class names the state and the two lines below draw it.
            button.Classes.Add("showing");
            button.BorderThickness = new Thickness(2);
            Themed(button, Button.BorderBrushProperty, ThemeManager.AccentKey);
        }

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>
    /// One thing in the index, as a card in a grid rather than a bar in a list (asked for 2026-09-03).
    /// </summary>
    internal static Control Card(
        string text,
        string? aside,
        bool marked,
        Action pressed,
        LoadoutStanding standing,
        string? hull = null,
        bool drawings = false,
        bool showing = false)
    {
        var stroke = standing == LoadoutStanding.Wanted
            ? ThemeManager.TextMutedKey
            : ThemeManager.TextKey;

        var body = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            ],
        };

        Image? spinning = null;
        Bitmap? resting = null;

        if (drawings && ShipArt.For(hull) is { } picture)
        {
            resting = picture;

            // Uniform, so a hull keeps its proportions whatever share of the width the column count left the
            // card; and centred in the row rather than stretched to it, because a Sidewinder and a Type-10
            // are framed to fill the same box already and letting the control stretch would undo that.
            var drawing = new Image
            {
                Source = picture,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2),
            };

            Grid.SetRow(drawing, 0);
            body.Children.Add(drawing);

            spinning = drawing;
        }

        // **"Flying now" as a badge, not as a highlighted card** (#289, reported 2026-09-04).
        if (standing == LoadoutStanding.Active)
        {
            var badge = Pill("FLYING NOW");

            Grid.SetRow(badge, 0);
            body.Children.Add(badge);
        }

        var label = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        if (marked)
        {
            // The plan dot keeps the meaning it has on a row — a plan exists here — and keeps travelling with
            // the name rather than becoming a corner badge that would then be one of two corner badges saying
            // different things.
            label.Inlines =
            [
                new Run(text),
                Dot(),
            ];
        }

        Grid.SetRow(label, 1);
        body.Children.Add(label);

        if (aside is { Length: > 0 })
        {
            var note = new TextBlock
            {
                Text = aside,
                FontSize = TypeScale.Secondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0),
            };

            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            Grid.SetRow(note, 2);
            body.Children.Add(note);
        }

        var button = new Button
        {
            Content = body,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(10, 6),
        };

        if (standing == LoadoutStanding.Wanted)
        {
            // Thickness as well as colour, because a border that differs only in hue is no border at all to a
            // Commander who cannot separate the hues — the same rule the plan dot and the gear already answer
            // to.
            button.BorderThickness = new Thickness(1);
            Themed(button, Button.BorderBrushProperty, stroke);

            // **Faded, because a muted one-pixel edge was not a signal.** Drawn and looked at: an unbought
            // hull was all but indistinguishable from an owned one, which is the exact complaint this work
            // started from.
            button.Opacity = 0.55;
        }

        if (showing)
        {
            // **The card the other pane is drawing**, the same outline a row has carried since #110 and the
            // same reason: an index that stays on screen beside what it opened has to say which one that is.
            button.BorderThickness = new Thickness(2);
            Themed(button, Button.BorderBrushProperty, ThemeManager.AccentKey);
        }

        if (spinning is { } turning && resting is { } still)
        {
            button.Click += (_, _) =>
            {
                // The rotation first, then the drill, and both on the one press (#289).
                HullTurntable.Play(turning, hull, still);

                pressed();
            };

            // **And again when this card is the one being rebuilt**, which the press itself causes: opening a
            // ship redraws the index so it can outline what it opened, so the image the press started the
            // video on is thrown away a moment later.
            turning.AttachedToVisualTree += (_, _) =>
            {
                if (HullTurntable.Resumes(hull))
                {
                    HullTurntable.Play(turning, hull, still);
                }
            };
        }
        else
        {
            button.Click += (_, _) => pressed();
        }

        return button;
    }

    /// <summary>A small bordered label, the shape the build badge and the issue chips already use.</summary>
    private static Control Pill(string said)
    {
        var text = new TextBlock
        {
            Text = said,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.Bold,
        };

        var pill = new Border
        {
            Padding = new Thickness(7, 1),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = text,
        };

        Themed(text, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
        Themed(pill, Border.BorderBrushProperty, ThemeManager.AccentKey);
        Themed(pill, Border.BackgroundProperty, ThemeManager.BackgroundKey);

        return pill;
    }

    /// <summary>The plan mark, as an inline so it travels with the name on a card.</summary>
    private static Run Dot()
    {
        var dot = new Run(" ●");

        Themed(dot, Run.ForegroundProperty, ThemeManager.AccentKey);

        return dot;
    }

    /// <summary>
    /// The say-line along the bottom of a page: the phrase for what the Commander is looking at.
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
    internal static Control Stepped(LoadoutLine line, Func<string, Task<bool>>? copy)
    {
        if (line.Copy is { } target && copy is not null)
        {
            // Beside the text for the same reason the stepper is: the line is one string that is both shown
            // and spoken, so it keeps its sentence and the control sits next to it.
            var copyable = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };

            copyable.Children.Add(Line(line));
            copyable.Children.Add(D47.App.Controls.CopyGlyph.For(target.Value, copy));

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

        // **The grade last, then the buttons that move it** (remediation.md 17, item 11).
        var grade = new TextBlock
        {
            Text = $"Grade {step.Value.ToString(CultureInfo.InvariantCulture)}",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(grade);

        // Highest first, which is how the offer is ordered and how a Commander reads a grade: up is better.
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

        // What was done to the module, in its own colour (remediation.md 15, item 10).
        LoadoutTone.Engineered => Toned(line.Text, ThemeManager.InfoKey),
        _ => Muted(line.Text),
    };

    internal static TextBlock Muted(string text) => Toned(text, ThemeManager.TextMutedKey);

    /// <summary>Prose in a detail pane, and it is selectable (#122).</summary>
    internal static TextBlock Toned(string text, string key)
    {
        var block = new SelectableTextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    /// <summary>A detail pane's heading, selectable for the same reason its prose is.</summary>
    internal static TextBlock Heading(string text) => new SelectableTextBlock
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
    /// The mark meaning a roll has been done, as an inline so it travels with the name it is about
    /// (remediation.md 17, item 10).
    /// </summary>
    private static Run Gear()
    {
        var gear = new Run(" ⚙");

        Themed(gear, Run.ForegroundProperty, ThemeManager.AccentKey);

        return gear;
    }

    /// <summary>The coin: this module is behind a Powerplay pledge (Phase 38).</summary>
    private static Run Coin()
    {
        var coin = new Run($" {ShipsMode.Coin}");

        Themed(coin, Run.ForegroundProperty, ThemeManager.DangerKey);

        return coin;
    }

    /// <summary>The waiting question, as a banner with two answers (Phase 38).</summary>
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

    /// <summary>One gauge at the head of a slot list — a labelled horizontal bar (Phase 38).</summary>
    internal static Control Gauge(LoadoutGauge gauge)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 6) };

        // **A DockPanel rather than a horizontal stack, so the reading wraps** (#289).
        var heading = new DockPanel();

        var name = new TextBlock
        {
            Text = gauge.Name,
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        DockPanel.SetDock(name, Dock.Left);

        Themed(name, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        heading.Children.Add(name);

        // The reading, in the gauge's own tone: red where the build does not fit, and the muted Info hue
        // where every figure in it was worked out rather than read off the game.
        var reading = new TextBlock
        {
            Text = gauge.Modelled ? $"~ {gauge.Reading}" : gauge.Reading,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
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

        // The figures under the points they belong to, where the gauge supplied any.
        if (gauge.Scale.Count > 0)
        {
            stack.Children.Add(Scale(gauge));
        }

        // What the marks are, and anything wrong with the figures.
        var said = string.Join(
            " · ",
            gauge.Marks
                .Where(mark => !gauge.Scale.Any(tick =>
                    string.Equals(tick.Label, mark.Label, StringComparison.Ordinal)))
                .Select(mark => mark.Label)
                .Append(gauge.Note)
                .Where(text => text is { Length: > 0 }));

        if (said.Length > 0)
        {
            stack.Children.Add(Muted(said));
        }

        return stack;
    }

    /// <summary>
    /// The figures written under the points on the bar they belong to (the Commander's instruction,
    /// 2026-09-01).
    /// </summary>
    private static Control Scale(LoadoutGauge gauge)
    {
        var row = new Grid();

        foreach (var tick in gauge.Scale)
        {
            var at = Math.Clamp(double.IsFinite(tick.At) ? tick.At : 0, 0, 1);

            var over = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new(new GridLength(at, GridUnitType.Star)),
                    new(new GridLength(1 - at, GridUnitType.Star)),
                },
            };

            var text = new TextBlock
            {
                Text = tick.Label,
                FontSize = TypeScale.Small,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

            Grid.SetColumn(text, 0);
            over.Children.Add(text);

            Grid.SetColumnSpan(over, 2);
            row.Children.Add(over);
        }

        return row;
    }

    /// <summary>The bar itself: a track, a fill, and a hairline per mark.</summary>
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

        // Each mark, as its own two-column grid over the same track.
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

            // **The mark says what it is** (asked 2026-09-01 — *"I can't remember what the white bar on the
            // blue line is for"*).
            var reach = new Border
            {
                Width = 12,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, -5, 0),
                Child = hairline,
            };

            ToolTip.SetTip(reach, mark.Label);
            AutomationProperties.SetName(reach, mark.Label);

            Grid.SetColumn(reach, 0);
            over.Children.Add(reach);

            Grid.SetColumnSpan(over, 2);
            track.Children.Add(over);
        }

        return track;
    }

    /// <summary>
    /// The columns every slot row and the header above them share (docs/plans/change-requests.md 38).
    /// </summary>
    private static ColumnDefinitions Columns() => new("22,0.75*,1.45*,1.15*");

    /// <summary>
    /// The one header above a ship's slot list, because two columns that are not labelled are two
    /// columns a Commander has to work out (docs/plans/change-requests.md 38).
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
    /// </summary>
    internal static Control SlotRow(LoadoutRow row, Action pressed)
    {
        var parts = row.Parts!;

        var line = new Grid
        {
            ColumnDefinitions = Columns(),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // The dot: there is work left in this slot.
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

        // The slot, muted: it is what the row is *about* rather than what the row says, and the heading above
        // already names the block it belongs to.
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

        // The slot's name in full, for anything that cannot see the row — and now for the eye as well, since
        // the column is the short form.
        AutomationProperties.SetName(button, row.Word);
        ToolTip.SetTip(slot, row.Word);

        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>
    /// The plan column: a tick where the hull already matches, the roll alone where only the module
    /// does, and nothing at all where there is no plan.
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

        // Where the module is already the right one, the plan is about the roll and says only that: "7A Power
        // Dist." twice over is the noise this change exists to remove.
        var agreed = plan.Module is { Length: > 0 } wanted
                     && string.Equals(wanted, parts.Current.Module, StringComparison.Ordinal);

        return Cell(
            agreed ? plan with { Module = null, Long = null } : plan,
            string.Empty,
            gear: false,
            bold: false);
    }

    /// <summary>
    /// One side of a slot row, drawn: the module, then what was rolled on it, then as much of what the
    /// roll did as the column has room for.
    /// </summary>
    /// <param name="vacant">
    /// What a silent side says. "empty" for the hull, and nothing for a plan — a slot with no plan is
    /// not an empty plan, it is a slot nobody has asked anything of.
    /// </param>
    /// <param name="gear">
    /// Whether a roll has been done here, which is only ever the hull's fact.
    /// </param>
    /// <param name="bold">
    /// Whether the module leads in bold, which the hull's side does and a plan does not.
    /// </param>
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

        // **`Text` where one run would do.** A `TextBlock` carrying `Inlines` reports no `Text` at all, so a
        // cell drawn as inlines is invisible to anything reading the panel by text — which is how the tests
        // read it, and how the VR surface's capture check does.
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
    /// re-figures it whenever the cell changes width, which is what "dynamic" has to mean on a panel
    /// that runs from 512 to 2048 logical pixels and is resized by hand.
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
    /// 2026-08-25).
    /// </summary>
    private static void Hover(Control cell, LoadoutSide side)
    {
        if (side.Long is { Length: > 0 } spelled)
        {
            ToolTip.SetTip(cell, spelled);
        }
    }

    /// <summary>The roll itself — blueprint, grade, experimental — which never gets trimmed away.</summary>
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

    /// <summary>As many effects as the cell has room for, cut at a whole effect.</summary>
    private static string Fitted(
        TextBlock cell, string module, string head, IReadOnlyList<string> effects, bool bold)
    {
        // What is left after the module has taken its share.
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

/// <summary>A page that redraws itself when its mode changes underneath.</summary>
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

        // Being put back by the strip means having missed whatever happened while it was out, so a page
        // catches up rather than trusting what it last drew.
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

    /// <summary>The surface went mini, or came back.</summary>
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
/// The index (Phase 26, "The fleet, and the fleet you intend"; Phase 27, "The same page, on foot").
/// </summary>
public sealed class IndexPage : LoadoutPage
{
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;
    private readonly StackPanel _list = new() { Spacing = 3 };

    /// <summary>Where the cards go, for a mode that has them.</summary>
    private readonly WrapPanel _cards = new();

    private readonly ScrollViewer _scroller;

    /// <summary>The index's own switch, kept so it can be withdrawn when it has nothing to do.</summary>
    private readonly ToggleSwitch? _switch;

    /// <summary>The box the switch and its label share, withdrawn along with the switch.</summary>
    private readonly StackPanel? _switchBox;

    public IndexPage(ILoadoutMode mode, PanelNavigator nav, PanelPrompts prompts)
        : base(mode)
    {
        _nav = nav;
        _prompts = prompts;

        var intend = LoadoutPages.Press(mode.NewLabel, Intend);

        // The head of the page: what you can add, and how you want to look at what is there.
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

        DockPanel.SetDock(intend, Dock.Left);
        head.Children.Add(intend);

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine(mode.SayAtIndex);

        _scroller = LoadoutPages.Scrolling(_list);

        // After the scroller exists, because the switch re-lays the grid and would otherwise be capturing a
        // field the constructor has not filled in yet.
        if (mode.IndexToggle is { } toggle)
        {
            // A ToggleSwitch, the same control the raw-journal switch uses — a Commander asked for that one
            // by name, and two switches in one app that look different are two controls. The label sits
            // beside it rather than inside it: a ToggleSwitch stacks its Content above the knob, which
            // wraps one word onto two lines in a bar this short.
            _switch = new ToggleSwitch
            {
                Name = "FleetToggle",
                IsChecked = toggle.On,
                OnContent = null,
                OffContent = null,
                FontSize = TypeScale.Secondary,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _switch.IsCheckedChanged += (_, _) =>
            {
                toggle.Set(_switch.IsChecked == true);

                // The cards are rebuilt because the drawing is part of the card, and re-laid because the
                // height it needs changed with it.
                Refresh();
                Lay(_scroller.Bounds.Width);
            };

            _switchBox = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = toggle.Label,
                        FontSize = TypeScale.Secondary,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    _switch,
                },
            };

            DockPanel.SetDock(_switchBox, Dock.Right);
            head.Children.Add(_switchBox);
        }

        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(head);
        root.Children.Add(say);
        root.Children.Add(_scroller);

        // Sized against the scroller rather than against the cards, which would be the panel measuring
        // itself: setting a child's width inside its own SizeChanged is how a layout pass comes back round
        // for another go.
        _scroller.SizeChanged += (_, size) => Lay(size.NewSize.Width);

        Content = root;

        Refresh();
    }

    /// <summary>How wide a card is, and how tall, for the width the page actually has.</summary>
    private void Lay(double available)
    {
        if (!Mode.Cards || available <= 0)
        {
            return;
        }

        // Below this a card is narrower than the name it carries, and the grid should become one column
        // rather than two unreadable ones.
        const double Smallest = 210;

        var columns = Math.Max(1, (int)Math.Floor(available / Smallest));

        // A pixel back, because a width that divides the space exactly still wraps once the panel's own
        // rounding goes the wrong way — and a wrap one card early is a column of whitespace down the side of
        // the page.
        var width = Math.Floor(available / columns) - 1;

        _cards.ItemWidth = width;

        // 64 is what the name and its note need, and the drawing adds a 16:9 box of whatever width is left
        // inside the card's padding.
        const double Text = 64;
        const double Sides = 20;

        _cards.ItemHeight = Drawings
            ? Text + Math.Floor((width - Sides) * 9 / 16)
            : Text;
    }

    /// <summary>Whether the index is drawing its artwork.</summary>
    private bool Drawings => (Mode.IndexToggle?.On ?? false) && _drawable;

    /// <summary>Whether any row in the index has artwork behind it.</summary>
    private bool _drawable;

    protected override void Refresh()
    {
        _list.Children.Clear();

        // A question left over from one d47 asked out loud (Phase 38).
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

        if (!Mode.Cards)
        {
            foreach (var row in rows)
            {
                _list.Children.Add(LoadoutPages.Row(
                    row.Text,
                    row.Aside,
                    row.Marked,
                    () => _nav.Drill(LoadoutPages.Crumb(Mode, row)),
                    row.Engineered));
            }

            return;
        }

        _cards.Children.Clear();

        // Asked before the cards are built, because the height they get depends on the answer and the switch
        // has nothing to offer a fleet with no captured hull in it.
        _drawable = rows.Any(row => ShipArt.For(row.Hull) is not null);

        if (_switch is { } box)
        {
            box.IsVisible = _drawable;
        }

        foreach (var row in rows)
        {
            // Read off the trail rather than kept as a second piece of state (#110): the last crumb is what
            // the other pane is drawing, and every card builds the crumb it would drill to anyway — so the
            // outline follows the right pane however it got there, pressed here, reached by voice, or arrived
            // at by the back gesture.
            var crumb = LoadoutPages.Crumb(Mode, row);
            var showing = _nav.Trail.Count > 0
                          && string.Equals(_nav.Trail[^1].Key, crumb.Key, StringComparison.Ordinal);

            _cards.Children.Add(LoadoutPages.Card(
                row.Text,
                row.Aside,
                row.Marked,
                () => _nav.Drill(crumb),
                row.Standing,
                row.Hull,
                Drawings,
                showing));
        }

        _list.Children.Add(_cards);

        // The scroller has its width already on a redraw; only the first pass through here is ahead of a
        // layout, and its SizeChanged catches that one.
        Lay(_scroller.Bounds.Width);
    }

    private void Intend() => Mode.New(_prompts, Refresh);
}

/// <summary>One item's slots (Phase 26, "What is fitted and what you want").</summary>
public sealed class ItemPage : LoadoutPage
{
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts? _prompts;
    private readonly string _item;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly Button? _drop;
    private readonly StackPanel _list = new() { Spacing = 3 };

    /// <summary>What the page is about, in the size a name goes in (#289).</summary>
    private readonly TextBlock _title = new()
    {
        FontSize = TypeScale.Heading,
        FontWeight = FontWeight.Bold,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8),
    };

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

    public ItemPage(
        ILoadoutMode mode,
        PanelNavigator nav,
        string item,
        PanelPrompts? prompts,
        Func<string, Task<bool>>? copy = null)
        : base(mode)
    {
        _nav = nav;
        _item = item;
        _prompts = prompts;
        _copy = copy;

        LoadoutPages.Themed(_title, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        LoadoutPages.Themed(_summary, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var promote = LoadoutPages.Press(mode.PromoteLabel, () => Said(Mode.Promote(_item)));

        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { promote },
        };

        // Only where there is something to drop — an intended hull rather than an owned ship (remediation.md
        // 11, item 7).
        if (mode.DropLabel(item) is { Length: > 0 } label)
        {
            _drop = LoadoutPages.Press(label, Drop);
            actions.Children.Add(_drop);
        }

        var root = new DockPanel { Margin = new Thickness(14) };
        var say = LoadoutPages.SayLine(mode.SayAtItem);

        DockPanel.SetDock(_title, Dock.Top);
        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(actions, Dock.Top);
        DockPanel.SetDock(say, Dock.Bottom);

        root.Children.Add(_title);
        root.Children.Add(_summary);
        root.Children.Add(actions);
        root.Children.Add(say);
        root.Children.Add(LoadoutPages.Scrolling(_list));

        // The page, with a layer above it for the thing being carried.
        _overlay = new Canvas { IsHitTestVisible = false };

        Content = new Avalonia.Controls.Panel { Children = { root, _overlay } };

        Refresh();
    }

    /// <summary>
    /// Puts a sentence on the line under the heading, or takes the line away when there is none.
    /// </summary>
    private void Said(string? sentence)
    {
        _summary.Text = sentence ?? string.Empty;
        _summary.IsVisible = sentence is { Length: > 0 };
    }

    /// <summary>Drops the plan, having asked (remediation.md 11, item 7).</summary>
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
                Mode.Title(_item) is { } what
                    ? $"{what}. There is no way back from this one."
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

    /// <summary>Drops it and says so on the summary line, which is where <c>Promote</c> already reports.</summary>
    private void Dropped()
    {
        var said = Mode.Drop(_item);

        Refresh();

        Said(said);
    }

    protected override void Refresh()
    {
        _list.Children.Clear();

        var title = Mode.Title(_item);
        var summary = Mode.Summary(_item);

        // Gone is neither: a mode with no heading says everything in the summary, and one with a heading says
        // nothing in the summary for an owned ship.
        if (title is null && summary is null)
        {
            _title.IsVisible = false;
            _summary.IsVisible = false;
            _list.Children.Add(LoadoutPages.Muted("That build is not there any more."));
            return;
        }

        _title.Text = title ?? string.Empty;
        _title.IsVisible = title is { Length: > 0 };

        Said(summary);

        // The same question the index carries, here too (Phase 38): a Commander who has drilled into the ship
        // it is about is the likeliest one to answer it, and they would otherwise have to go back a level to
        // find out it had been asked.
        if (Mode.Notice() is { } notice)
        {
            _list.Children.Add(LoadoutPages.Notice(notice, said =>
            {
                Refresh();
                Said(said);
            }));
        }

        var rows = Mode.Slots(_item);

        if (rows.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted(Mode.EmptySlots));
            return;
        }

        // **Mini gets neither of the two blocks below.** They are read once when the page opens and then
        // scrolled past, which is a fair trade on a window and not on six rows: a mini surface that spends
        // all six saying what a Python is has not answered the question it was switched to
        // (docs/plans/change-requests.md 38).
        var roomy = !Mini || rows.All(row => row.Parts is null);

        // What the ship is, before what is in it (remediation.md 13, item 2).
        var facts = new StackPanel { Spacing = 3 };

        foreach (var line in roomy ? Mode.Details(_item) : [])
        {
            // Stepped rather than Line, for the copy glyph a whereabouts line carries: the system a ship is
            // parked in goes on the clipboard from here, to be pasted into the Galaxy Map.
            facts.Children.Add(LoadoutPages.Stepped(line, _copy));
        }

        // Power and jump range, at the head of the slot list and under the hull's own figures (Phase 38).
        foreach (var gauge in roomy ? Mode.Gauges(_item) : [])
        {
            facts.Children.Add(LoadoutPages.Gauge(gauge));
        }

        _list.Children.Add(roomy && Mode.HullOf(_item) is { Length: > 0 } hull
            ? HullPicture.For(hull, facts)
            : facts);

        // **Mini shows only the rows that disagree** (the Commander's ruling, 2026-08-25).
        var slots = Mini && rows.Any(row => row.Parts is not null)
            ? [.. rows.Where(row => row.Parts is null || row.Marked)]
            : rows;

        if (slots.Count == 0)
        {
            _list.Children.Add(LoadoutPages.Muted("Nothing outstanding on this ship."));
            return;
        }

        // The header, once, above the first slot row — and only where there are slot rows to head
        // (docs/plans/change-requests.md 38).
        if (slots.Any(row => row.Parts is not null))
        {
            _list.Children.Add(LoadoutPages.SlotHeader());
        }

        // A heading wherever the group changes, so a ship's slots read as the four blocks of the outfitting
        // screen rather than as thirty-odd names in journal order (remediation.md 12, item 1).
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
    /// </summary>
    private void Draggable(Control control, LoadoutRow row)
    {
        var slot = LoadoutPages.SplitSlot(row.Key).Slot;

        // What this row is, for the geometry that finds the row under the pointer.
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

        // **The pointer is captured by the row the press landed on**, so every move and the release itself
        // arrive here rather than at the row under the cursor.
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
                try
                {
                    args.Handled = true;

                    var target = SlotUnder(args.GetPosition(this)) ?? _over ?? from;

                    if (target == from)
                    {
                        Said($"{row.Word} is where that plan already is, so nothing was copied.");
                        return;
                    }

                    // **Explained rather than refused in silence** (remediation.md 17, item 8).
                    var said = Mode.Copy(_item, from, target);

                    // Before Said, because Refresh writes the build's own summary into this block.
                    Refresh();

                    Said(said);
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
            // What is being carried rather than where it came from: the aside is the plan, and the plan is
            // the thing being copied.
            said.Text = row.Aside is { Length: > 0 } plan ? plan : row.Word;
        }

        _ghost.IsVisible = true;

        // Beside the pointer rather than under it, so the ghost never hides the row it is about to land on.
        Canvas.SetLeft(_ghost, at.X + 14);
        Canvas.SetTop(_ghost, at.Y + 12);

        Cursor = _copying ??= new Cursor(StandardCursorType.DragCopy);
    }

    /// <summary>Everything <see cref="Carry"/> turned on, turned off.</summary>
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

    /// <summary>Which row the pointer is over, by geometry rather than by hit-testing.</summary>
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
    /// What the rows look like while a plan is over them: dimmed where the drop would be refused, so
    /// the Commander learns the rule from the interface rather than from a message afterwards.
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

/// <summary>One slot (Phase 26, "What is fitted and what you want").</summary>
public sealed class SlotPage : LoadoutPage
{
    private readonly PanelPrompts _prompts;
    private readonly string _item;
    private readonly string _slot;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly StackPanel _body = new() { Spacing = 4 };

    public SlotPage(
        ILoadoutMode mode, PanelPrompts prompts, string item, string slot, Func<string, Task<bool>>? copy = null)
        : base(mode)
    {
        _prompts = prompts;
        _item = item;
        _slot = slot;
        _copy = copy;

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
            // Stepped, because the grade on this page is a control: moving it re-costs the block below
            // without leaving the page (remediation.md 15, item 4).
            _body.Children.Add(LoadoutPages.Stepped(line, _copy));
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

/// <summary>The gap between every plan and what the Commander is carrying (Phase 27, "Gap analysis").</summary>
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

    /// <summary>Redraws against the live plans.</summary>
    public void Refresh()
    {
        _body.Children.Clear();

        var report = _gap.Of(_includeIntended);

        // The filter, and it says which question it is answering rather than merely which state it is in:
        // counting hulls nobody owns accounts for the whole ambition, and excluding them answers what can
        // be finished now.
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
    /// Redrawn on the way in as well as on a change, because the third thing this page reads is the
    /// Commander's own inventory — which moves without either store having said anything.
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

        // Trade second and never instead: the headline stays the raw shortfall.
        if (line.Trade is { } trade)
        {
            _body.Children.Add(LoadoutPages.Muted(trade.Describe()));
        }

        // What wants it.
        if (line.Wanted.Count > 0)
        {
            _body.Children.Add(LoadoutPages.Muted(
                "Wanted by: " + string.Join(", ", line.Wanted.Select(demand => demand.Describe()))));
        }
    }
}
