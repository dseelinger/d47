using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Reminders;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>"Remind me to" is read by the grammar and reaches the store with no model call (#643).</summary>
[Trait("Category", "Integration")]
public class SayItAndTheReminderIsSetTests
{
    private const string Commander = "F100";

    private static (TurnLoop Loop, FakeLlmProvider Model) Build(ReminderBench bench, bool timers = false)
    {
        List<CapabilityDescriptor> descriptors = [RemindersCapability.Create(bench.Store, () => Commander, () => ReminderBench.Now)];

        if (timers)
        {
            descriptors.Add(UtilitiesCapability.Create());
        }

        var registry = CapabilityRegistry.Build(descriptors);
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

    [Fact]
    public async Task ANextDockingReminderIsStoredAndReadBack()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "Remind me to buy limpets when I next dock.");

        var stored = Assert.Single(bench.Store.For(Commander));
        Assert.Equal(JournalTrigger.NextDocking, stored.Trigger);
        Assert.Equal("buy limpets", stored.Sentence);
        Assert.Equal(ReminderBench.Now, stored.Set);
        Assert.Equal("I'll remind you to buy limpets when you next dock.", said);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task AnArrivalReminderKeepsTheSystemAsSaid()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "Remind me to sell data when I arrive in Sol");

        var stored = Assert.Single(bench.Store.For(Commander));
        Assert.Equal(JournalTrigger.ArrivalIn, stored.Trigger);
        Assert.Equal("Sol", stored.Argument);
        Assert.Equal("sell data", stored.Sentence);
        Assert.Equal("I'll remind you to sell data when you arrive in Sol.", said);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task AMaterialIsStoredByItsJournalName()
    {
        using var bench = new ReminderBench();
        var (loop, _) = Build(bench);

        var said = await SaidAsync(loop, "remind me to visit a trader when my arsenic is full");

        var stored = Assert.Single(bench.Store.For(Commander));
        Assert.Equal(JournalTrigger.MaterialFull, stored.Trigger);
        Assert.Equal("arsenic", stored.Argument);
        Assert.Equal("I'll remind you to visit a trader when your Arsenic is full.", said);
    }

    [Fact]
    public async Task AMomentTheGameCannotSeeIsDeclinedWithTheMomentsItCan()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "Remind me to eat when I'm bored");

        Assert.Equal(JournalReminderPhrase.NoTrigger, said);
        Assert.Contains("when you next dock", said, StringComparison.Ordinal);
        Assert.Empty(bench.Store.For(Commander));
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task ATimeIsDeclinedInARunWithoutTimers()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "Remind me in twenty minutes to check the carrier");

        Assert.Equal(JournalReminderPhrase.NoClock, said);
        Assert.Empty(bench.Store.For(Commander));
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task ATimeGoesToTheModelInARunWithTimers()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench, timers: true);

        var said = await SaidAsync(loop, "Remind me in twenty minutes to check the carrier");

        Assert.Equal("From the model.", said);
        Assert.Empty(bench.Store.For(Commander));
        Assert.Equal(1, model.CallCount);
    }

    [Fact]
    public async Task AReminderIsCancelledByItsWords()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        await SaidAsync(loop, "Remind me to buy limpets when I next dock");
        await SaidAsync(loop, "when I arrive in Sol, remind me to sell data");

        var said = await SaidAsync(loop, "cancel the reminder to buy limpets");

        Assert.Equal("Cancelled the reminder to buy limpets when you next dock.", said);
        Assert.Equal("sell data", Assert.Single(bench.Store.For(Commander)).Sentence);
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task AQuestionThatStartsWithRemindMeIsLeftToTheModel()
    {
        using var bench = new ReminderBench();
        var (loop, model) = Build(bench);

        var said = await SaidAsync(loop, "remind me what my rank is");

        Assert.Equal("From the model.", said);
        Assert.Equal(1, model.CallCount);
    }
}
