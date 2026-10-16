using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>A board objective has a button that plans or shows its ship, on the chapter page, the Stories strip and the headset. Captures go to <see cref="TestSurface.CaptureDirectory"/>.</summary>
[Trait("Category", "Integration")]
public sealed class AStoryShipObjectiveHasAButtonTests
{
    private const string Hull = "cobramkiii";

    private static IReadOnlyList<FleetEntry> OwnedCobra { get; } = [new FleetEntry(null, new StoredShip(7, Hull, "Pathfinder", "Sol"), false)];

    private static Button? Ship(Visual root, string label) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Content as string == label);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ThePlanButtonFillsThePickerAndPlansOnceCommitted()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var ships = Fleet("d47-story-ship-plan");
        var surface = Open(TriggerKind.Board, ships, Hull);
        ReadChapter(surface);
        Save(surface.Window, "story-ship-objective-plan-chapter.png");

        var plan = Assert.IsAssignableFrom<Button>(Ship(surface.Panel, "Plan a Cobra MkIII"));
        Assert.True(plan.Bounds.Height >= 44);
        Assert.Empty(ships.Plans.Store.Builds);

        Press(plan);

        var pick = surface.Panel.GetVisualDescendants().OfType<ListBox>().Single(box => AutomationProperties.GetName(box) == "Ship");
        Assert.Equal("Cobra MkIII", pick.SelectedItem);
        Assert.Empty(ships.Plans.Store.Builds);

        Press(surface.Panel.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Plan this hull"));

        var build = Assert.Single(ships.Plans.Store.Builds);
        Assert.Equal("Cobra MkIII", build.HullName);
        Assert.Equal(LoadoutPages.ShipPrefix + build.Id, surface.Panel.Nav.Trail[^1].Key);

        surface.Panel.Tab = PanelTab.Stories;
        surface.Panel.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();
        ReadChapter(surface);

        Assert.NotNull(Ship(surface.Panel, "Show the Cobra MkIII plan"));
        Assert.Null(Ship(surface.Panel, "Plan a Cobra MkIII"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void CancellingThePickerPlansNothing()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var ships = Fleet("d47-story-ship-cancel");
        var surface = Open(TriggerKind.Board, ships, Hull);
        ReadChapter(surface);

        Press(Ship(surface.Panel, "Plan a Cobra MkIII")!);
        Assert.True(surface.Panel.GoBack());
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(ships.Plans.Store.Builds);
        Assert.NotNull(Ship(surface.Panel, "Plan a Cobra MkIII"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheShowButtonNamesTheShipTheCommanderOwns()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Board, Fleet("d47-story-ship-show"), Hull, OwnedCobra);
        ReadChapter(surface);
        Save(surface.Window, "story-ship-objective-show-chapter.png");

        var show = Assert.IsAssignableFrom<Button>(Ship(surface.Panel, "Show my Cobra MkIII"));
        Assert.True(show.Bounds.Height >= 44);
        Assert.NotNull(Pressable(surface.Panel, "Never ask this"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheStripDrawsTheButtonUnderTheObjective()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Board, Fleet("d47-story-ship-strip"), Hull);
        Save(surface.Window, "story-ship-objective-plan-strip.png");

        var plan = Assert.IsAssignableFrom<Button>(Ship(surface.Panel, "Plan a Cobra MkIII"));
        var objective = Assert.IsAssignableFrom<TextBlock>(Starting(surface.Panel, "Board"));

        Assert.True(Bottom(objective, surface.Window) <= Top(plan, surface.Window));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AnObjectiveThatIsNotBoardingHasNoShipButton()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive, Fleet("d47-story-ship-none"), Hull);
        ReadChapter(surface);

        Assert.Null(Ship(surface.Panel, "Plan a Cobra MkIII"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetDrawsBothButtonsWithAPressableTarget()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        foreach (var (fleet, label, capture) in new[]
        {
            ((IReadOnlyList<FleetEntry>?)null, "Plan a Cobra MkIII", "story-ship-objective-plan-headset.png"),
            (OwnedCobra, "Show my Cobra MkIII", "story-ship-objective-show-headset.png"),
        })
        {
            var ships = Fleet("d47-story-ship-headset");
            var (settings, _, _) = TestSurface.Create();
            settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

            using var vr = new VrPanelSurface(
                new PanelViewModel(),
                settings,
                _ => null,
                checklists: ships.Checklists,
                ships: ships.Plans,
                gameState: () => null,
                adventures: Build(TriggerKind.Board, ships, Hull, fleet));

            vr.Nav.Select(PanelTab.Stories);
            vr.Nav.SelectRoot(StoriesView.RootKey);
            Dispatcher.UIThread.RunJobs();
            vr.Board.Render();
            Dispatcher.UIThread.RunJobs();
            vr.Board.Render();

            var button = Assert.IsAssignableFrom<Button>(Ship(vr.Board.View, label));
            Assert.True(button.Bounds.Height >= 44);

            vr.Board.Render().SaveCapture(capture);
        }
    }

    [AvaloniaFact]
    public void TheEngineerButtonOpensThatEngineersPage()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Engineer, Fleet("d47-story-engineer"), engineer: "Felicity Farseer", engineers: true);
        ReadChapter(surface);
        Save(surface.Window, "story-engineer-objective-chapter.png");

        var show = Assert.IsAssignableFrom<Button>(Ship(surface.Panel, "Show Felicity Farseer"));
        Assert.True(show.Bounds.Height >= 44);

        Press(show);

        Assert.Equal(PanelTab.Assets, surface.Panel.Tab);
        Assert.StartsWith(EngineersPages.WhoPrefix, surface.Panel.Nav.Trail[^1].Key);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AButtonWhosePageIsNotRegisteredIsNotDrawn()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Engineer, Fleet("d47-story-engineer-none"), engineer: "Felicity Farseer");
        ReadChapter(surface);

        Assert.Null(Ship(surface.Panel, "Show Felicity Farseer"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetDrawsTheEngineerButtonWithAPressableTarget()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var ships = Fleet("d47-story-engineer-headset");
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        using var vr = new VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            checklists: ships.Checklists,
            ships: ships.Plans,
            gameState: () => null,
            onFoot: ships.OnFoot,
            unlocks: ships.Unlocks,
            adventures: Build(TriggerKind.Engineer, ships, engineer: "Felicity Farseer"));

        vr.Nav.Select(PanelTab.Stories);
        vr.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();

        var button = Assert.IsAssignableFrom<Button>(Ship(vr.Board.View, "Show Felicity Farseer"));
        Assert.True(button.Bounds.Height >= 44);

        vr.Board.Render().SaveCapture("story-engineer-objective-headset.png");
    }
}
