using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The tritium block on the carrier page: the tank, the hold, the ship, a total and a rough range
/// (#307).
/// </summary>
public class TheCarrierPageShowsTritiumAndRangeTests
{
    private const long CarrierId = 3715429376;
    private const string Sign = "BNH-T2F";

    private static string[] CarrierLines(int? intoHold = null, bool tradeOrderOpen = false)
    {
        var lines = new List<string>
        {
            """{"timestamp":"2026-09-13T16:00:00Z","event":"Commander","FID":"F735466","Name":"TEST"}""",
            $$"""{"timestamp":"2026-09-13T16:00:01Z","event":"CarrierBuy","CarrierID":{{CarrierId}},"Callsign":"{{Sign}}","Location":"Meene"}""",
            $$$"""{"timestamp":"2026-09-13T16:00:02Z","event":"CarrierStats","CarrierID":{{{CarrierId}}},"CarrierType":"FleetCarrier","Callsign":"{{{Sign}}}","Name":"Sacred Fire","DockingAccess":"all","FuelLevel":982,"JumpRangeCurr":500.0,"PendingDecommission":false,"SpaceUsage":{"TotalCapacity":25000,"Cargo":0,"FreeSpace":25000},"Finance":{"CarrierBalance":0} }""",
            $$"""{"timestamp":"2026-09-13T16:00:03Z","event":"Docked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{CarrierId}} }""",
        };

        if (intoHold is { } count)
        {
            lines.Add(
                $$"""{"timestamp":"2026-09-13T16:00:04Z","event":"CargoTransfer","Transfers":[{"Type":"tritium","Count":{{count}},"Direction":"tocarrier"}]}""");
        }

        if (tradeOrderOpen)
        {
            lines.Add(
                $$"""{"timestamp":"2026-09-13T16:00:05Z","event":"CarrierTradeOrder","CarrierID":{{CarrierId}},"Commodity":"tritium","CancelTrade":false}""");
        }

        return [.. lines];
    }

    private static void WriteCargo(string root, string vessel, int tritium)
    {
        var inventory = tritium > 0
            ? $$"""[{ "Name":"tritium", "Name_Localised":"Tritium", "Count":{{tritium}}, "Stolen":0 }]"""
            : "[]";

        File.WriteAllText(
            Path.Combine(root, CargoManifestReader.ManifestFile),
            $$"""{ "timestamp":"2026-09-13T16:00:06Z", "event":"Cargo", "Vessel":"{{vessel}}", "Count":{{tritium}}, "Inventory":{{inventory}} }""");
    }

    /// <summary>
    /// A Commander with a carrier, and the ship's hold read from a real <c>Cargo.json</c> — because,
    /// unlike everything else this page draws, the ship's hold cannot arrive any other way.
    /// </summary>
    private static (GameStateStore Store, JournalSpine Spine, string Root) Commander(
        int shipTritium = 0,
        string vessel = "Ship",
        int? carrierHoldTritium = null,
        bool tradeOrderOpen = false)
    {
        var root = TempFolders.Create("d47-carrier-tritium-tests");

        File.WriteAllLines(
            Path.Combine(root, "Journal.2026-09-13T160000.01.log"),
            CarrierLines(carrierHoldTritium, tradeOrderOpen));

        WriteCargo(root, vessel, shipTritium);

        var store = new GameStateStore();
        var spine = new JournalSpine(root, store, NullLoggerFactory.Instance);

        spine.Poll();

        return (store, spine, root);
    }

    /// <summary>The page drawn for a Commander whose state the panel reads live.</summary>
    private static (Window Window, PanelView Panel) Open(Func<CommanderGameState?> state)
    {
        var root = TempFolders.Create("d47-carrier-tritium-page");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };

        window.Show();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.SelectRoot(PanelTab.Loadout, LoadoutPages.CarrierRoot);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static string Text(PanelView panel) =>
        string.Join(" | ", panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

    [AvaloniaFact]
    public void ItShowsTheTankTheHoldTheShipAndATotal()
    {
        var (store, _, _) = Commander(shipTritium: 40, carrierHoldTritium: 158);
        var (window, panel) = Open(() => store.Active);

        var text = Text(panel);

        Assert.Contains("IN THE TANK", text, StringComparison.Ordinal);
        Assert.Contains("982", text, StringComparison.Ordinal);
        Assert.Contains("CARRIER'S HOLD", text, StringComparison.Ordinal);
        Assert.Contains("158 t (counted)", text, StringComparison.Ordinal);
        Assert.Contains("YOUR SHIP'S HOLD", text, StringComparison.Ordinal);
        Assert.Contains("40 t", text, StringComparison.Ordinal);
        Assert.Contains("TOTAL", text, StringComparison.Ordinal);
        Assert.Contains("1,180 t", text, StringComparison.Ordinal);
        Assert.Contains("RANGE, ROUGHLY", text, StringComparison.Ordinal);
        Assert.Contains("jump", text, StringComparison.Ordinal);
        Assert.Contains("at 500 ly", text, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void TheCarriersHoldRowIsOmittedUntilTritiumIsCounted()
    {
        var (store, _, _) = Commander();
        var (window, panel) = Open(() => store.Active);

        Assert.DoesNotContain("Carrier's hold", Text(panel), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void AnOpenTradeOrderMarksTheHoldsFigureAsMayBeOff()
    {
        var (store, _, _) = Commander(carrierHoldTritium: 158, tradeOrderOpen: true);
        var (window, panel) = Open(() => store.Active);

        Assert.Contains("may be off: a tritium order was open", Text(panel), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptyShipsHoldRowIsOmitted()
    {
        var (store, _, _) = Commander(shipTritium: 0);
        var (window, panel) = Open(() => store.Active);

        Assert.DoesNotContain("Your ship's hold", Text(panel), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>The SRV's hold is not the ship's, so its tritium is not counted here.</summary>
    [AvaloniaFact]
    public void TheSrvsHoldDoesNotCountAsTheShips()
    {
        var (store, _, _) = Commander(shipTritium: 40, vessel: "SRV");
        var (window, panel) = Open(() => store.Active);

        Assert.DoesNotContain("Your ship's hold", Text(panel), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>The page redraws for the ship's hold alone, not only the carrier (#307).</summary>
    [AvaloniaFact]
    public void ThePageRedrawsWhenTheShipsHoldChangesAlone()
    {
        var (store, spine, root) = Commander(shipTritium: 0);
        var (window, panel) = Open(() => store.Active);

        Assert.DoesNotContain("Your ship's hold", Text(panel), StringComparison.Ordinal);

        WriteCargo(root, "Ship", 40);
        File.SetLastWriteTimeUtc(
            Path.Combine(root, CargoManifestReader.ManifestFile), DateTime.UtcNow.AddSeconds(1));
        spine.Poll();

        Assert.True(panel.TickLoadout(), "a changed ship's hold is drawn");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("YOUR SHIP'S HOLD", Text(panel), StringComparison.Ordinal);

        window.Close();
    }
}
