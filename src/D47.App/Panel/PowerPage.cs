using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>A ship's Power page: <see cref="PowerView"/>, redrawn when the ship's build changes (#469).</summary>
public sealed class PowerPage : LoadoutPage
{
    private readonly string _item;

    public PowerPage(ILoadoutMode mode, PanelNavigator nav, string item)
        : base(mode)
    {
        _item = item;

        var key = LoadoutPages.Power(item).Key;

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(14), Child = View },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        // The what-if lasts while the page is on the trail and goes when the Commander leaves it.
        nav.Changed += (_, _) =>
        {
            if (!nav.Trail.Any(crumb => crumb.Key == key))
            {
                View.Forget();
            }
        };

        Refresh();
    }

    internal PowerView View { get; } = new();

    protected override void Refresh() => View.Show(Mode.Power(_item));
}

/// <summary>
/// The verdicts, the stack of every module, and one priority opened up beside it (#469). Drawn at the
/// design's fixed width and scaled down, never up, to fit the window.
/// </summary>
public sealed class PowerView : UserControl
{
    public const double ViewWidth = 980;

    public const string NoPlant = "No power plant visible, so there is nothing to compare the draw against.";

    public const string NoGroups = "Board this ship once to read its priority groups.";

    public const string Footnote = "Damage levels from the community wiki, not checked against the game.";

    private readonly Viewbox _view = new()
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private LoadoutPower? _power;
    private bool _retracted;
    private int? _selected;
    private List<string>? _order;
    private IReadOnlyList<string> _slots = [];

    public PowerView() => Content = _view;

    /// <summary>The chart as last drawn, or null in a reduced state.</summary>
    internal PowerChart? Chart { get; private set; }

    /// <summary>Whether the stack counts hardpoints out of the draw.</summary>
    internal bool Retracted
    {
        get => _retracted;
        set
        {
            _retracted = value;
            Redraw();
        }
    }

