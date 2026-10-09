using D47.Core.Journal;
using D47.Core.Reminders;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>Hold and material reminders fire when their condition becomes true, and never before the hold is read.</summary>
public class AHoldReminderWaitsForTheHoldTests
{
    private static CommanderGameState WithCapacity(int capacity)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(ReminderBench.Parse(
            $$"""{"timestamp":"2026-10-05T19:00:00Z","event":"Loadout","Ship":"Python","CargoCapacity":{{capacity}}}"""));
        return state;
    }

    private static CargoHold Holding(int tonnes) => new()
    {
        Vessel = "Ship",
        Count = tonnes,
        Items = tonnes == 0 ? [] : [new CargoItem("gold", tonnes)],
        ReadAt = ReminderBench.Now,
    };

    [Fact]
    public void HoldRemindersAreSilentBeforeTheHoldHasBeenRead()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.HoldEmpty, "Go back for more.");
        bench.Arm("F1", JournalTrigger.HoldFull, "Go and sell.");
        var state = WithCapacity(64);

        Assert.Empty(bench.Say(state));
        Assert.Empty(bench.Say(state));

        // The first reading is a baseline, not a change.
        state.Hold = Holding(0);
        Assert.Empty(bench.Say(state));
    }

    [Fact]
    public void AnSrvManifestIsNotTheShipsHold()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.HoldEmpty, "Go back for more.");
        var state = WithCapacity(64);

        state.Hold = Holding(10) with { Vessel = "SRV" };
        Assert.Empty(bench.Say(state));

        state.Hold = Holding(0) with { Vessel = "SRV" };
        Assert.Empty(bench.Say(state));
    }

    [Fact]
    public void AHoldFullReminderFiresWhenTheHoldFills()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.HoldFull, "Go and sell.");
        var state = WithCapacity(64);

        state.Hold = Holding(10);
        Assert.Empty(bench.Say(state));

        state.Hold = Holding(64);
        Assert.EndsWith("Go and sell.", Assert.Single(bench.Say(state)).Heard, StringComparison.Ordinal);
        Assert.Empty(bench.Say(state));
    }

    [Fact]
    public void AHoldEmptyReminderFiresWhenTheHoldEmpties()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.HoldEmpty, "Go back for more.");
        var state = WithCapacity(64);

        state.Hold = Holding(30);
        Assert.Empty(bench.Say(state));

        state.Hold = Holding(0);
        Assert.Single(bench.Say(state));
    }

    [Fact]
    public void AMaterialReminderFiresWhenTheMaterialReachesCapacity()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.MaterialFull, "Trade the surplus.", "iron");
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var capacity = MaterialGrades.CapacityOf("iron")!.Value;

        Assert.Empty(bench.Say(state, events: Materials(capacity - 1)));

        var said = Assert.Single(bench.Say(state, events: Materials(capacity)));
        Assert.EndsWith("Trade the surplus.", said.Heard, StringComparison.Ordinal);
    }

    private static JournalEvent Materials(int iron) => ReminderBench.Parse(
        $$"""{"timestamp":"2026-10-05T20:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":{{iron}}}],"Manufactured":[],"Encoded":[]}""");
}
