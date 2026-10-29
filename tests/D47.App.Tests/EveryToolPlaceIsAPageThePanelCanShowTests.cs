using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Coverage;
using D47.App.Panel;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every tool the Features page can send to a page names a tool that exists and a page the panel has.</summary>
[Trait("Category", "Gate")]
public sealed class EveryToolPlaceIsAPageThePanelCanShowTests
{
    [AvaloniaFact]
    public void EveryToolPlaceNamesARegisteredToolAndAShowablePage()
    {
        var (settings, _, paths, registry, _) = TestSurface.CreateFull(coverage: () => "x");
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var panel = Furnished(settings, paths, registry);
        var tools = registry.All.SelectMany(capability => capability.Descriptor.Tools).Select(tool => tool.Name).ToHashSet();

        var unknown = ToolPlaces.All.Keys.Where(name => !tools.Contains(name)).ToList();
        var unshowable = ToolPlaces.All.Where(place => !panel.Nav.CanShow(place.Value)).Select(place => place.Key).ToList();

        Assert.True(unknown.Count == 0, "Not in the registry: " + string.Join(", ", unknown));
        Assert.True(unshowable.Count == 0, "Page not on the panel: " + string.Join(", ", unshowable));
    }

    private static PanelView Furnished(SettingsService settings, AppPaths paths, CapabilityRegistry registry)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var fs = new MemoryFileSystem();

        panel.EnableChecklist(new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), fs, NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(paths.Data, "checklist-proposals.json"), fs, NullLogger<ChecklistProposalStore>.Instance),
            () => null));

        panel.EnableRouting(new RoutingSurface(() => new NavRoute(), () => null, registry, Bookmarks: new BookmarkStore(
            Path.Combine(paths.Data, "bookmarks.json"), fs, NullLogger<BookmarkStore>.Instance)));

        panel.EnableStarSystem(new StarSystemSurface(
            new NoSystems(),
            () => new JournalLocation("Sol", null, false, null),
            () => true,
            () => { })
        {
            Searches = new GalaxySearchSurface(
                registry,
                new GalaxySearchBoard(),
                new SavedSearchStore("saved-searches.json", fs, NullLogger<SavedSearchStore>.Instance),
                () => "F1",
                () => []),
        });

        var window = new Window { Content = panel, Width = 1100, Height = 900 };
        window.Show();

        return panel;
    }

    private sealed class NoSystems : IStarSystemService
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
}