    /// <summary>The priority opened up, once drawn.</summary>
    internal int? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Redraw();
        }
    }

    /// <summary>Draws <paramref name="power"/>, keeping the mode, selection and order already chosen.</summary>
    public void Show(LoadoutPower? power)
    {
        _power = power;
        Redraw();
    }

    /// <summary>Drops the mode, selection and order back to their defaults.</summary>
    public void Forget()
    {
        _retracted = false;
        _selected = null;
        _order = null;
    }

    private void Redraw() => _view.Child = Draw(_power);

    private Border Draw(LoadoutPower? power)
    {
        var body = new StackPanel { Spacing = 14 };

        var frame = new Border
        {
            Width = ViewWidth,
            Padding = new Thickness(20),
            BorderThickness = new Thickness(1),
            Child = body,
        };

        LoadoutPages.Themed(frame, Border.BackgroundProperty, ThemeManager.BgKey);
        LoadoutPages.Themed(frame, Border.BorderBrushProperty, ThemeManager.Line2Key);

        Chart = null;

        if (power?.Gauge is not { } gauge)
        {
            body.Children.Add(Head(null));
            body.Children.Add(Prose(power?.Silent ?? "There is no power budget for this ship.", 14, ThemeManager.GreyKey));
            return frame;
        }

        body.Children.Add(Head(gauge.Kind == FigureKind.Modelled ? "~ MODELLED FROM PLAN" : "MEASURED IN GAME"));

        if (gauge.Capacity is not { } made || made <= 0)
        {
            body.Children.Add(Draws(gauge));
            body.Children.Add(Prose(NoPlant, 14, ThemeManager.GreyKey));
            return frame;
        }

        if (gauge.Groups is null || gauge.Modules.Any(module => module.Priority is not (>= 1 and <= PowerPriorities.Lowest)))
        {
            body.Children.Add(new Border { Width = 380, HorizontalAlignment = HorizontalAlignment.Left, Child = Verdicts(gauge) });
            body.Children.Add(Slab(NoGroups));
            return frame;
        }

        _slots = [.. gauge.Modules.Select(module => module.Slot)];

        var priorities = PowerPriorities.Of(gauge.Modules, made, _retracted, _order ?? _slots);
        var selected = _selected ??= priorities.DefaultSelected;
        var drill = priorities.Drill(selected);

        body.Children.Add(Controls(gauge, drill.Band, selected));

        var chart = new PowerChart();

        chart.Show(priorities, selected, priorities.Deployed.Draw);
        chart.Selected += priority =>
        {
            _selected = priority;
            Redraw();
        };
        chart.Reordered += Reorder;
        Chart = chart;

        body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 20,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { chart, Side(priorities, drill, selected) },
        });

        body.Children.Add(Prose(Footnote, 12, ThemeManager.Grey2Key));

        return frame;
    }

    /// <summary>The verdicts and the mode switch on the left; the priority stepper and its figures on the right.</summary>
    private Grid Controls(PowerGauge gauge, PriorityBand band, int selected)
    {
        var mode = new Segment
        {
            ItemsSource = ["Deployed", "Retracted"],
            SelectedIndex = _retracted ? 1 : 0,
        };

        mode.SelectionChanged += (_, _) =>
        {
            _retracted = mode.SelectedIndex == 1;
            Redraw();
        };

        var left = new StackPanel { Spacing = 2, Children = { Verdicts(gauge), mode } };

        var stepper = new Stepper
        {
            ItemsSource = [.. Enumerable.Range(1, PowerPriorities.Lowest).Select(priority => $"PRIORITY P{priority}")],
            SelectedIndex = selected - 1,
            Wraps = false,
            MaxWidth = double.PositiveInfinity,
        };

        stepper.SelectionChanged += (_, _) =>
        {
            _selected = stepper.SelectedIndex + 1;
            Redraw();
        };

        var reset = new Button { Content = "Reset order", VerticalAlignment = VerticalAlignment.Top };

        reset.Click += (_, _) =>
        {
            _order = null;
            Redraw();
        };

        var steps = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };

        Grid.SetColumn(reset, 1);
        steps.Children.Add(stepper);
        steps.Children.Add(reset);

        var figures = Mono(
            $"{PowerChart.Figure(band.Total)} MW · {PowerChart.Figure(band.Start)} → {PowerChart.Figure(band.Cumulative)} MW",
            13,
            ThemeManager.WhiteKey);

        var right = new StackPanel { Spacing = 8, Children = { steps, figures } };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("380,*"), ColumnSpacing = 20 };

        Grid.SetColumn(right, 1);
        row.Children.Add(left);
        row.Children.Add(right);

        return row;
    }

    /// <summary>The lines inside the priority, then whether it keeps power at each output level.</summary>
    private static StackPanel Side(PowerPriorities priorities, PriorityDrill drill, int selected)
    {
        var side = new StackPanel { Spacing = 2, Width = 215 };

        side.Children.Add(GroupHead($"LINES IN P{selected}", 0));

        if (drill.Crossings.Count == 0)
        {
            var none = Prose("No line crosses this priority.", 13, ThemeManager.GreyKey);
            none.Margin = new Thickness(0, 6);
            side.Children.Add(none);
        }

        foreach (var crossing in drill.Crossings)
        {
            var index = priorities.Lines.ToList().IndexOf(crossing.Line);

            var tile = new Border
            {
                Padding = new Thickness(10, 8),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        Chrome(PowerChart.LevelName(index), 12, ThemeManager.WhiteKey, tracking: 0.72),
                        Mono($"{PowerChart.Figure(crossing.Line.Megawatts)} MW", 12, ThemeManager.GreyKey),
                    },
                },
            };

            LoadoutPages.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);
            side.Children.Add(tile);
        }

        side.Children.Add(GroupHead($"P{selected} POWERED AT", 12));

        for (var index = 0; index < priorities.Lines.Count; index++)
        {
            side.Children.Add(Ladder(
                index, priorities.Lines[index].Megawatts, drill.Band.PoweredAt[index]));
        }

        if (drill.Stowed.Count > 0)
        {
            var stowed = Prose(
                "Stowed: " + string.Join(
                    " · ", drill.Stowed.Select(module => $"{module.Name} {PowerChart.Figure(module.Megawatts)}")),
                13,
                ThemeManager.GreyKey);

            stowed.Margin = new Thickness(0, 10, 0, 0);
            side.Children.Add(stowed);
        }

        return side;
    }

    /// <summary>One output level's row: its name and megawatts, then ON, OFF or OVER.</summary>
    private static Grid Ladder(int index, double megawatts, bool powered)
    {
        var label = new Border
        {
            Padding = new Thickness(10, 0),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    Chrome(PowerChart.LevelName(index), 11, ThemeManager.WhiteKey, FontWeight.SemiBold, 0.66),
                    Mono($"{PowerChart.Figure(megawatts)} MW", 11, ThemeManager.GreyKey),
                },
            },
        };

        LoadoutPages.Themed(label, Border.BackgroundProperty, ThemeManager.SlabKey);

        var (word, ground, ink) = powered
            ? ("ON", ThemeManager.Tile2Key, ThemeManager.AKey)
            : index == 0
                ? ("OVER", ThemeManager.RedKey, ThemeManager.KnockKey)
                : ("OFF", ThemeManager.SlabKey, ThemeManager.Grey2Key);

        var text = Chrome(word, 12, ink, FontWeight.SemiBold, 1.2);

        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;

        var state = new Border { Child = text };

        LoadoutPages.Themed(state, Border.BackgroundProperty, ground);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,56"), ColumnSpacing = 2, Height = 40 };

        Grid.SetColumn(state, 1);
        row.Children.Add(label);
        row.Children.Add(state);

        return row;
    }

    private void Reorder(string dragged, string target, bool after)
    {
        var order = (_order ?? [.. _slots]).Where(slot => slot != dragged).ToList();
        var at = order.IndexOf(target);

        if (at < 0)
        {
            return;
        }

        order.Insert(at + (after ? 1 : 0), dragged);
        _order = order;

        Redraw();
    }

    /// <summary>The POWER block on a ship's page: the verdicts, pressed to open the Power page.</summary>
    internal static Control Block(LoadoutPower power, Action open)
    {
        var heading = Chrome("POWER", TypeScale.Meta, ThemeManager.GreyKey, FontWeight.Medium, TypeScale.Meta * Fonts.ChromeTracking);

        if (power.Gauge is not { } gauge)
        {
            return new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(0, 6),
                Children = { heading, LoadoutPages.Muted(power.Silent ?? string.Empty) },
            };
        }

        var tiles = gauge.Capacity is > 0 ? Verdicts(gauge) : Draws(gauge);

        var button = new Button
        {
            Padding = new Thickness(12, 8, 12, 12),
            Margin = new Thickness(0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 6, Children = { heading, tiles } },
        };

        button.Classes.Add(ListRow.Class);
        AutomationProperties.SetName(button, "Power");
        button.Click += (_, _) => open();

        return button;
    }

    /// <summary>The DEPLOYED and RETRACTED verdicts against the plant.</summary>
    internal static Grid Verdicts(PowerGauge gauge)
    {
        var made = gauge.Capacity ?? 0;
        var tilde = gauge.Kind == FigureKind.Modelled ? "~" : string.Empty;

        return Pair(
            Verdict("DEPLOYED", gauge.Deployed, made, tilde),
            Verdict("RETRACTED", gauge.Retracted, made, tilde));
    }

    /// <summary>The draw alone, for a build with no plant to weigh it against.</summary>
    private static Grid Draws(PowerGauge gauge)
    {
        var tilde = gauge.Kind == FigureKind.Modelled ? "~" : string.Empty;

        return Pair(
            Tile("DRAW DEPLOYED", Mono($"{tilde}{PowerChart.Figure(gauge.Deployed)} MW", 20, ThemeManager.WhiteKey)),
            Tile("DRAW RETRACTED", Mono($"{tilde}{PowerChart.Figure(gauge.Retracted)} MW", 20, ThemeManager.WhiteKey)));
    }

    private static Grid Pair(Control left, Control right)
    {
        var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 2 };

        Grid.SetColumn(right, 1);
        pair.Children.Add(left);
        pair.Children.Add(right);

        return pair;
    }

    private static Border Verdict(string label, double draw, double made, string tilde)
    {
        var fits = draw <= made + 1e-9;

        var verdict = Chrome(
            fits ? "FITS" : $"OVER {tilde}{PowerChart.Figure(draw - made)}",
            20,
            fits ? ThemeManager.BlueKey : ThemeManager.RedKey,
            FontWeight.SemiBold,
            1.2);

        var share = made > 0 ? Math.Round(draw / made * 100).ToString("0", CultureInfo.InvariantCulture) : "—";

        return Tile(
            label,
            verdict,
            Mono($"{tilde}{PowerChart.Figure(draw)} MW · {share}%", 12, ThemeManager.WhiteKey));
    }

    private static Border Tile(string label, params Control[] lines)
    {
        var stack = new StackPanel { Spacing = 3 };

        stack.Children.Add(Chrome(label, 11, ThemeManager.GreyKey, tracking: 1.1));
        stack.Children.AddRange(lines);

        var tile = new Border { Padding = new Thickness(12, 10), Child = stack };

        LoadoutPages.Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    /// <summary>POWER over a rule in A, with where the figures came from on the right.</summary>
    private static Border Head(string? provenance)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        row.Children.Add(Chrome("POWER", 15, ThemeManager.AKey, FontWeight.SemiBold, 1.5));

        if (provenance is not null)
        {
            var right = Chrome(provenance, 11, ThemeManager.GreyKey, tracking: 1.1);

            right.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(right, 1);
            row.Children.Add(right);
        }

        var head = new Border
        {
            Padding = new Thickness(0, 0, 0, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };

        LoadoutPages.Themed(head, Border.BorderBrushProperty, ThemeManager.AKey);

        return head;
    }

    private static Border GroupHead(string text, double above)
    {
        var head = new Border
        {
            Padding = new Thickness(0, above, 0, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = Chrome(text, 12, ThemeManager.GreyKey, tracking: 1.2),
        };

        LoadoutPages.Themed(head, Border.BorderBrushProperty, ThemeManager.LineKey);

        return head;
    }

    private static Border Slab(string text)
    {
        var slab = new Border { Padding = new Thickness(12, 10), Child = Prose(text, 14, ThemeManager.WhiteKey) };

        LoadoutPages.Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    private static TextBlock Chrome(
        string text, double size, string key, FontWeight weight = FontWeight.Normal, double tracking = 0)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = size,
            FontWeight = weight,
            LetterSpacing = tracking,
        };

        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static TextBlock Mono(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = size,
        };

        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static TextBlock Prose(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ProseFamily),
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };

        LoadoutPages.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }
}
