using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Reminders;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>Reminders survive a restart, filed under the Commander who set them.</summary>
public class RemindersAreKeptPerCommanderTests
{
    [Fact]
    public void AReminderSurvivesARestartAndAnotherCommandersIsNotFired()
    {
        var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.NextDocking, "Buy limpets.");
        bench.Arm("F2", JournalTrigger.NextDocking, "Sell the painite.");

        var restarted = bench.Open();
        var callout = new JournalReminderCallout(restarted);
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var dock = ReminderBench.Event("Docked", ("StationName", "Jameson Memorial"));
        state.Apply(dock);

        var said = Assert.Single(callout.Examine(new CalloutContext(
            ReminderBench.Now, false, state, GameStatus.Unknown, NavRoute.None, [dock])));

        Assert.EndsWith("Buy limpets.", said.Heard, StringComparison.Ordinal);
        Assert.Equal(JournalReminderState.Armed, Assert.Single(restarted.For("F2")).State);
        Assert.Equal(JournalReminderState.Fired, Assert.Single(bench.Open().For("F1")).State);
    }

    [Fact]
    public void AHandWrittenReminderWithNoNameToMatchIsRefused()
    {
        var bench = new ReminderBench();
        bench.Files.WriteText(ReminderBench.FilePath, """
            {
              "commanders": [
                { "frontierId": "F1", "reminders": [
                  { "sentence": "Sell the painite.", "trigger": "arrivalIn" },
                  { "sentence": "Buy limpets.", "trigger": "nextDocking" }
                ] }
              ]
            }
            """);

        var store = bench.Open();

        Assert.Equal("Buy limpets.", Assert.Single(store.For("F1")).Sentence);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void AReminderTheFileWouldRefuseIsNotAdded()
    {
        var bench = new ReminderBench();

        Assert.False(bench.Store.Add("F1", new JournalReminder("a", "Trade the surplus.", JournalTrigger.MaterialFull)));
        Assert.False(bench.Store.Add("F1", new JournalReminder("b", "  ", JournalTrigger.NextDocking)));
        Assert.Empty(bench.Store.For("F1"));
    }

    [Fact]
    public void TwoHandWrittenRemindersSharingAnIdKeepTheFirst()
    {
        var bench = new ReminderBench();
        bench.Files.WriteText(ReminderBench.FilePath, """
            { "commanders": [ { "frontierId": "F1", "reminders": [
              { "id": "x", "sentence": "Buy limpets.", "trigger": "nextDocking" },
              { "id": "x", "sentence": "Sell the painite.", "trigger": "nextDocking" }
            ] } ] }
            """);

        var store = bench.Open();

        Assert.Equal("Buy limpets.", Assert.Single(store.For("F1")).Sentence);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void AFileThatDoesNotParseIsNotWrittenOver()
    {
        var bench = new ReminderBench();
        const string Broken = """{ "commanders": [ { "frontierId": "F1", "reminders": [ { "trigger": "nextDockin" } ] } ] }""";
        bench.Files.WriteText(ReminderBench.FilePath, Broken);

        var store = bench.Open();

        Assert.False(store.Add("F1", new JournalReminder("a", "Buy limpets.", JournalTrigger.NextDocking)));
        Assert.Equal(Broken, bench.Files.ReadText(ReminderBench.FilePath));
    }
}
