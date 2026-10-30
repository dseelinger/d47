using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Help;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Help on Asset Mgmt › Carrier opens the carrier page, not the tab's help (#1886).</summary>
public class F1OnTheCarrierPageOpensTheCarrierHelpTests
{
    private const string Stats = """
        {"event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier",
         "Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","AllowNotorious":false,
         "FuelLevel":792,"JumpRangeCurr":500.0,"PendingDecommission":false,
         "SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},
         "Finance":{"CarrierBalance":750352669},"Crew":[]}
        """;

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void HelpOpensTheCarrierCapabilityPage()
    {
        var root = TestSurface.MemoryFolder("d47-carrier-help-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var state = new CommanderGameState(new CommanderIdentity("F735466", "TEST"));
        var stats = JsonDocument.Parse(Stats).RootElement;
        state.Apply(new JournalEvent(DateTimeOffset.UtcNow, "CarrierStats", stats));
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, () => state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        panel.Nav.SelectRoot(PanelTab.Assets, LoadoutPages.CarrierRoot);
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.OpenHelp());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(HelpLevel.Prefix + "carrier", panel.Nav.Trail[^1].Key);

        window.Close();
    }
}
