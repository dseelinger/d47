using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Retry with backoff, and saying so out loud when it runs out (list.md Phase 5, "Say
/// something when a turn is taking too long").
/// </summary>
public class TurnRetryTests
{
    private static TurnLoop Build(
        TempInstall install,
        ILlmProvider provider,
        InstantClock clock,
        RetryPolicy retry)
    {
        var registry = TestSurface.For(install).Registry;

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: clock);

        loop.Retry = retry;
        return loop;
    }

    private static async Task<(TurnResult Result, List<TurnEvent> Events)> RunAsync(TurnLoop loop)
    {
        var events = new List<TurnEvent>();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(
                           "tell me about hyperspace physics", TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);

            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        return (result!, events);
    }

    [Fact]
    public async Task ATransientFailureIsTriedAgainOnTheConfiguredSchedule()
    {
        using var install = new TempInstall();
        var clock = new InstantClock();
        var provider = new FakeLlmProvider(new LlmStreamEvent.Failed("Overloaded.", Transient: true));

        var loop = Build(install, provider, clock, new RetryPolicy
        {
            Attempts = 3,
            Wait = TimeSpan.FromSeconds(2),
            Backoff = BackoffShape.Sequential,
        });

        var (result, events) = await RunAsync(loop);

        Assert.Equal(3, provider.CallCount);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], clock.Waited);
        Assert.Equal(TurnOutcome.Failed, result.Outcome);

        var retries = events.OfType<TurnEvent.Retrying>().ToList();
        Assert.Equal(2, retries.Count);
        Assert.Equal(2, retries[0].Attempt);
        Assert.Equal(3, retries[0].Of);
    }

    /// <summary>
    /// A configuration failure fails identically next time, so retrying it only spends the
    /// Commander's silence.
    /// </summary>
    [Fact]
    public async Task AConfigurationFailureIsNotRetried()
    {
        using var install = new TempInstall();
        var clock = new InstantClock();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.Failed("That model does not exist.", Transient: false));

        var loop = Build(install, provider, clock, new RetryPolicy { Attempts = 4 });

        await RunAsync(loop);

        Assert.Equal(1, provider.CallCount);
        Assert.Empty(clock.Waited);
    }

    /// <summary>
    /// The asymmetry that shapes the whole design: streamed text has probably already been
    /// spoken, and there is no such thing as un-saying it.
    /// </summary>
    [Fact]
    public async Task AnAttemptThatAlreadySpokeIsNeverRetried()
    {
        using var install = new TempInstall();
        var clock = new InstantClock();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("You are in Sol. "),
            new LlmStreamEvent.Failed("Connection reset.", Transient: true));

        var loop = Build(install, provider, clock, new RetryPolicy { Attempts = 4 });

        var (result, _) = await RunAsync(loop);

        Assert.Equal(1, provider.CallCount);
        Assert.Empty(clock.Waited);
        Assert.Equal(TurnOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task RunningOutOfTriesIsSaidOutLoudRatherThanLeavingSilence()
    {
        using var install = new TempInstall();
        var clock = new InstantClock();
        var provider = new FakeLlmProvider(new LlmStreamEvent.Failed("Overloaded.", Transient: true));

        var loop = Build(install, provider, clock, new RetryPolicy { Attempts = 3 });

        var (result, _) = await RunAsync(loop);

        // The text is what the speech pipeline speaks, so "said out loud" and "produced text"
        // are the same claim at this layer.
        Assert.Contains("3 tries", result.Text, StringComparison.Ordinal);
        Assert.Contains("Overloaded.", result.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A provider that hangs produces no events at all — not an error, just a turn that never
    /// ends. That is the worst thing this app can do, because it is indistinguishable from
    /// having been ignored.
    /// </summary>
    [Fact]
    public async Task AProviderThatNeverAnswersBecomesAFailureRatherThanAHang()
    {
        using var install = new TempInstall();
        var clock = new InstantClock { TimeOutImmediately = true };
        var provider = new FakeLlmProvider(new LlmStreamEvent.TextDelta("this never arrives"));

        var loop = Build(install, provider, clock, new RetryPolicy
        {
            Attempts = 2,
            AttemptTimeout = TimeSpan.FromSeconds(30),
        });

        var (result, _) = await RunAsync(loop);

        Assert.Equal(TurnOutcome.Failed, result.Outcome);
        Assert.Contains("30 seconds", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProviderThatThrowsIsAFailedTurnRatherThanACrash()
    {
        using var install = new TempInstall();
        var clock = new InstantClock();
        var provider = new ThrowingLlmProvider();

        var loop = Build(install, provider, clock, new RetryPolicy { Attempts = 2 });

        var (result, _) = await RunAsync(loop);

        Assert.Equal(TurnOutcome.Failed, result.Outcome);
        Assert.Contains("the socket went away", result.Text, StringComparison.Ordinal);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task ARecoveryOnTheSecondTryAnswersNormallyAndCostsNothingExtra()
    {
        using var install = new TempInstall();
        var clock = new InstantClock();
        var provider = new FlakyLlmProvider(failuresBeforeSuccess: 1);

        var loop = Build(install, provider, clock, new RetryPolicy { Attempts = 3 });

        var (result, _) = await RunAsync(loop);

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Equal("Jump range is 12.5 light years.", result.Text);
        Assert.Single(clock.Waited);
    }
}
