using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The System page's Orbits section: one level of the system at a time, its plot, its list and the simulated time.</summary>
public sealed partial class StarSystemPage
{
    public const string NoOrbits = "No orbit on record for this system.";

    public const string OrbitsNote = "Seen from above. Angles are true; distances are compressed.";

    /// <summary>Marks each row of the Orbits list, pressable or not.</summary>
    public const string OrbitRowClass = "orbit-row";

    /// <summary>Marks each crumb above the plot.</summary>
    public const string OrbitCrumbClass = "orbit-crumb";

    private StarSystemProfile? _orbitProfile;
    private IReadOnlyList<OrbitNode> _orbitNodes = [];
    private int? _orbitFocus;
    private OrbitClock? _orbitClock;
    private OrbitPlot? _orbitPlot;

    /// <summary>Back to the root, the present and running, at the root's speed.</summary>
    private void ResetOrbits()
    {
        _orbitFocus = null;
        _orbitClock = null;
    }

    private IReadOnlyList<OrbitNode> OrbitNodes(StarSystemProfile profile)
    {
        if (!ReferenceEquals(profile, _orbitProfile))
        {
            _orbitProfile = profile;
            _orbitNodes = Orrery.Nodes(profile);
        }

        return _orbitNodes;
    }

    private Control Orbits(StarSystemProfile profile)
    {
        var nodes = OrbitNodes(profile);

        if (nodes.FirstOrDefault(node => node.IsRoot) is not { } root)
        {
            return Sentence(NoOrbits, ThemeManager.GreyKey);
        }

        var focus = nodes.FirstOrDefault(node => node.BodyId == _orbitFocus) ?? root;
        var level = OrbitLevel.Of(nodes, focus, _surface.Clock.GetUtcNow());
        var clock = _orbitClock ??= new OrbitClock(_surface.Clock, level.DefaultSpeed);

        var plot = _orbitPlot = new OrbitPlot(level, clock, profile.Name) { HorizontalAlignment = HorizontalAlignment.Left };
        Avalonia.Automation.AutomationProperties.SetName(plot, "Orbits");

        var plotColumn = new StackPanel { Spacing = 10, Children = { plot, Legend() } };

        foreach (var missing in level.Unplaced)
        {
            plotColumn.Children.Add(Typeset.Prose($"No orbit on record for {OrbitLevel.Name(missing)}.", TypeScale.Description, ThemeManager.GreyKey, wrap: true));
        }

        var columns = new OrbitColumns { Children = { plotColumn, OrbitList(nodes, level, plot) } };

        return new StackPanel
        {
            Spacing = 12,
            Children = { OrbitCrumbs(nodes, focus), columns, TimeStrip(clock, plot) },
        };
    }

    private void FocusOn(IReadOnlyList<OrbitNode> nodes, OrbitNode node)
    {
        _orbitFocus = node.BodyId;
        _orbitClock?.SetSpeed(OrbitLevel.Of(nodes, node, _surface.Clock.GetUtcNow()).DefaultSpeed);
        Draw();
    }

    private WrapPanel OrbitCrumbs(IReadOnlyList<OrbitNode> nodes, OrbitNode focus)
    {
        var chain = new List<OrbitNode>();

        for (var at = focus; at is not null && !chain.Contains(at); at = at.IsRoot ? null : at.Parent)
        {
            chain.Insert(0, at);
        }

        var line = new WrapPanel { Orientation = Orientation.Horizontal, MinHeight = TypeScale.MinimumTarget };

        for (var i = 0; i < chain.Count; i++)
        {
            if (i > 0)
            {
                line.Children.Add(Typeset.Chrome("›", TypeScale.Body, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, margin: new Thickness(2, 0)));
            }

            var node = chain[i];
            var crumb = new Button
            {
                Content = OrbitLevel.Name(node),
                IsEnabled = i < chain.Count - 1,
                VerticalAlignment = VerticalAlignment.Center,
            };
            crumb.Bind(ThemeProperty, crumb.GetResourceObservable("D47.Crumb"));
            crumb.Classes.Add(OrbitCrumbClass);
            crumb.Click += (_, _) => FocusOn(nodes, node);
            line.Children.Add(crumb);
        }

        var count = nodes.Count(node => node.Parent == focus);
        line.Children.Add(Typeset.Prose(
            $"{OrbitLevel.Kind(focus)} · {OrbitLevel.InOrbit(count)}",
            TypeScale.Description,
            ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center,
            margin: new Thickness(16, 0, 0, 0)));

        return line;
    }

