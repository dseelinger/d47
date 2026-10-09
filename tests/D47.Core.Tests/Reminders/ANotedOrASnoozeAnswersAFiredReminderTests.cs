using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Reminders;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>"Noted" and "remind me next time" answer the reminder that has just gone off, and only then (#644).</summary>
[Trait("Category", "Integration")]
public class ANotedOrASnoozeAnswersAFiredReminderTests
{
    private const string Commander = "F100";

    private static (TurnLoop Loop, FakeLlmProvider Model) Build(ReminderBench bench)
    {
        var registry = CapabilityRegistry.Build(
            [RemindersCapability.Create(bench.Store, () => Commander, () => ReminderBench.Now)]);
        var model = FakeLlmProvider.Answering("From the model.");

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            model,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return (loop, model);
    }

    private static async Task<string> SaidAsync(TurnLoop loop, string input)
    {
        var text = new System.Text.StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
        }

        return text.ToString();
    }

    private static JournalEvent Docked(string station = "Jameson Memorial") =>
        ReminderBench.Event("Docked", ("StationName", station));

    private static CommanderGameState Flying() => new(new CommanderIdentity(Commander, "Fixture"));

    [Theory]
    [InlineData("noted")]
    [InlineData("Got it.")]
    [InlineData("thanks")]
    public async Task NotedRemovesTheReminderThatWentOff(string answer)
    {
        using var bench = new ReminderBench();
        bench.Arm(Commander, JournalTrigger.NextDocking, "buy limpets");
        Assert.Single(bench.Say(Flying(), events: Docked()));
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, answer);

        Assert.Equal("Noted.", said);
        Assert.Empty(bench.Store.For(Commander));
        Assert.Empty(bench.Open().For(Commander));
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task RemindMeNextTimeFiresAgainAtTheSecondDocking()
    {
        using var bench = new ReminderBench();
        var reminder = bench.Arm(Commander, JournalTrigger.NextDocking, "buy limpets");
        var state = Flying();
        Assert.Single(bench.Say(state, events: Docked()));
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "remind me next time");

        Assert.Equal("I'll remind you to buy limpets when you next dock.", said);
        Assert.Equal(JournalReminderState.Armed, Assert.Single(bench.Store.For(Commander)).State);
        Assert.Equal(0, model.CallCount);

        var again = Assert.Single(bench.Say(state, events: Docked()));

        Assert.Equal(JournalReminderCallout.KeyPrefix + reminder.Id, again.Key);
        Assert.EndsWith("buy limpets", again.Heard, StringComparison.Ordinal);
        Assert.Equal(JournalReminderState.Fired, Assert.Single(bench.Store.For(Commander)).State);
    }

    [Theory]
    [InlineData("remind me tomorrow")]
    [InlineData("remind me next session")]
    public async Task RemindMeTomorrowMovesTheReminderToTheNextSession(string answer)
    {
        using var bench = new ReminderBench();
        bench.Arm(Commander, JournalTrigger.DockingAt, "sell data", "Jameson Memorial");
        Assert.Single(bench.Say(Flying(), events: Docked()));
        var (loop, _) = Build(bench);

        var said = await SaidAsync(loop, answer);

        var moved = Assert.Single(bench.Open().For(Commander));
        Assert.Equal(JournalTrigger.NextSession, moved.Trigger);
        Assert.Null(moved.Argument);
        Assert.Equal(JournalReminderState.Armed, moved.State);
        Assert.Equal("I'll remind you to sell data at the start of your next session.", said);
    }

    [Fact]
    public async Task OnlyTheMostRecentlyFiredReminderIsAnswered()
    {
        using var bench = new ReminderBench();
        var first = bench.Arm(Commander, JournalTrigger.NextDocking, "buy limpets");
        var second = bench.Arm(Commander, JournalTrigger.DockingAt, "sell data", "Jameson Memorial");
        var state = Flying();

        Assert.Single(bench.Say(state, events: Docked("Other Station")));
        Assert.Single(bench.Say(state, events: Docked()));
        var (loop, _) = Build(bench);

        await SaidAsync(loop, "noted");

        Assert.Equal(first.Id, Assert.Single(bench.Store.For(Commander)).Id);
        Assert.NotEqual(second.Id, bench.Store.For(Commander)[0].Id);
    }

    [Theory]
    [InlineData("noted")]
    [InlineData("remind me next time")]
    public async Task WithNothingFiredTheAnswerIsNotMatched(string answer)
    {
        using var bench = new ReminderBench();
        bench.Arm(Commander, JournalTrigger.NextDocking, "buy limpets");
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, answer);

        Assert.Equal("From the model.", said);
        Assert.Equal(1, model.CallCount);
        Assert.Equal(JournalReminderState.Armed, Assert.Single(bench.Store.For(Commander)).State);
    }

    [Fact]
    public async Task RemindMeTomorrowWithNothingFiredIsStillDeclinedAsATime()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "remind me tomorrow");

        Assert.Equal(JournalReminderPhrase.NoClock, said);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public void TheAnswersAreRefusedToTheModel()
    {
        using var bench = new ReminderBench();
        var tools = RemindersCapability.Create(bench.Store, () => Commander, () => ReminderBench.Now).Tools;

        Assert.True(tools.Single(tool => tool.Name == RemindersCapability.AcknowledgeTool).Protected);
        Assert.True(tools.Single(tool => tool.Name == RemindersCapability.SnoozeTool).Protected);
    }
}
