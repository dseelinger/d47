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
/// A mini panel carries no clickable control, and this is the first test that ever built a page to
/// find out (<a href="https://github.com/dseelinger/d47/issues/202">#202</a>).
/// <para>
/// <b><see cref="MiniInTheHeadsetCarriesNoButtonsTests"/> leaves every host surface null</b>, so
/// <c>EnableChecklist</c> is never called, <c>PagePane.Child</c> is null, and it only ever sees
/// <c>PanelView</c>'s own chrome. Nothing on any page had ever been under the rule — which is why
/// a Commander could report a Goals button and a filter checkbox on mini while the suite was
/// green. Worse, its third fact asserted that checkboxes survive, so the suite actively ratified
/// the reported behaviour.
/// </para>
/// <para>
/// <b>The rule is about a container now rather than about a control's type</b>, for two reasons
/// that both bit at once. A style setter sits below <c>LocalValue</c>, so the three controls that
/// assign their own <c>IsVisible</c> pinned it and the selector never applied; and the selector
/// matched exact <c>Button</c>, so a filter checkbox was never covered. A hidden parent hides its
/// children whatever they say about themselves, and it survives a rebuild — which per-control
/// hiding would not, since pages rebuild their contents constantly and build their bars once.
/// </para>
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
    /// One frame into a buffer nobody reads, twice — the same drive the sibling file uses, and for
    /// the same reason: the class is applied on the way into a draw, so a single pass would test
    /// the frame before it rather than the frame after.
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

    /// <summary>
    /// What a Commander can actually see to press on the furnished page, by the words on it.
    /// <para>
    /// By word rather than by count, because the claim is about named controls a Commander
    /// reported seeing rather than about a number — and a count says nothing about <em>which</em>
    /// one survived, which is the whole diagnosis when this fails.
    /// </para>
    /// <para>
    /// Scrollbar parts are left out, and that exemption is the one the original rule already
    /// carried: a <c>RepeatButton</c> and a <c>Thumb</c> are half of a scrollbar rather than
    /// something to press, and removing them would take away the data.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Whether anything between this control and the surface is hiding it.
    /// <para>
    /// <b><c>IsEffectivelyVisible</c> is the wrong question here</b>, and it answers false for
    /// every control in the tree: the headset hosts its <c>PanelView</c> in a window that is
    /// constructed and <em>never shown</em>, which is what makes the overlay path minimise-safe.
    /// So the walk is done by hand and stops at the view, which is the boundary the rule is about
    /// — above it is a window nobody is looking at either way.
    /// </para>
    /// </summary>
    private static bool Drawn(Control control, PanelView view) =>
        control.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .TakeWhile(above => !ReferenceEquals(above, view))
            .All(above => above.IsVisible);

    /// <summary>The words on the Checklist bar, which is the page's own chrome.</summary>
    private static readonly string[] Bar =
        ["Showing everything", "Order", "Goals (9 running)", "Import/Export"];

    /// <summary>
    /// <b>The report, with a page actually built.</b> The Checklist tab showed a Goals control and
    /// an Include Partial Grades checkbox on a 512-wide strip; every other control on that bar was
    /// hidden, which is exactly the shape of the defect — the two that survived are the two that
    /// assign their own visibility, and a local value outranks the style that was the whole rule.
    /// </summary>
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
    /// <b>And the big panel keeps every one of them</b>, which is the half that fails if anybody
    /// reaches for the mode instead of the class. The ray presses these through the geometric hit
    /// test, so this is the one headset surface where they genuinely work.
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

    /// <summary>
    /// <b>The line ticks survive, and that is deliberate rather than a hole.</b> They are inside
    /// the list rather than on the chrome bar, and the docs say a Commander ticks a line off in
    /// the headset — so the rule has to be able to tell one from the other, which is the whole
    /// reason it is about a container.
    /// </summary>
    [AvaloniaFact]
    public void TheLineTicksAreNotChromeAndStay()
    {
        var (panel, view, _) = Headset("mini");
        using var _disposable = panel;

        Assert.Contains("buy limpets", DrawnWords(view));
    }

    /// <summary>
    /// <b>It comes back when the panel does.</b> The headset flips the class on every move between
    /// the big panel and mini, so a rule applied one way only would leave the bar gone for the rest
    /// of the session — and the class is what says it moved, since <c>Mode</c> on the view is not
    /// what the headset changes.
    /// </summary>
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

    /// <summary>
    /// <b>A rebuild does not put it back.</b> Pages rebuild their contents on every change and
    /// build their bars once, which is the property that makes a container the right unit: hiding
    /// each control would be undone by the next redraw, and this is the assertion that says so.
    /// </summary>
    [AvaloniaFact]
    public void ARebuildDoesNotBringItBack()
    {
        var (panel, view, checklists) = Headset("mini");
        using var _disposable = panel;

        Assert.DoesNotContain("Order", DrawnWords(view));

        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");
        Serve(panel);

        Assert.DoesNotContain("Order", DrawnWords(view));

        // And the line that was just added is drawn, so this is a rebuild that happened rather
        // than a page that stopped redrawing.
        Assert.Contains("sell the cargo", DrawnWords(view));
    }
}
