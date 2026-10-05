using D47.Core.Journal;
using D47.Core.Reminders;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>Station, system and carrier reminders fire where they were asked to, and nowhere else.</summary>
public class AReminderMatchesItsPlaceTests
{
    private const long CarrierId = 3700000000L;

    private static CommanderGameState Fresh() => new(new CommanderIdentity("F1", "Fixture"));

    private static JournalEvent AtTheCarrier() => ReminderBench.Event(
        "Docked", ("StationName", "K7Q-B4X"), ("StationType", "FleetCarrier"), ("MarketID", CarrierId));

    [Fact]
    public void AnArrivalReminderFiresInTheNamedSystemAndNotAnother()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.ArrivalIn, "Sell the painite.", "Shinrarta Dezhra");
        var state = Fresh();

        Assert.Empty(bench.Say(state, events: ReminderBench.Event("FSDJump", ("StarSystem", "Sol"))));

        var said = Assert.Single(bench.Say(state, events: ReminderBench.Event("FSDJump", ("StarSystem", "shinrarta dezhra"))));
        Assert.Contains("Shinrarta Dezhra", said.Text, StringComparison.Ordinal);
        Assert.EndsWith("Sell the painite.", said.Heard, StringComparison.Ordinal);
    }

    [Fact]
    public void AnArrivalReminderMatchesTheWholeNameOnly()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.ArrivalIn, "Sell the painite.", "Sol");

        Assert.Empty(bench.Say(Fresh(), events: ReminderBench.Event("FSDJump", ("StarSystem", "Solati"))));
    }

    [Fact]
    public void ADockingAtReminderFiresAtTheNamedStationOnly()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.DockingAt, "Hand in the bounties.", "Jameson Memorial");
        var state = Fresh();

        Assert.Empty(bench.Say(state, events: ReminderBench.Event("Docked", ("StationName", "Abraham Lincoln"))));
        Assert.Single(bench.Say(state, events: ReminderBench.Event("Docked", ("StationName", "JAMESON MEMORIAL"))));
    }

    [Fact]
    public void AnOwnCarrierReminderFiresAtTheCommandersOwnCarrier()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.OwnCarrier, "Load the tritium.");
        var state = Fresh();
        state.Carrier = state.Carrier with { CallSign = "K7Q-B4X", CarrierId = CarrierId, IsSquadron = false };

        Assert.Single(bench.Say(state, events: AtTheCarrier()));
        Assert.True(state.Carrier.DockedAtOwnCarrier);
    }

    [Fact]
    public void AnOwnCarrierReminderDoesNotFireDockingAtASquadronCarrier()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.OwnCarrier, "Load the tritium.");
        var state = Fresh();
        var dock = AtTheCarrier();
        state.Apply(dock);

        // Docked aboard it, as far as the fold says, so only the squadron flag stands in the way.
        state.Carrier = state.Carrier with { CallSign = "K7Q-B4X", CarrierId = CarrierId, IsSquadron = true, DockedAtOwnCarrier = true };

        Assert.Empty(bench.Callout.Examine(new D47.Core.Callouts.CalloutContext(
            ReminderBench.Now, false, state, GameStatus.Unknown, NavRoute.None, [dock])));
    }

    [Fact]
    public void AnOwnCarrierReminderDoesNotFireDockingAtAStation()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.OwnCarrier, "Load the tritium.");
        var state = Fresh();
        state.Carrier = state.Carrier with { CallSign = "K7Q-B4X", CarrierId = CarrierId, IsSquadron = false };

        Assert.Empty(bench.Say(state, events: ReminderBench.Event("Docked", ("StationName", "Jameson Memorial"))));
    }
}
