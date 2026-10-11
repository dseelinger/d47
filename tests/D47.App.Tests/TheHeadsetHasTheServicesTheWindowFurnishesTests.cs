using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Seats;
using D47.Core.Ships;
using D47.Core.Storage;
using D47.Vr;
using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The headset panel is furnished with the phrases, story desk, crew seats and build talk the window has (#1092).</summary>
[Trait("Category", "Integration")]
public class TheHeadsetHasTheServicesTheWindowFurnishesTests
{
    private const string Journal =
        """{"timestamp":"2026-10-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private const string Loadout =
        """{"timestamp":"2026-10-10T09:00:01Z","event":"Loadout","Ship":"anaconda","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[{"Slot":"MainEngines","Item":"int_engine_size6_class5","On":true,"Priority":0,"Health":1.0}]}""";

    private sealed record Start(D47.Core.Configuration.SettingsService Settings, D47.Core.AppPaths Paths, CommanderGameState Flying);

    private static Start Begin()
    {
        var (settings, _, paths) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, "full", D47.Core.Configuration.SettingsCaller.Panel);
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var store = new GameStateStore();

        foreach (var line in new[] { Journal, Loadout })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return new Start(settings, paths, store.Active!);
    }

    private static ChecklistService Checklists(string folder, Func<CommanderGameState?> state) =>
        new(
            new ChecklistStore(Path.Combine(folder, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(folder, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            state);

    private static void Redraw(VrPanelSurface panel)
    {
        Dispatcher.UIThread.RunJobs();
        panel.Invalidate();
        var (width, height) = panel.Size;
        var pixels = new VrPixels(width, height);
        panel.Draw(pixels.Address, pixels.RowBytes);
        Dispatcher.UIThread.RunJobs();
    }

    private static VrPanelSurface Settings(bool store)
    {
        var start = Begin();
        var phrases = new LearnedPhrasesStore(
            Path.Combine(start.Paths.Data, "phrases.json"), new MemoryFileSystem(), NullLogger<LearnedPhrasesStore>.Instance);

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            start.Settings,
            _ => null,
            settingsPage: () => new TextBlock { Text = "Settings" },
            gameState: () => start.Flying,
            capabilities: CapabilityRegistry.Build([InterfaceCapability.Create()]),
            learnedPhrases: store ? phrases : null);

        Dispatcher.UIThread.RunJobs();
        panel.Nav.Select(PanelTab.Settings);

        return panel;
    }

    [AvaloniaFact]
    public void SettingsOnTheHeadsetShowsPhrases() =>
        Assert.True(Settings(store: true).Nav.SelectRoot(PanelTab.Settings, PhrasesPage.RootKey));

    [AvaloniaFact]
    public void SettingsOnTheHeadsetHasNoPhrasesWithoutTheStore() =>
        Assert.False(Settings(store: false).Nav.SelectRoot(PanelTab.Settings, PhrasesPage.RootKey));

    [AvaloniaFact]
    public void ADraftStoryIsActedOnFromTheHeadsetDesk()
    {
        var start = Begin();
        var surface = AdventureFixture.Surface();
        var draft = AdventuresTabTests.Story(null, AdventureSource.Generated) with { Key = "one", Name = "one" };
        Assert.Null(surface.Book.Write("F1", draft));

        var desk = new AdventureCapability.AdventureDesk();

        using var panel = new VrPanelSurface(new PanelViewModel(), start.Settings, _ => null, adventures: surface, adventureDesk: desk);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(desk.Reject());
        Assert.Null(surface.Book.Standing("F1", "one"));
    }

    private static VrPanelSurface Fleet(bool crew, bool talk)
    {
        var start = Begin();
        Func<CommanderGameState?> state = () => start.Flying;
        var checklists = Checklists(start.Paths.Data, state);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(start.Paths.Data, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            state);

        var seats = new CrewSeatStore(
            Path.Combine(start.Paths.Data, "crew-seats.json"), new MemoryFileSystem(), NullLogger<CrewSeatStore>.Instance);

        var host = new CrewSeatsHost(
            () => seats,
            () => [("am_michael", "Michael"), ("af_heart", "Heart")],
            () => "kokoro",
            () => ["Ava"]);

        var buildTalk = new BuildTalk(
            ships,
            (_, _, _, _) => Task.FromResult(new BuildAdvice(BuildRemarkKind.Goal, "Nothing changes.", [], null, [])),
            () => new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));

        var panel = new VrPanelSurface(
            new PanelViewModel(),
            start.Settings,
            _ => null,
            checklists: checklists,
            ships: ships,
            gameState: state,
            modulePower: () => ModulePower.None,
            hullArt: () => false,
            crewSeats: crew ? host : null,
            buildTalk: talk ? buildTalk : null);

        Dispatcher.UIThread.RunJobs();
        panel.Nav.Select(PanelTab.Assets);

        return panel;
    }

    private static void AssertPressable(Control section) =>
        Assert.All(
            section.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible),
            button => Assert.True(button.Bounds.Height >= 44, $"{button.Name} is {button.Bounds.Height}px tall."));

    [AvaloniaFact]
    public void FleetOnTheHeadsetShowsCrewSeatsAtPressableHeight()
    {
        var panel = Fleet(crew: true, talk: false);

        panel.Nav.GoTo(new NavCrumb(LoadoutPages.FleetRoot, "Fleet"), new NavCrumb(CrewPage.RootKey, "Crew"));
        Redraw(panel);

        var page = panel.Board.View.GetVisualDescendants().OfType<CrewPage>().Single();

        Assert.Contains(page.GetVisualDescendants().OfType<Button>(), button => button.Name == CrewSeatsSection.OfferName);
        AssertPressable(page);
    }

    [AvaloniaFact]
    public void FleetOnTheHeadsetHasNoSeatsWithoutTheHost()
    {
        var panel = Fleet(crew: false, talk: false);

        panel.Nav.GoTo(new NavCrumb(LoadoutPages.FleetRoot, "Fleet"), new NavCrumb(CrewPage.RootKey, "Crew"));
        Redraw(panel);

        Assert.DoesNotContain(
            panel.Board.View.GetVisualDescendants().OfType<Button>(), button => button.Name == CrewSeatsSection.OfferName);
    }

    [AvaloniaFact]
    public void ABuildOnTheHeadsetShowsItsTalkSectionAtPressableHeight()
    {
        var panel = Fleet(crew: false, talk: true);

        OpenShip(panel);

        AssertPressable(panel.Board.View.GetVisualDescendants().OfType<BuildTalkSection>().Single());
    }

    [AvaloniaFact]
    public void ABuildOnTheHeadsetHasNoTalkSectionWithoutTheTalk()
    {
        var panel = Fleet(crew: false, talk: false);

        OpenShip(panel);

        Assert.Empty(panel.Board.View.GetVisualDescendants().OfType<BuildTalkSection>());
    }

    private static void OpenShip(VrPanelSurface panel)
    {
        Redraw(panel);

        var card = panel.Board.View.GetVisualDescendants().OfType<Button>().First(button =>
            button.GetVisualDescendants().OfType<TextBlock>().Any(text =>
                (text.Text ?? string.Empty).Contains("Bad Idea", StringComparison.OrdinalIgnoreCase)));

        card.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Redraw(panel);
        Redraw(panel);
    }
}