    private static WrapPanel Legend()
    {
        var legend = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 20, LineSpacing = 6 };

        foreach (var (kind, word) in new[] { (OrbitNodeKind.Planet, "Planet"), (OrbitNodeKind.Star, "Star"), (OrbitNodeKind.Barycentre, "Barycentre") })
        {
            var mark = new OrbitMark(kind) { VerticalAlignment = VerticalAlignment.Center };
            ThemeInk.Bind(mark, TextElement.ForegroundProperty, ThemeManager.WhiteKey);

            legend.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { mark, Typeset.Prose(word, TypeScale.Description, ThemeManager.GreyKey, vertical: VerticalAlignment.Center) },
            });
        }

        legend.Children.Add(Typeset.Prose(OrbitsNote, TypeScale.Description, ThemeManager.GreyKey, wrap: true, vertical: VerticalAlignment.Center));

        return legend;
    }

    private StackPanel OrbitList(IReadOnlyList<OrbitNode> nodes, OrbitLevel level, OrbitPlot plot)
    {
        var headCells = new Grid { ColumnDefinitions = OrbitColumnsOfARow() };

        foreach (var (column, word, align) in new[] { (1, "In orbit", TextAlignment.Left), (2, "Distance", TextAlignment.Right), (3, "Period", TextAlignment.Right) })
        {
            var label = Typeset.Chrome(
                word.ToUpperInvariant(), TypeScale.Control, ThemeManager.GreyKey,
                weight: FontWeight.SemiBold, tracking: TypeScale.Control * Fonts.ChromeTracking, vertical: VerticalAlignment.Center, align: align);
            Grid.SetColumn(label, column);
            headCells.Children.Add(label);
        }

        var head = new Border { Height = 32, Padding = new Thickness(12, 0, 0, 0), BorderThickness = new Thickness(0, 0, 0, 1), Child = headCells };
        ThemeInk.Bind(head, Border.BorderBrushProperty, ThemeManager.LineKey);

        var rows = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, Gaps.Tile, 0, 0) };

        foreach (var child in level.Placed)
        {
            var count = nodes.Count(node => node.Parent == child);
            Control row = count > 0 ? OpeningRow(nodes, child, count) : ReadOnlyRow(child);

            row.Classes.Add(OrbitRowClass);
            Avalonia.Automation.AutomationProperties.SetName(row, OrbitLevel.Name(child));
            row.PointerEntered += (_, _) => plot.Highlight(child.BodyId);
            row.PointerExited += (_, _) => plot.Highlight(null);
            rows.Children.Add(row);
        }

        return new StackPanel { MaxWidth = 640, Children = { head, rows } };
    }

    private Button OpeningRow(IReadOnlyList<OrbitNode> nodes, OrbitNode child, int count)
    {
        var cells = RowCells(child, $"{OrbitLevel.Kind(child)} · {OrbitLevel.InOrbit(count)}", ThemeManager.AKey, ThemeManager.AKey);

        var chevron = Typeset.Chrome("›", TypeScale.Figure, ThemeManager.AKey, vertical: VerticalAlignment.Center, align: TextAlignment.Center);
        Grid.SetColumn(chevron, 4);
        cells.Children.Add(chevron);

        var row = new Button
        {
            Content = cells,
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        row.Click += (_, _) => FocusOn(nodes, child);

        return row;
    }

    private static Border ReadOnlyRow(OrbitNode child)
    {
        var row = new Border
        {
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0, 0, 0),
            Child = RowCells(child, OrbitLevel.Kind(child), ThemeManager.WhiteKey, ThemeManager.GreyKey),
        };
        ThemeInk.Bind(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    private static Grid RowCells(OrbitNode child, string second, string nameKey, string restKey)
    {
        var cells = new Grid { ColumnDefinitions = OrbitColumnsOfARow() };

        var mark = new OrbitMark(child.Kind) { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        ThemeInk.Bind(mark, TextElement.ForegroundProperty, nameKey);
        cells.Children.Add(mark);

        var named = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Typeset.Chrome(OrbitLevel.Name(child), TypeScale.ControlLarge, nameKey, weight: FontWeight.Medium, tracking: 0, trimming: TextTrimming.CharacterEllipsis),
                Typeset.Prose(second, TypeScale.Caption, restKey, weight: FontWeight.Normal, tracking: 0, trimming: TextTrimming.CharacterEllipsis),
            },
        };
        Grid.SetColumn(named, 1);
        cells.Children.Add(named);

        var distance = Typeset.Mono(OrbitLevel.DistanceText(OrbitLevel.SemiMajorAxis(child)), TypeScale.Small, restKey, weight: FontWeight.Normal, tracking: 0, vertical: VerticalAlignment.Center, align: TextAlignment.Right);
        Grid.SetColumn(distance, 2);
        cells.Children.Add(distance);

        var period = Typeset.Mono(OrbitLevel.PeriodText(OrbitLevel.Period(child)), TypeScale.Small, restKey, weight: FontWeight.Normal, tracking: 0, vertical: VerticalAlignment.Center, align: TextAlignment.Right);
        Grid.SetColumn(period, 3);
        cells.Children.Add(period);

        return cells;
    }

    private static ColumnDefinitions OrbitColumnsOfARow() => new("24,*,96,120,44");

    private static Grid TimeStrip(OrbitClock clock, OrbitPlot plot)
    {
        var run = new Button { Width = 96 };
        Avalonia.Automation.AutomationProperties.SetName(run, "Pause or run");

        var speed = new Stepper
        {
            Width = 340,
            Wraps = false,
            ItemsSource = [.. OrbitClock.Speeds.Select(step => step.Label)],
            SelectedIndex = clock.Speed,
        };
        Avalonia.Automation.AutomationProperties.SetName(speed, "Speed");

        var now = new Button { Width = 80, Content = "NOW" };

        var state = Typeset.Chrome(string.Empty, TypeScale.MetaSmall, ThemeManager.GreyKey, weight: FontWeight.Medium, tracking: TypeScale.MetaSmall * Fonts.ChromeTracking, horizontal: HorizontalAlignment.Right);
        var when = Typeset.Mono(string.Empty, TypeScale.Body, ThemeManager.WhiteKey, horizontal: HorizontalAlignment.Right);
        Avalonia.Automation.AutomationProperties.SetName(when, "Simulated time");

        void Show()
        {
            run.Content = clock.Running ? "PAUSE" : "RUN";
            state.Text = clock.Running ? "SIMULATED · RUNNING" : "SIMULATED · PAUSED";
            when.Text = Stamp(clock.Now());
        }

        run.Click += (_, _) =>
        {
            if (clock.Running)
            {
                clock.Pause();
            }
            else
            {
                clock.Run();
            }

            Show();
            plot.InvalidateVisual();
            plot.Request();
        };

        speed.SelectionChanged += (_, _) =>
        {
            clock.SetSpeed(speed.SelectedIndex);
            Show();
        };

        now.Click += (_, _) =>
        {
            clock.Present();
            Show();
            plot.InvalidateVisual();
        };

        plot.Moved += () => when.Text = Stamp(clock.Now());

        Show();

        var readout = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            Children = { state, when },
        };

        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*"), ColumnSpacing = Gaps.Tile };

        foreach (var (column, control) in new (int, Control)[] { (0, run), (1, speed), (2, now), (3, readout) })
        {
            Grid.SetColumn(control, column);
            strip.Children.Add(control);
        }

        return strip;
    }
}
