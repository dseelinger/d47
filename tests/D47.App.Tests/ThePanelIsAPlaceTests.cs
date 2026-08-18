using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The bar, the modes, the breadcrumb and the panes (list.md Phase 25, "The panel becomes a
/// place").
/// <para>
/// The collapse is what pays for the bar: Conversation, Technical and the log file were three
/// tabs and are three modes of one, because they are three readings of one exchange rather than
/// three destinations.
/// </para>
/// </summary>
public class ThePanelIsAPlaceTests
{
    private static PanelView Laid(PanelView panel, double width = 900, double height = 700)
    {
        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static PanelView Furnished(double width = 900)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("locker", "Suits and weapons"));

        return Laid(panel, width);
    }


    /// <summary>
    /// One tab for the three readings, and the tabs the host has not furnished are not drawn.
    /// A dead tab is worse than an absent one.
    /// </summary>
    [AvaloniaFact]
    public void TheBarCarriesOneTranscriptTabAndOnlyWhatWasFurnished()
    {
        var panel = Laid(new PanelView { DataContext = new PanelViewModel() });

        Assert.True(panel.GetControl<RadioButton>("TranscriptTab").IsVisible);

        foreach (var name in new[] { "ChecklistTab", "LoadoutTab", "EngineersTab", "UtilitiesTab", "SettingsTab" })
        {
            Assert.False(panel.GetControl<RadioButton>(name).IsVisible, name);
        }

        // And there is no second tab strip: the three readings are one drop-down inside the pane,
        // not four more tabs beside it (remediation.md 10, item 1).
        Assert.Equal(3, PanelModes.Count(panel));
    }

    /// <summary>
    /// The mode control is visible at a root and hidden below one: a mode switch three levels
    /// into a ship is a question about which stack you are in, and the breadcrumb is already
    /// answering the one about where you are.
    /// </summary>
    [AvaloniaFact]
    public void TheModeControlIsARootOnlyAffordance()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        Assert.True(PanelModes.Offered(panel));

        panel.Nav.Drill(new NavCrumb("ship:12", "Corsair"));
        Dispatcher.UIThread.RunJobs();

        Assert.False(PanelModes.Offered(panel));
    }

    /// <summary>
    /// A tab with one root shows no mode control at all, rather than a control with one entry.
    /// </summary>
    [AvaloniaFact]
    public void ATabWithOneRootHasNoModeControl()
    {
        var panel = Laid(new PanelView { DataContext = new PanelViewModel() });

        panel.EnableSettings(() => new TextBlock { Text = "settings" });
        panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.False(PanelModes.Offered(panel));
    }

    /// <summary>
    /// The breadcrumb is drawn below a root and not at one — at a root its only crumb would be
    /// the tab, which is already highlighted a row above.
    /// </summary>
    [AvaloniaFact]
    public void TheBreadcrumbAppearsOnlyBelowARoot()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<StackPanel>("CrumbRow").IsVisible);

        panel.Nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));
        Dispatcher.UIThread.RunJobs();

        var crumbs = panel.GetControl<StackPanel>("CrumbRow");

        Assert.True(crumbs.IsVisible);

        var words = crumbs.Children.OfType<Button>()
            .Select(button => button.Content as string ?? string.Empty)
            .ToList();

        // The whole trail, root first — including levels the Commander never walked, because
        // voice jumps and the breadcrumb has to be right the first time it is drawn.
        Assert.Equal(["Ships", "Corsair", "Weapon 3"], words);

        // Every crumb but the last is pressable; the last is where you are, and a control that
        // does nothing costs a press to learn that.
        Assert.Equal([true, true, false], crumbs.Children.OfType<Button>().Select(b => b.IsEnabled));
    }

    /// <summary>And pressing one goes back to it.</summary>
    [AvaloniaFact]
    public void PressingACrumbGoesBackToIt()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));
        Dispatcher.UIThread.RunJobs();

        var corsair = panel.GetControl<StackPanel>("CrumbRow").Children
            .OfType<Button>()
            .First(button => button.Content as string == "Corsair");

        corsair.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ships", "Corsair"], panel.Nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>
    /// Pressing an already-selected tab returns to its root.
    /// <para>
    /// <b>This is the behaviour that had to be added rather than inherited.</b> The tabs are
    /// <c>RadioButton</c>s and re-pressing a checked one announces nothing at all — no change,
    /// so no event — which would make the one gesture a Commander three levels down reaches for
    /// first the one gesture that does nothing.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void PressingTheTabYouAreOnReturnsToItsRoot()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));
        Dispatcher.UIThread.RunJobs();

        var tab = panel.GetControl<RadioButton>("LoadoutTab");

        Assert.True(tab.IsChecked);

        tab.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, new PointerEventArgs(
            InputElement.PointerMovedEvent,
            tab,
            new Pointer(0, PointerType.Mouse, true),
            null,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None)));
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.Nav.AtRoot);
        Assert.Equal(PanelTab.Loadout, panel.Tab);
    }

    /// <summary>Clicking a mode moves the page, so the control and the property stay one thing.</summary>
    [AvaloniaFact]
    public void ClickingAModeMovesTheRoot()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        PanelModes.Choose(panel, "locker");

        Assert.Equal("locker", panel.Nav.RootKeyOf(PanelTab.Loadout));
    }

    /// <summary>
    /// Drilling in and reflowing are one mechanism: how many panes fit. Wide shows the level you
    /// are on beside the one above it; narrow shows one and you drill.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(520, 1)]
    [InlineData(900, 2)]
    [InlineData(1500, 3)]
    public void HowManyPanesShowFollowsTheWidth(double width, int panes)
    {
        var panel = Furnished(width);

        panel.Tab = PanelTab.Loadout;
        panel.Nav.GoTo(
            new NavCrumb("ship:12", "Corsair"),
            new NavCrumb("slot:3", "Weapon 3"),
            new NavCrumb("blueprint", "Overcharged"));

        Dispatcher.UIThread.RunJobs();

        var strip = panel.GetVisualDescendants().OfType<DrillView>().Single();

        Assert.Equal(panes, strip.Panes);
    }

    /// <summary>
    /// A chooser replaces the panel until it is dismissed, which is what makes it a level rather
    /// than a pop-up — and while it is up, nothing navigates away.
    /// </summary>
    [AvaloniaFact]
    public void AChooserTakesThePanelAndHoldsIt()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.Drill(new NavCrumb("ship:12", "Corsair"));
        Dispatcher.UIThread.RunJobs();

        ChoiceOption? picked = null;

        panel.Prompts.Choose(
            new ChoiceRequest(
                "choose:weapon3",
                "Weapon 3",
                "Weapon 3",
                "Medium hardpoint. Fitted now: Beam Laser.",
                [new ChoiceOption("beam", "Beam Laser"), new ChoiceOption("multi", "Multi-Cannon")],
                "beam",
                ChoiceSurface.Page),
            option => picked = option);

        Dispatcher.UIThread.RunJobs();

        // The content region gives way to it entirely: a page still visible behind a modal is a
        // page a ray can still reach.
        Assert.True(panel.GetControl<Border>("ModalPane").IsVisible);
        Assert.False(panel.GetControl<Border>("PagePane").IsVisible);
        Assert.False(panel.GetControl<Border>("TranscriptPane").IsVisible);

        // The header carries what is being chosen for, which is the one thing a drop-down cannot
        // do and the whole reason taking the panel costs nothing.
        var shown = panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains("Medium hardpoint. Fitted now: Beam Laser.", shown);
        Assert.Contains("fitted now", shown);

        // No navigating away mid-choice.
        Assert.False(panel.Nav.Select(PanelTab.Transcript));
        Assert.Equal(PanelTab.Loadout, panel.Tab);

        var multi = panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "Multi-Cannon"));

        multi.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("multi", picked?.Key);

        // And dismissing puts back the level it was opened from, rather than the tab's root.
        Assert.False(panel.Nav.Modal);
        Assert.Equal(["Ships", "Corsair"], panel.Nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>
    /// A layer chooser stays over the page instead. Which one a control gets is declared per call
    /// site rather than computed from list length, because a control that behaves differently on
    /// different days is one nobody trusts.
    /// </summary>
    [AvaloniaFact]
    public void ALayerChooserDoesNotTakeThePanel()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        panel.Prompts.Choose(
            new ChoiceRequest(
                "choose:theme",
                "Theme",
                "Theme",
                null,
                [new ChoiceOption("elite", "Elite"), new ChoiceOption("dark", "Dark")],
                "elite",
                ChoiceSurface.Layer),
            _ => { });

        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.Nav.Modal);
        Assert.True(panel.GetControl<Avalonia.Controls.Panel>("Layer").IsVisible);
        Assert.True(panel.GetControl<Border>("PagePane").IsVisible);
    }

    /// <summary>
    /// Mini is the transcript's tail and the provenance line and nothing else, so the bar, the
    /// mode control and the breadcrumb all go with the rest of the chrome.
    /// </summary>
    [AvaloniaFact]
    public void MiniShowsNoBar()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.Drill(new NavCrumb("ship:12", "Corsair"));
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<DockPanel>("TabStrip").IsVisible);
        Assert.False(panel.GetControl<StackPanel>("CrumbRow").IsVisible);
    }

    /// <summary>
    /// Back is the one method every route goes through, and it says whether there was anything to
    /// go back from — so a controller button at a root stays available to whatever else wants it.
    /// </summary>
    [AvaloniaFact]
    public void BackIsOneMethodAndSaysWhetherItDidAnything()
    {
        var panel = Furnished();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.Drill(new NavCrumb("ship:12", "Corsair"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.GoBack());
        Assert.True(panel.Nav.AtRoot);

        // At a tab's root, back leaves the tab.
        Assert.True(panel.GoBack());
        Assert.Equal(PanelTab.Transcript, panel.Tab);

        // And on the transcript at its root there is nothing left to leave.
        Assert.False(panel.GoBack());
    }
}
