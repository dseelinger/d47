using System.Text.Json;
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
/// The carrier page on the Fleet tab
/// (<a href="https://github.com/dseelinger/d47/issues/230">#230</a>).
/// <para>
/// Driven through the real panel rather than by building the page alone: the shared page helpers
/// resolve theme resources off the running application, so a page built outside one throws before
/// it draws — and going the long way exercises the tab wiring as well as the drawing.
/// </para>
/// <para>
/// The event bodies are taken from the Commander's own journals rather than invented.
/// </para>
/// </summary>
public class TheCarrierPageDrawsWhatIsKnownTests
{
    private const string Stats = """
        {"event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier",
         "Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","AllowNotorious":false,
         "FuelLevel":792,"JumpRangeCurr":500.0,"PendingDecommission":false,
         "SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},
         "Finance":{"CarrierBalance":750352669},
         "Crew":[{"CrewRole":"BlackMarket","Activated":false},
                 {"CrewRole":"Refuel","Activated":true,"Enabled":true,"CrewName":"Rosa Guthrie"},
                 {"CrewRole":"Repair","Activated":true,"Enabled":false,"CrewName":"Ev Chang"}]}
        """;

    private static JournalEvent Event(string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(DateTimeOffset.UtcNow, root.GetProperty("event").GetString()!, root);
    }

    /// <summary>What the carrier page draws, for a Commander whose journal held these events.</summary>
    private static string Shown(params string[] events)
    {
        var root = TempFolders.Create("d47-carrier-page-tests");

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

        var state = new CommanderGameState(new CommanderIdentity("F735466", "TEST"));

        foreach (var json in events)
        {
            state.Apply(Event(json));
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };

        window.Show();

        panel.Tab = PanelTab.Loadout;
        panel.Nav.SelectRoot(PanelTab.Loadout, LoadoutPages.CarrierRoot);
        Dispatcher.UIThread.RunJobs();

        var text = string.Join(
            " | ",
            panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

        window.Close();

        return text;
    }

    [AvaloniaFact]
    public void ItNamesTheCarrierAndItsFigures()
    {
        var text = Shown(Stats);

        Assert.Contains("Sacred Fire (BNH-T2F)", text, StringComparison.Ordinal);
        Assert.Contains("792", text, StringComparison.Ordinal);
        Assert.Contains("500", text, StringComparison.Ordinal);
        Assert.Contains("23,530", text, StringComparison.Ordinal);
        Assert.Contains("750,352,669", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bought and switched off is said separately from open for business. It is the state a
    /// Commander can undo from the management panel, and the one they are most likely not to have
    /// meant.
    /// </summary>
    [AvaloniaFact]
    public void ASwitchedOffServiceIsNotListedAsOpen()
    {
        var text = Shown(Stats);

        Assert.Contains("Refuel", text, StringComparison.Ordinal);
        Assert.Contains("Switched off", text, StringComparison.Ordinal);
        Assert.Contains("Repair", text, StringComparison.Ordinal);

        // Never bought at all, so it is neither open nor switched off.
        Assert.DoesNotContain("Black market", text, StringComparison.Ordinal);
    }

    /// <summary>A booked jump says where it is going and where it will park.</summary>
    [AvaloniaFact]
    public void ABookedJumpSaysWhereAndWhere()
    {
        var departure = DateTimeOffset.UtcNow.AddMinutes(20).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var text = Shown(
            Stats,
            $$"""
            {"event":"CarrierJumpRequest","CarrierType":"FleetCarrier","CarrierID":3715429376,
             "SystemName":"Kuwemaki","Body":"Kuwemaki A 3","DepartureTime":"{{departure}}"}
            """);

        Assert.Contains("Kuwemaki, at Kuwemaki A 3", text, StringComparison.Ordinal);
        Assert.Contains("leaves in", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The figures say how old they are, because they only refresh when the Commander opens the
    /// management panel — saying them flat would be saying they are current.
    /// </summary>
    [AvaloniaFact]
    public void TheFiguresCarryTheirAge()
    {
        Assert.Contains("only refresh when you open", Shown(Stats), StringComparison.Ordinal);
    }

    /// <summary>
    /// The squadron's carrier is drawn under its own heading, and never mixed with the
    /// Commander's own.
    /// </summary>
    [AvaloniaFact]
    public void TheSquadronsCarrierIsDrawnApartAndSaidToBeTheirs()
    {
        var text = Shown(
            Stats,
            """
            {"event":"CarrierStats","CarrierID":3713474048,"CarrierType":"SquadronCarrier",
             "Callsign":"QRS-11X","Name":"Wandering Home","DockingAccess":"squadronfriends",
             "FuelLevel":140,"Finance":{"CarrierBalance":9999}}
            """);

        Assert.Contains("Sacred Fire (BNH-T2F)", text, StringComparison.Ordinal);
        Assert.Contains("Your squadron's carrier", text, StringComparison.Ordinal);
        Assert.Contains("Wandering Home (QRS-11X)", text, StringComparison.Ordinal);
        Assert.Contains("Your squadron's, not yours", text, StringComparison.Ordinal);

        // Its balance is deliberately not drawn: a figure the Commander cannot act on, level with
        // one they can, is a figure waiting to be misread.
        Assert.DoesNotContain("9,999", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Commander with no squadron carrier is told nothing about one. d47 cannot tell "no
    /// squadron" from "not seen yet", and saying either would be a claim it cannot support.
    /// </summary>
    [AvaloniaFact]
    public void NoSquadronCarrierMeansNoSquadronHeading()
    {
        Assert.DoesNotContain("squadron", Shown(Stats), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>"d47 has not seen one" is a different claim from "you have none"</b>, and only the first
    /// is something d47 can know before the management panel has ever been opened.
    /// </summary>
    [AvaloniaFact]
    public void ACommanderWithNoCarrierIsToldWhichItIs()
    {
        var text = Shown();

        Assert.Contains("No carrier has turned up in the journal yet", text, StringComparison.Ordinal);
        Assert.Contains("open its management panel", text, StringComparison.Ordinal);
    }
}
