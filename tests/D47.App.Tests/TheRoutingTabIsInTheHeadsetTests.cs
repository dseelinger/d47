using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Where the Commander is going, in the headset (#52): every root of the Routing tab, drawn and
/// pressed on the surface the headset is actually handed.
/// </summary>
public class TheRoutingTabIsInTheHeadsetTests
{
    /// <summary>What was copied, so a press on a Progress row has somewhere to land.</summary>
    private static CapabilityRegistry Registry(List<string> copied) =>
        CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "navigation",
                Group = "Acting on the game",
                Name = "Navigation",
                Summary = "Stands in for the clipboard, so the press has an effect a test can read.",
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "copy_to_clipboard",
                        Description = "Records what a row asked to copy.",
                        Parameters =
                        [
                            new ToolParameter
                            {
                                Name = "text",
                                Type = ToolParameterType.String,
                                Description = "What to put on the clipboard.",
                                Required = true,
                            },
                        ],
                        Handler = (arguments, _) =>
                        {
                            copied.Add(arguments.TryGetString("text", out var text) ? text : string.Empty);
                            return Task.FromResult(ToolResult.Ok("copied"));
                        },
                    },
                ],
            },
        ]);

    private static RouteHop Hop(string system, double x) => new(system, "G") { Position = (x, 0, 0) };

    /// <summary>
    /// The tab as the composition root hands it over: one record, every root furnished, nothing about
    /// it particular to a surface.
    /// </summary>
    private static RoutingSurface Surface(string folder, CapabilityRegistry registry) =>
        new(
            () => new NavRoute { Hops = [Hop("Sol", 0), Hop("Shinrarta Dezhra", 65)] },
            () => "Sol",
            registry,
            new RoutePlanBook(
                Path.Combine(folder, "route-plans.json"), NullLogger<RoutePlanBook>.Instance),
            () => true,
            null,
            new CommodityBoard(),
            () => 34.5,
            new CommunityGoalSurface(
                new CommunityGoalSearch { Showing = () => true },
                new CommodityLedger(),
                () => "F1",
                () => new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
                at => CommodityLedger.Week(at, DayOfWeek.Thursday, 7)));

    private static ChecklistService Checklists(string folder) =>
        new(
            new ChecklistStore(
                Path.Combine(folder, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(folder, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

    /// <summary>A headset surface with the tab on it, drawn once so everything has a place on the quad.</summary>
    private static (VrPanelSurface Panel, VrPixels Pixels, List<string> Copied) Headset()
    {
        var (settings, _, paths) = TestSurface.Create();

        // Full, said out loud: mini carries no buttons and there would be nothing to press.
        settings.Apply(
            D47.Core.Capabilities.Builtin.VrCapability.ModeKey,
            "full",
            D47.Core.Configuration.SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var copied = new List<string>();

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            checklists: Checklists(paths.Data),
            routing: Surface(paths.Data, Registry(copied)));

        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var pixels = new VrPixels(width, height);

        return (panel, pixels, copied);
    }

    /// <summary>Selects a root and rasterises it, the way the runtime asks for a frame.</summary>
    private static void Draw(VrPanelSurface panel, VrPixels pixels, string root)
    {
        panel.Nav.Select(PanelTab.Routing);
        panel.Nav.SelectRoot(PanelTab.Routing, root);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Routing, panel.Nav.Tab);
        Assert.Equal(root, panel.Nav.RootKeyOf(PanelTab.Routing));

        panel.Invalidate();
        panel.Draw(pixels.Address, pixels.RowBytes);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>How many colours the submitted buffer has, up to the point the answer is settled.</summary>
    private static int Colours(VrPixels pixels, (int Width, int Height) size)
    {
        var distinct = new HashSet<uint>();

        unsafe
        {
            var read = (uint*)pixels.Address;

            for (var index = 0; index < size.Width * size.Height; index++)
            {
                distinct.Add(read[index]);

                if (distinct.Count > 8)
                {
                    break;
                }
            }
        }

        return distinct.Count;
    }

    private static Control? Pressable(VrPanelSurface panel, Func<Control, bool> wanted) =>
        panel.Board.View.GetVisualDescendants().OfType<Control>().FirstOrDefault(wanted);

    /// <summary>Presses a control where it is drawn on the quad, in the 0..1 a ray answers in.</summary>
    private static bool Press(VrPanelSurface panel, Control control)
    {
        var (width, height) = panel.Size;

        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), panel.Board.View);

        Assert.NotNull(centre);

        return panel.Press((float)(centre!.Value.X / width), (float)(centre.Value.Y / height));
    }

    private static IEnumerable<string> TextOf(VrPanelSurface panel) =>
        panel.Board.View.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0);

    private static TextBox Box(VrPanelSurface panel, string name) =>
        panel.Board.View.GetVisualDescendants()
            .OfType<TextBox>()
            .First(box => AutomationProperties.GetName(box) == name);

    /// <summary>
    /// The claim itself: the headset carries the tab, and it carries it where the window does rather
    /// than at the end of the strip.
    /// </summary>
    [AvaloniaFact]
    public void RoutingFollowsChecklistOnTheHeadsetAsItDoesInTheWindow()
    {
        var (panel, _, copied) = Headset();
        var (_, _, paths) = TestSurface.Create();

        var window = new PanelView { DataContext = new PanelViewModel() };

        window.EnableChecklist(Checklists(paths.Data));
        window.EnableRouting(Surface(paths.Data, Registry(copied)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Tabs(window.Nav), Tabs(panel.Nav));
        Assert.Contains(PanelTab.Routing, Tabs(panel.Nav));

        var order = Tabs(panel.Nav);

        Assert.Equal(order.IndexOf(PanelTab.Checklist) + 1, order.IndexOf(PanelTab.Routing));
    }

    private static List<PanelTab> Tabs(PanelNavigator nav) =>
        [.. Enum.GetValues<PanelTab>().Where(nav.Has)];

    /// <summary>Every root the tab has, drawn — the headset's own copy, through the real rasterise.</summary>
    [AvaloniaTheory]
    [InlineData(RoutingPages.PlanRoot)]
    [InlineData(RoutingPages.ProgressRoot)]
    [InlineData(RoutingPages.CourseRoot)]
    [InlineData(RoutingPages.MarketRoot)]
    [InlineData(RoutingPages.CommunityGoalRoot)]
    public void EveryRootRastersInTheHeadset(string root)
    {
        var (panel, pixels, _) = Headset();

        Draw(panel, pixels, root);

        Assert.True(
            Colours(pixels, panel.Size) > 8,
            $"The submitted buffer for {root} has one or two colours, which is a blank quad rather "
            + "than a rendered page.");
    }

    /// <summary>
    /// Plan's own button, pressed by a ray: the form answers on the surface rather than waiting for a
    /// keyboard that is not there.
    /// </summary>
    [AvaloniaFact]
    public void PlansPlotButtonTakesARayPress()
    {
        var (panel, pixels, _) = Headset();

        Draw(panel, pixels, RoutingPages.PlanRoot);

        var plot = Pressable(panel, control => control is Button { Content: "Plot" });

        Assert.NotNull(plot);
        Assert.True(Press(panel, plot!));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("Name a destination first", StringComparison.Ordinal));
    }

    /// <summary>And the errand beside it (Phase 49).</summary>
    [AvaloniaFact]
    public void MarketsFindItButtonTakesARayPress()
    {
        var (panel, pixels, _) = Headset();

        Draw(panel, pixels, RoutingPages.MarketRoot);

        var find = Pressable(panel, control => control is Button { Content: "Find it" });

        Assert.NotNull(find);
        Assert.True(Press(panel, find!));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("Name a commodity first", StringComparison.Ordinal));
    }

    /// <summary>
    /// And a hop on the route, which is the press with no button on it: every system name on the page
    /// is a copy target, and the clipboard is the same PC's.
    /// </summary>
    [AvaloniaFact]
    public void AProgressRowCopiesItsSystemWhenARayPressesIt()
    {
        var (panel, pixels, copied) = Headset();

        Draw(panel, pixels, RoutingPages.ProgressRoot);

        var row = panel.Board.View.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Text == "Shinrarta Dezhra");

        Assert.True(Press(panel, row));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Shinrarta Dezhra"], copied);
    }

    /// <summary>
    /// A destination said rather than typed, onto the board a ray press on the box opens (#51) — the
    /// reason the tab was withheld, answered on the page it was withheld for.
    /// </summary>
    [AvaloniaFact]
    public void PlansDestinationTakesASpokenSystemName()
    {
        var (panel, pixels, _) = Headset();

        Draw(panel, pixels, RoutingPages.PlanRoot);

        var destination = Box(panel, "Destination, required");

        Assert.True(Press(panel, destination));
        Assert.True(panel.Board.IsListening);

        panel.Board.Hear(new Heard("shinrarta dezhra", 1, Final: true));
        panel.Board.Hear(new Heard("done", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("shinrarta dezhra", destination.Text);
    }

    /// <summary>And a number spelled key by key, which is the other half of the same board.</summary>
    [AvaloniaFact]
    public void PlansJumpRangeTakesASpelledFigure()
    {
        var (panel, pixels, _) = Headset();

        Draw(panel, pixels, RoutingPages.PlanRoot);

        var range = Box(panel, "Jump range (ly), optional, filled from your ship");

        Assert.True(Press(panel, range));
        Assert.True(panel.Board.IsListening);

        panel.Board.Hear(new Heard("three four dot five", 1, Final: true));
        panel.Board.Hear(new Heard("done", 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("34.5", range.Text);
    }
}
