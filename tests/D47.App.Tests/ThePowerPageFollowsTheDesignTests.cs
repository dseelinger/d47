using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Ships;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Power page drawn from the design prototype's own ship, captured beside the design's reference
/// screenshots, and driven the way the design says it is driven.
/// </summary>
public class ThePowerPageFollowsTheDesignTests
{
    private const double Plant = 22.93;

    /// <summary>The prototype's 22 modules, in its order: slot, name, MW, hardpoint, priority, role.</summary>
    private static readonly PowerModule[] Prototype =
    [
        new("pd", "Power Distributor", 0.97, false, 1, PowerRole.Core),
        new("ls", "Life Support", 0.67, false, 1, PowerRole.Life),
        new("fsd", "Frame Shift Drive", 0.75, false, 1, PowerRole.Core),
        new("sen", "Sensors", 0.69, false, 1, PowerRole.Core),
        new("thr", "Thrusters", 6.70, false, 2, PowerRole.Core),
        new("sg", "Shield Generator", 4.30, false, 3, PowerRole.Keep),
        new("scb", "Shield Cell Bank", 1.18, false, 3, PowerRole.Keep),
        new("gfb", "Guardian FSD Booster", 0.75, false, 4, PowerRole.Depends),
        new("fs", "Fuel Scoop", 0.52, false, 4, PowerRole.Shed),
        new("afm", "Auto Field-Maintenance", 0.94, false, 5, PowerRole.Shed),
        new("ch", "Cargo Hatch", 0.60, false, 5, PowerRole.Shed),
        new("sb1", "Shield Booster", 1.20, false, 4, PowerRole.Keep),
        new("sb2", "Shield Booster", 1.20, false, 4, PowerRole.Keep),
        new("sb3", "Shield Booster", 1.20, false, 4, PowerRole.Keep),
        new("hs", "Heat Sink Launcher", 0.20, false, 5, PowerRole.Depends),
        new("pdt", "Point Defence", 0.20, false, 5, PowerRole.Keep),
        new("cf", "Chaff Launcher", 0.20, false, 5, PowerRole.Keep),
        new("ws", "Wake Scanner", 0.20, false, 5, PowerRole.Keep),
        new("bl1", "Beam Laser", 0.80, true, 4, PowerRole.Keep),
        new("bl2", "Beam Laser", 0.85, true, 4, PowerRole.Keep),
        new("mc1", "Multi-Cannon", 0.46, true, 4, PowerRole.Keep),
        new("mc2", "Multi-Cannon", 0.46, true, 4, PowerRole.Keep),
    ];

    private static LoadoutPower Power(bool groups = true, double? plant = Plant)
    {
        var deployed = Prototype.Sum(module => module.Megawatts);
        var retracted = Prototype.Where(module => !module.IsHardpoint).Sum(module => module.Megawatts);

        return new LoadoutPower(
            new PowerGauge(
                retracted,
                deployed,
                plant,
                FigureKind.Modelled,
                Prototype.ToDictionary(module => module.Slot, module => new SlotDraw(module.Megawatts, FigureKind.Modelled)))
            {
                Groups = groups
                    ? Prototype.GroupBy(module => module.Priority!.Value)
                        .ToDictionary(group => group.Key, group => group.Sum(module => module.Megawatts))
                    : null,
                Modules = Prototype,
            },
            null);
    }

    private static PowerView View(bool retracted = false, int? selected = null)
    {
        var view = new PowerView();

        view.Show(Power());

        if (retracted)
        {
            view.Retracted = true;
        }

        if (selected is { } priority)
        {
            view.Selected = priority;
        }

        return view;
    }

