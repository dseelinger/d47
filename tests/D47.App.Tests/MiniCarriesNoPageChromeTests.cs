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

        var controlWords = page.GetSelfAndVisualDescendants()
            .OfType<ContentControl>()
            .Where(control => control is Button or ToggleButton)
            .Where(control => control is not RepeatButton)
            .Where(control => !control.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any())
            .Where(control => Drawn(control, view))
            .Select(control => control.Content as string ?? string.Empty);

        // A switch carries no Content of its own (#223): the word it shows is the TextBlock beside it.
        var switchWords = page.GetSelfAndVisualDescendants()
            .OfType<ToggleSwitch>()
            .Where(toggle => !toggle.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any())
            .Where(toggle => Drawn(toggle, view))
            .Select(SwitchLabel);

        // A stepper shows its selected item rather than a Content (#269, #274).
        var comboWords = page.GetSelfAndVisualDescendants()
            .OfType<D47.App.Controls.Stepper>()
            .Where(stepper => !stepper.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any())
            .Where(stepper => Drawn(stepper, view))
            .Select(stepper => stepper.SelectedItem ?? string.Empty);

        // The custom line's checkbox carries "completed" as its own Content (picked up by controlWords
        // above), but the word worth asserting on is the line it sits beside (#271).
        var checkboxWords = page.GetSelfAndVisualDescendants()
            .OfType<CheckBox>()
            .Where(box => !box.GetSelfAndVisualAncestors().OfType<ScrollBar>().Any())
            .Where(box => Drawn(box, view))
            .Select(Ticks.Label);

        return
        [
            .. controlWords.Concat(switchWords).Concat(comboWords).Concat(checkboxWords)
                .Where(word => word.Length > 0),
        ];
    }

    /// <summary>The label beside a switch, wherever it sits among the switch's siblings.</summary>
    private static string SwitchLabel(ToggleSwitch toggle) =>
        toggle.GetVisualParent() is Control parent
            ? parent.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty
            : string.Empty;

    /// <summary>Whether anything between this control and the surface is hiding it.</summary>
    private static bool Drawn(Control control, PanelView view) =>
        control.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .TakeWhile(above => !ReferenceEquals(above, view))
            .All(above => above.IsVisible);

    /// <summary>The words on the Checklist bar, which is the page's own chrome.</summary>
    private static readonly string[] Bar =
        ["Everything", "Goals (9 running)", "Delete completed items"];

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

        Assert.DoesNotContain("Everything", DrawnWords(view));

        view.Classes.Remove("output-only");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Everything", DrawnWords(view));
    }

    [AvaloniaFact]
    public void ARebuildDoesNotBringItBack()
    {
        var (panel, view, checklists) = Headset("mini");
        using var _disposable = panel;

        Assert.DoesNotContain("Everything", DrawnWords(view));

        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");
        Serve(panel);

        Assert.DoesNotContain("Everything", DrawnWords(view));

        // And the line that was just added is drawn, so this is a rebuild that happened rather than a page
        // that stopped redrawing.
        Assert.Contains("sell the cargo", DrawnWords(view));
    }
}
