using Avalonia;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Characters;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>A panel with every tab furnished from empty services and its conditional roots shown, for tests that read its destinations.</summary>
internal sealed class PanelPathFixture : IDisposable
{
    private readonly SettingsHost _host;

    private PanelPathFixture(SettingsHost host) => _host = host;

    public PanelView Panel => _host.Panel;

    public static PanelPathFixture Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(TestSurface.Settings().Current.Ui.Theme);

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);
        var panel = host.Panel;

        var root = TestSurface.MemoryFolder("d47-panel-paths");
        var memory = () => new MemoryFileSystem();
        CommanderGameState? game = null;
        Func<CommanderGameState?> state = () => game;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), memory(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), memory(), NullLogger<ChecklistProposalStore>.Instance),
            state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), memory(), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), memory(), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, state);
        var onFoot = new OnFootPlanService(kit, checklists, state);
        var characterStore = new CharacterStore(Path.Combine(root, "characters.json"), memory(), NullLogger<CharacterStore>.Instance);
        var characters = new CharactersHost(
            () => characterStore,
            settings,
            _ => null,
            (_, _, _) => Task.FromResult<double?>(null));

        panel.EnableLoadout(
            ships, checklists, state, onFoot,
            construction: new ConstructionSurface(state, () => DateTimeOffset.UtcNow, null, null, () => true),
            characters: characters);
        panel.EnableEngineers(new EngineerPlanService(builds, kit, checklists, state), ships, state, onFoot, checklists: checklists);
        panel.EnableChecklist(checklists);
        panel.EnableRouting(new RoutingSurface(() => new NavRoute(), () => "Sol", CapabilityRegistry.Build([]), Plans: null, () => true));

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(root, "adventures.json"), memory(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, state,
            () => null, () => null, null, null, NullLogger.Instance);

        panel.EnableAdventures(new AdventureSurface(
            book, generator, state, () => "F1", () => DateTimeOffset.UtcNow, _ => { }, () => false, () => false, () => null, () => { }));

        var now = () => DateTimeOffset.UtcNow;

        panel.EnableMissions(state, now);
        panel.EnableStanding(state);
        panel.EnableStatistics(state);
        panel.EnableSession(state);
        panel.EnableBgsWork(state, () => null, now);
        panel.EnableCombatStats(state, () => HistoryState.Done, now);
        panel.EnableCredits(state, () => HistoryState.Done, now);
        panel.EnableCodex(state, () => HistoryState.Done, now);
        panel.EnableRawJournal();
        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(), () => new JournalLocation("Sol", null, false, null), () => true, () => { }));

        // Roots the window shows only while the Commander owns a carrier or is in a story.
        panel.Nav.SetVisible(PanelTab.Assets, LoadoutPages.CarrierRoot, true);
        panel.Nav.SetVisible(PanelTab.Stories, AdventuresPage.AskKey, true);
        panel.Nav.SetVisible(PanelTab.Stories, MessagesView.RootKey, true);

        Dispatcher.UIThread.RunJobs();
        return new PanelPathFixture(host);
    }

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    public void Dispose() => _host.Close();
}
