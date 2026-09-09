using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Goals;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A mini panel carries no clickable control, and this is the first test that ever built a page to find
/// out.
/// </summary>
public class MiniCarriesNoPageChromeTests
{
    private static (VrPanelSurface Panel, PanelView View, ChecklistService Checklists) Headset(string mode)
    {
        var (settings, _, paths) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, mode, SettingsCaller.Panel);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        var goals = new GoalBook(
            new GoalStore(Path.Combine(paths.Data, "goals.json"), NullLogger<GoalStore>.Instance),
            () => null,
            () => null,
            checklists);

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            dumpTo: TestSurface.CaptureDirectory,
            checklists: checklists,
            goals: goals);

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        view.Tab = PanelTab.Checklist;

        Serve(panel);

        return (panel, view, checklists);
    }

    /// <summary>
    /// One frame into a buffer nobody reads, twice — the same drive the sibling file uses, and for the
    /// same reason: the class is applied on the way into a draw, so a single pass would test the frame
    /// before it rather than the frame after.
    /// </summary>
    private static void Serve(VrPanelSurface panel)
    {
        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>What a Commander can actually see to press on the furnished page, by the words on it.</summary>
    private static IReadOnlyList<string> DrawnWords(PanelView view)
    {
        var pane = view.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PagePane");

        if (pane?.Child is not { } page)
        {
            return [];
        }

        return
        [
            .. page.GetSelfAndVisualDescendants()
                .OfType<ContentControl>()
                .Where(control => control is Button or CheckBox or ToggleButton)
                .Where(control => control is not RepeatButton)
                .Where(control => !control.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any())
                .Where(control => Drawn(control, view))
                .Select(control => control.Content as string ?? string.Empty)
                .Where(word => word.Length > 0),
        ];
    }

    /// <summary>Whether anything between this control and the surface is hiding it.</summary>
    private static bool Drawn(Control control, PanelView view) =>
        control.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .TakeWhile(above => !ReferenceEquals(above, view))
            .All(above => above.IsVisible);

    /// <summary>The words on the Checklist bar, which is the page's own chrome.</summary>
    private static readonly string[] Bar =
        ["Showing everything", "Order", "Goals (9 running)", "Import/Export"];

    [AvaloniaFact]
    public void MiniCarriesNoneOfThePagesOwnChrome()
    {
        var (panel, view, _) = Headset("mini");
        using var _disposable = panel;

        var drawn = DrawnWords(view);

        foreach (var word in Bar)
        {
            Assert.DoesNotContain(word, drawn);
        }
    }

    /// <summary>
    /// And the big panel keeps every one of them, which is the half that fails if anybody reaches for
    /// the mode instead of the class.
    /// </summary>
    [AvaloniaFact]
    public void TheBigPanelKeepsTheWholeBar()
    {
        var (panel, view, _) = Headset("full");
        using var _disposable = panel;

        var drawn = DrawnWords(view);

        foreach (var word in Bar)
        {
            Assert.Contains(word, drawn);
        }
    }

    /// <summary>The line ticks survive, and that is deliberate rather than a hole.</summary>
    [AvaloniaFact]
    public void TheLineTicksAreNotChromeAndStay()
    {
        var (panel, view, _) = Headset("mini");
        using var _disposable = panel;

        Assert.Contains("buy limpets", DrawnWords(view));
    }

    [AvaloniaFact]
    public void TheBarComesBackOnTheWayOutOfMini()
    {
        var (panel, view, _) = Headset("mini");
        using var _disposable = panel;

        Assert.DoesNotContain("Order", DrawnWords(view));

        view.Classes.Remove("output-only");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Order", DrawnWords(view));
    }

    [AvaloniaFact]
    public void ARebuildDoesNotBringItBack()
    {
        var (panel, view, checklists) = Headset("mini");
        using var _disposable = panel;

        Assert.DoesNotContain("Order", DrawnWords(view));

        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");
        Serve(panel);

        Assert.DoesNotContain("Order", DrawnWords(view));

        // And the line that was just added is drawn, so this is a rebuild that happened rather than a page
        // that stopped redrawing.
        Assert.Contains("sell the cargo", DrawnWords(view));
    }
}
