using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheChecklistIsInTheHeadsetTests
{
    /// <summary>What a ray-sized target has to clear, in surface pixels.</summary>
    private const double Floor = 30;

    private static ChecklistService Checklists(string root)
    {
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }

    /// <summary>
    /// A line worked out from the journal rather than agreed to, in a state that has something to say
    /// about itself.
    /// </summary>
    private static ChecklistItem Derived()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(12),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 dirty drives",
            Intent = intent,
            State = ChecklistState.Blocked,
        };
    }

    /// <summary>The sentence a blocked derived item carries, which the row has to draw.</summary>
    private static string Blocked => ChecklistNextAction.For(ChecklistState.Blocked)!;

    /// <summary>The headset's own copy of the panel, on the Checklist tab, drawn.</summary>
    private static (VrPanelSurface Panel, PanelView View, ChecklistService Checklists, string Dump) Headset(
        int lines = 0,
        bool derived = false)
    {
        var (settings, _, _) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var checklists = Checklists(TempFolders.Create("d47-checklist-in-vr"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        for (var line = 0; line < lines; line++)
        {
            checklists.AddNote(
                ChecklistScope.Ship(12),
                $"line {line.ToString(System.Globalization.CultureInfo.InvariantCulture)}: fit a fuel scoop");
        }

        if (derived)
        {
            checklists.List.Save(
            [
                checklists.Document with { Items = [.. checklists.Document.Items, Derived()] },
            ]);
        }

        var dump = TestSurface.CaptureDirectory;

        var panel = new VrPanelSurface(
            new PanelViewModel(), settings, _ => null, dumpTo: dump, checklists: checklists);

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        // Spoken rather than assigned, because that is the route a Commander with a headset on actually has:
        // there is no tab to click until the surface has drawn one.
        PanelPhrases.Apply("show me the checklist", panel.Nav);

        Serve(panel);

        return (panel, view, checklists, dump);
    }

    /// <summary>One frame, into a buffer nobody reads.</summary>
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

    /// <summary>Where a control is on the quad's face, in the 0..1 a ray answers in.</summary>
    private static (float U, float V) At(Control control, PanelView view, VrPanelSurface panel)
    {
        var corner = control.TranslatePoint(new Point(0, 0), view);
        Assert.NotNull(corner);

        var centre = corner.Value + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var (width, height) = panel.Size;

        return ((float)(centre.X / width), (float)(centre.Y / height));
    }

    private static ChecklistPage Page(PanelView view) =>
        view.GetVisualDescendants().OfType<ChecklistPage>().Single();

    /// <summary>The spoken route reaches it and still does not reach the fleet, asserted through the phrase rather than the tab's visibility because a tab nobody furnished has no root for a phrase to land on either.</summary>
    [AvaloniaFact]
    public void TheSpokenRouteReachesItAndStillStopsAtTheFleet()
    {
        var (panel, view, _, _) = Headset();
        using var _disposable = panel;

        Assert.Equal(PanelTab.Checklist, view.Tab);

        // Away and back, so the phrase is answered rather than merely not refused: a tab nobody furnished has
        // no root for a phrase to land on, and the answer is the tab's own name.
        Assert.Equal("Transcript.", PanelPhrases.Apply("show me the transcript", panel.Nav));
        Assert.Equal("Checklist.", PanelPhrases.Apply("open the checklist", panel.Nav));

        // And the tab that stayed behind.
        Assert.Null(PanelPhrases.Apply("show me the loadout", panel.Nav));
        Assert.Equal(PanelTab.Checklist, view.Tab);
    }

    /// <summary>
    /// What the headset is handed, written out for a human to look at — the surface's own first-frame
    /// dump rather than a bitmap this test made, so what lands in the folder is what a session would
    /// have written to <c>data/</c>.
    /// </summary>
    [AvaloniaFact]
    public void ItRendersToACaptureAtTheOverlaysOwnSize()
    {
        var (panel, _, _, dump) = Headset(lines: 12, derived: true);
        using var _disposable = panel;

        Assert.Equal((1024, 640), panel.Size);

        var capture = new FileInfo(Path.Combine(dump, $"vr-{panel.Surface}.png"));

        Assert.True(capture.Exists, $"the surface writes its first frame to {capture.FullName}");

        // Not an empty quad.
        Assert.True(capture.Length > 4000, $"the capture is {capture.Length} bytes");
    }

    /// <summary>Every target a ray has to hit clears the floor the VR pages already stood on.</summary>
    [AvaloniaFact]
    public void EveryTargetARayHasToHitClearsTheFloor()
    {
        var (panel, view, _, _) = Headset(lines: 4);
        using var _disposable = panel;

        var card = Page(view).GetVisualDescendants().OfType<Border>()
            .First(border => border.MinHeight > 0 && border.Bounds.Height > 0);

        var (u, v) = At(card, view, panel);

        Assert.True(panel.Press(u, v), "the row takes the press");
        Serve(panel);

        var targets = Page(view).GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is Button or CheckBox)
            .Where(control => control.IsVisible && control.Bounds.Height > 0)
            .ToList();

        // The movers are what makes this worth asserting; without them it is four buttons that were already
        // right.
        Assert.Contains(targets, control => (control as Button)?.Content as string == "Delete");

        Assert.All(
            targets,
            control => Assert.True(
                control.Bounds.Height >= Floor,
                $"'{(control as ContentControl)?.Content}' is {control.Bounds.Height:F0} pixels tall"));

        // And the tab strip itself, which is how a hand gets to this page in the first place.
        Assert.All(
            view.GetVisualDescendants().OfType<RadioButton>().Where(tab => tab.IsVisible && tab.Bounds.Height > 0),
            tab => Assert.True(tab.Bounds.Height >= Floor, $"the {tab.Content} tab is {tab.Bounds.Height:F0} tall"));
    }

    /// <summary>
    /// A long list scrolls to its end, by a ray that never leaves the panel — the interaction most
    /// likely to be wrong in a headset, on the first tab in VR that needs much of it.
    /// </summary>
    [AvaloniaFact]
    public void ALongListScrollsToItsEndWithoutTheRayLeavingThePanel()
    {
        var (panel, view, _, _) = Headset(lines: 60);
        using var _disposable = panel;

        var bar = Page(view).GetVisualDescendants()
            .OfType<ScrollBar>()
            .Single(found =>
                found.IsVisible
                && found.Orientation == Avalonia.Layout.Orientation.Vertical
                && found.Maximum > 0);

        var (width, height) = panel.Size;

        Rect Box()
        {
            var corner = bar.TranslatePoint(new Point(0, 0), view);
            Assert.NotNull(corner);
            return new Rect(corner.Value, bar.Bounds.Size);
        }

        // Twenty pixels short of the bar, which is what a hand at arm's length actually does.
        var u = (float)((Box().Center.X - 20) / width);

        Assert.True(panel.GrabsScroll(u, (float)(Box().Center.Y / height)), "a ray near the bar takes hold");

        // Held at the end of the bar across two frames, which is what a hand does and what this needs: the
        // first pass measures rows that had never been laid out, so the extent — and with it the far end of
        // the bar — is still moving under the ray.
        for (var frame = 0; frame < 2; frame++)
        {
            var toTheEnd = (float)(Box().Bottom / height);

            // The ray never leaves the panel to reach the end of the list, which is the item's own criterion:
            // a gesture that runs off the quad is one the Commander cannot finish.
            Assert.InRange(u, 0f, 1f);
            Assert.InRange(toTheEnd, 0f, 1f);

            panel.Scroll(u, toTheEnd);
            Serve(panel);
        }

        panel.ReleaseScroll();
        Serve(panel);

        // Asserted on where the document actually is rather than on the bar's own Maximum, which is a
        // hundred-odd pixels past the end: the viewer clamps the offset to what there is to scroll and the
        // bar's range does not follow it back down.
        var viewer = bar.GetVisualAncestors().OfType<ScrollViewer>().First();

        Assert.Equal(viewer.Extent.Height - viewer.Viewport.Height, viewer.Offset.Y, 3);

        // And the last line is inside the quad rather than merely realised: a row measured below the viewport
        // is a row that exists and cannot be read.
        var last = Page(view).GetVisualDescendants().OfType<CheckBox>()
            .Single(tick => tick.Content as string == "line 59: fit a fuel scoop");

        var where = last.TranslatePoint(new Point(0, last.Bounds.Height), view);

        Assert.NotNull(where);
        Assert.InRange(where.Value.Y, 0, height);
    }

    /// <summary>
    /// An authored line ticks from the headset and from nothing else: no window is shown, and the press
    /// is a fraction across a quad.
    /// </summary>
    [AvaloniaFact]
    public void AnAuthoredItemTicksFromTheHeadsetAlone()
    {
        var (panel, view, checklists, _) = Headset(lines: 3);
        using var _disposable = panel;

        var tick = Page(view).GetVisualDescendants()
            .OfType<CheckBox>()
            .Single(box => box.Content as string == "buy limpets");

        Assert.False(checklists.Document.Items.Single(item => item.Text == "buy limpets").IsComplete);

        var (u, v) = At(tick, view, panel);

        Assert.True(panel.Press(u, v), "the tick takes the press");
        Serve(panel);

        Assert.True(checklists.Document.Items.Single(item => item.Text == "buy limpets").IsComplete);

        // Nothing was shown to do it.
        Assert.False(((Window)TopLevel.GetTopLevel(view)!).IsVisible);
    }

    /// <summary>
    /// And a derived one refuses, with its sentence readable beside it rather than in a message
    /// somewhere else.
    /// </summary>
    [AvaloniaFact]
    public void ADerivedItemRefusesWithItsSentenceRatherThanTicking()
    {
        var (panel, view, checklists, _) = Headset(derived: true);
        using var _disposable = panel;

        var page = Page(view);

        // One line, one checkbox: the authored one.
        Assert.Single(Ticks.On(page));

        var said = page.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains(said, text => text.Contains("Grade 5 dirty drives", StringComparison.Ordinal));
        Assert.Contains(said, text => text.Contains(Blocked, StringComparison.Ordinal));

        // Pressed where the tick would be if it had one, which is what a Commander who has not read the
        // sentence yet will do.
        var row = page.GetVisualDescendants().OfType<TextBlock>()
            .First(block => (block.Text ?? string.Empty).Contains("Grade 5 dirty drives", StringComparison.Ordinal));

        var (u, v) = At(row, view, panel);

        panel.Press(u, v);
        Serve(panel);

        Assert.False(checklists.Document.Items.Single(item => item.Text == "Grade 5 dirty drives").IsComplete);

        Assert.Contains(
            Page(view).GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains(Blocked, StringComparison.Ordinal));
    }

 /// <summary>The sentence is drawn big enough to be read through a lens.</summary>
    [AvaloniaFact]
    public void TheSecondLineIsSizedToBeReadThroughALens()
    {
        var (panel, view, _, _) = Headset(derived: true);
        using var _disposable = panel;

        var sentence = Page(view).GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => (block.Text ?? string.Empty).Contains(Blocked, StringComparison.Ordinal));

        Assert.Equal(TypeScale.Body, sentence.FontSize);
    }
}