    private static IReadOnlyList<string> Said(Control root) =>
        [.. root.GetLogicalDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    private static string Screenshots()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary."),
            "design", "handoff", "Power gauge design breakdown", "design_handoff_power_gauge", "screenshots");
    }

    /// <summary>
    /// Saves <paramref name="captured"/> beside the reference screenshot's POWER panel, the build on the
    /// left and the design on the right, and returns the composite's path.
    /// </summary>
    private static string Composite(string captured, string reference)
    {
        using var ours = new Bitmap(captured);
        using var theirs = new Bitmap(Path.Combine(Screenshots(), reference));

        // Where the POWER panel sits in the reference, which also carries the design page's header above it.
        var panel = new Rect(48, 143, PowerView.ViewWidth + 1, 740);
        var size = new PixelSize((int)(ours.Size.Width + panel.Width + 20), (int)Math.Max(ours.Size.Height, panel.Height));

        using var frame = new RenderTargetBitmap(size);

        using (var context = frame.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.Black, new Rect(0, 0, size.Width, size.Height));
            context.DrawImage(ours, new Rect(ours.Size), new Rect(ours.Size));
            context.DrawImage(theirs, panel, new Rect(ours.Size.Width + 20, 0, panel.Width, panel.Height));
        }

        var path = Path.ChangeExtension(captured, null) + "-vs-design.png";

        frame.Save(path, new PngBitmapEncoderOptions());

        return path;
    }

    /// <summary>
    /// Builds the view under the theme, since Segment and Stepper take their control themes when they
    /// are made, and saves it as <paramref name="name"/>.
    /// </summary>
    private static (string Path, PowerView View) Capture(
        Func<PowerView> build, string name, string theme = ThemeCatalog.Elite)
    {
        using var look = AppLook.Put(theme);

        var view = build();
        var window = new Window { Content = view, Width = 1000, Height = 820 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        window.Close();
        Dispatcher.UIThread.RunJobs();

        return (path, view);
    }

    [AvaloniaFact]
    public void TheThreeReferenceStatesAreCapturedBesideTheDesign()
    {
        var states = new[]
        {
            (Capture(() => View(), "power-deployed-p5.png"), "01-3a-deployed-P5.png"),
            (Capture(() => View(retracted: true, selected: 5), "power-retracted-p5.png"), "02-3a-retracted-P5.png"),
            (Capture(() => View(selected: 3), "power-deployed-p3.png"), "03-3a-deployed-P3-damage-line.png"),
        }.Select(state => (state.Item1.View, Composite: Composite(state.Item1.Path, state.Item2))).ToList();

        Assert.All(states, state => Assert.True(File.Exists(state.Composite)));

        // The default opens the priority the full-output line falls inside.
        Assert.Equal(5, states[0].View.Selected);
        Assert.Contains("OVER ~2.11", Said(states[0].View));
        Assert.Contains("~ MODELLED FROM PLAN", Said(states[0].View));
        Assert.Contains("FULL OUTPUT", Said(states[0].View));
        Assert.Contains("OVER", Said(states[0].View));

        // Retracted, P5 fits and no line falls inside it.
        Assert.Contains("No line crosses this priority.", Said(states[1].View));
        Assert.Contains("ON", Said(states[1].View));

        // P3 holds the destroyed line.
        Assert.Contains("LINES IN P3", Said(states[2].View));
        Assert.Contains("DESTROYED 50%", Said(states[2].View));
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void TheDefaultStateIsCapturedInTheOtherThemes(string theme)
    {
        var (path, _) = Capture(() => View(), $"power-deployed-p5-{theme}.png", theme);

        Assert.True(File.Exists(path));
    }

    [AvaloniaFact]
    public void TheShipPageBlockCarriesTheVerdictsAndOpensThePage()
    {
        var opened = 0;
        Button? block = null;

        var path = AppLook.Capture(
            new Border
            {
                Width = 420,
                Padding = new Thickness(14),
                Child = block = (Button)PowerView.Block(Power(), () => opened++),
            },
            "power-ship-page-block.png",
            width: 460,
            height: 220);

        block.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(File.Exists(path));
        Assert.Equal(1, opened);
        Assert.Contains("OVER ~2.11", Said(block));
        Assert.Contains("FITS", Said(block));
    }

    [Fact]
    public void TheOpenedPriorityKeepsTheStacksProportions()
    {
        // P5 of the prototype: every bar clears the minimum, so each is its share of the column exactly.
        var p5 = new[] { 0.94, 0.60, 0.20, 0.20, 0.20, 0.20 };
        var heights = PowerChart.Heights(p5, 480, 24);

        Assert.Equal(480, heights.Sum(), 6);
        Assert.Equal(0.94 / 0.20, heights[0] / heights[2], 6);

        // One module too small to hold its label is raised to it, and the rest keep their ratio to each other.
        var lopsided = PowerChart.Heights([6.70, 3.30, 0.05], 480, 24);

        Assert.Equal(24, lopsided[2], 6);
        Assert.Equal(480, lopsided.Sum(), 6);
        Assert.Equal(6.70 / 3.30, lopsided[0] / lopsided[1], 6);
    }

    [AvaloniaFact]
    public void TheReducedStatesSayWhyThereIsNoStack()
    {
        var unread = new PowerView();
        unread.Show(Power(groups: false));

        Assert.Contains(PowerView.NoGroups, Said(unread));
        Assert.Contains("RETRACTED", Said(unread));
        Assert.Null(unread.Chart);

        var plantless = new PowerView();
        plantless.Show(Power(plant: null));

        Assert.Contains(PowerView.NoPlant, Said(plantless));
        Assert.Contains("DRAW DEPLOYED", Said(plantless));
        Assert.Null(plantless.Chart);

        var unseen = new PowerView();
        unseen.Show(new LoadoutPower(null, ShipGauges.Unseen));

        Assert.Contains(ShipGauges.Unseen, Said(unseen));
    }

    [AvaloniaFact]
    public void TheViewKeepsItsChoicesAcrossARedrawAndDropsThemWhenLeft()
    {
        var view = View(retracted: true, selected: 3);

        view.Show(Power());

        Assert.Equal(3, view.Selected);
        Assert.True(view.Retracted);

        view.Forget();
        view.Show(Power());

        Assert.Equal(5, view.Selected);
        Assert.False(view.Retracted);
    }

    private sealed record Hosted(Window Window, PowerView View, IDisposable Look) : IDisposable
    {
        public Point On(Point inChart) => View.Chart!.TranslatePoint(inChart, Window)!.Value;

        public void Dispose()
        {
            Window.Close();
            Look.Dispose();
        }
    }

    private static Hosted Host(PowerView view)
    {
        var look = AppLook.Put();
        var window = new Window { Content = view, Width = 1000, Height = 820 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Hosted(window, view, look);
    }

    [AvaloniaFact]
    public void PressingAPriorityInTheStackOpensIt()
    {
        using var host = Host(View());

        var at = host.On(host.View.Chart!.CentreOf(3)!.Value);

        host.Window.MouseDown(at, MouseButton.Left);
        host.Window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, host.View.Selected);
        Assert.Contains("LINES IN P3", Said(host.View));
    }

    [AvaloniaFact]
    public void TheStepperStopsAtTheLastPriority()
    {
        using var host = Host(View());

        var stepper = host.View.GetVisualDescendants().OfType<Stepper>().Single();
        var arrows = stepper.GetVisualDescendants().OfType<RepeatButton>().ToList();

        Assert.True(arrows[0].IsEnabled);
        Assert.False(arrows[^1].IsEnabled);
    }

    [AvaloniaFact]
    public void DraggingABarReordersItAndResetOrderPutsItBack()
    {
        using var host = Host(View());

        Assert.True(host.View.Chart!.CentreOfBar("ws")!.Value.Y < host.View.Chart.CentreOfBar("afm")!.Value.Y);

        // Wake Scanner, at the top of P5, dropped on the lower half of Auto Field-Maintenance at the bottom.
        var from = host.On(host.View.Chart.CentreOfBar("ws")!.Value);
        var to = host.On(host.View.Chart.CentreOfBar("afm")!.Value + new Point(0, 20));

        host.Window.MouseDown(from, MouseButton.Left);
        host.Window.MouseMove(from + new Point(0, 10));
        host.Window.MouseMove(to);
        host.Window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.View.Chart!.CentreOfBar("ws")!.Value.Y > host.View.Chart.CentreOfBar("afm")!.Value.Y);

        var reset = host.View.GetVisualDescendants().OfType<Button>()
            .First(button => Equals(button.Content, "Reset order"));

        reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.View.Chart!.CentreOfBar("ws")!.Value.Y < host.View.Chart.CentreOfBar("afm")!.Value.Y);
    }
}
