using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>A next-docking reminder fires at the first Docked of a replayed fixture, and not at the second.</summary>
[Trait("Category", "Integration")]
public class ANextDockingReminderFiresOnceTests
{
    private const string Commander = "F1000001";

    [Fact]
    public void ItFiresAtTheFirstDockingOnlyAndEndsInTheCommandersWords()
    {
        using var bench = new ReminderBench();
        var reminder = bench.Arm(Commander, JournalTrigger.NextDocking, "Buy limpets before you leave.");

        var lines = File.ReadAllLines(Path.Combine(FixturesDirectory(), "Journal.2026-02-10T090000.01.log"));
        var docked = lines.Single(line => line.Contains("\"event\":\"Docked\"", StringComparison.Ordinal));

        var gameState = new GameStateStore();
        var said = new List<(string Kind, Announcement Line)>();

        foreach (var line in lines.Append(docked))
        {
            if (!JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent))
            {
                continue;
            }

            gameState.Apply(journalEvent!);

            var context = new CalloutContext(
                ReminderBench.Now, false, gameState.Active, GameStatus.Unknown, NavRoute.None, [journalEvent!]);

            said.AddRange(bench.Callout.Examine(context).Select(announcement => (journalEvent!.Kind, announcement)));
        }

        var (kind, spoken) = Assert.Single(said);
        Assert.Equal("Docked", kind);
        Assert.Equal(JournalReminderCallout.KeyPrefix + reminder.Id, spoken.Key);
        Assert.EndsWith("Buy limpets before you leave.", spoken.Heard, StringComparison.Ordinal);
        Assert.Equal(JournalReminderState.Fired, Assert.Single(bench.Store.For(Commander)).State);
    }

    [Fact]
    public void NothingFiresWhilePriming()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.NextDocking, "Buy limpets.");

        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        Assert.Empty(bench.Say(state, priming: true, ReminderBench.Event("Docked", ("StationName", "Jameson Memorial"))));
        Assert.Equal(JournalReminderState.Armed, Assert.Single(bench.Store.For("F1")).State);
    }

    [Fact]
    public void AFiredReminderIsRemovedAtTheNextLoadGame()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.NextDocking, "Buy limpets.");
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        Assert.Single(bench.Say(state, events: ReminderBench.Event("Docked", ("StationName", "Jameson Memorial"))));
        Assert.Single(bench.Store.For("F1"));

        bench.Say(state, events: ReminderBench.Event("LoadGame", ("FID", "F1"), ("Commander", "Fixture")));

        Assert.Empty(bench.Store.For("F1"));
        Assert.Empty(bench.Open().For("F1"));
    }

    [Fact]
    public void ANextSessionReminderFiresAtALaterLoadGameAndSurvivesItsOwn()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.NextSession, "Check the Community Goal.");
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        var said = Assert.Single(bench.Say(state, events: ReminderBench.Event("LoadGame", ("FID", "F1"), ("Commander", "Fixture"))));

        Assert.EndsWith("Check the Community Goal.", said.Heard, StringComparison.Ordinal);
        Assert.Equal(JournalReminderState.Fired, Assert.Single(bench.Store.For("F1")).State);
    }

    [Fact]
    public void ANextSessionReminderFiresOnTheFirstLiveTickWhenTheSessionOpenedInTheBacklog()
    {
        using var bench = new ReminderBench();
        bench.Arm("F1", JournalTrigger.NextSession, "Check the Community Goal.");
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        Assert.Empty(bench.Say(state, priming: true, ReminderBench.Event("LoadGame", ("FID", "F1"), ("Commander", "Fixture"))));

        Assert.Single(bench.Say(state));
        Assert.Empty(bench.Say(state));
    }

    private static string FixturesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests", "fixtures", "journal");
    }
}
