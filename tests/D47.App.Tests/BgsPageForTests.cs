using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>A Commander › BGS page with no faction report, for tests that read its WORK section.</summary>
internal static class BgsPageForTests
{
    public static CampaignPage Page(CommanderGameState state, string faction, DateTimeOffset? tick, DateTimeOffset now, Func<string, Task<bool>>? copy = null)
    {
        var store = new CampaignStore("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);
        var sources = new CampaignSources(
            () => state,
            store,
            () => null,
            () => faction,
            () => tick,
            CapabilityRegistry.Build([CampaignCapability.Create(store, () => state)]))
        {
            Now = () => now,
        };

        return new CampaignPage(sources, copy: copy);
    }

    /// <summary>Presses WORK once the page is on screen.</summary>
    public static void ShowWork(CampaignPage page)
    {
        page.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == CampaignPage.SectionsName).PressOption(CampaignModel.WorkSection);
        Dispatcher.UIThread.RunJobs();
    }
}
