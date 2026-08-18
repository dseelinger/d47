using D47.Core.Capabilities;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Whether a turn asks for a web search, and what happens when one is cut short (Step 10).
/// <para>
/// The gate has two halves that fail for different reasons, and both are asserted: the
/// Commander's setting, and whether the endpoint can do it at all. Getting the second wrong is
/// not a privacy fault but a reliability one — a gateway that is asked for a server-side tool
/// it does not have rejects the whole request.
/// </para>
/// </summary>
public class WebSearchTurnTests
{
    private static TurnLoop Build(ILlmProvider provider, bool? webSearchEnabled)
    {
        var registry = CapabilityRegistry.Build([]);

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };

        if (webSearchEnabled is not null)
        {
            loop.WebSearchEnabled = () => webSearchEnabled.Value;
        }

        return loop;
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task TheSettingOffMeansTheTurnDoesNotAskToSearch()
    {
        var provider = FakeLlmProvider.Answering("Mining is popular.");

        await RunAsync(Build(provider, webSearchEnabled: false), "what is everyone mining?");

        Assert.False(provider.LastRequest!.WebSearch);
    }

    [Fact]
    public async Task TheSettingOnMeansTheTurnAsksToSearch()
    {
        var provider = FakeLlmProvider.Answering("Mining is popular.");

        await RunAsync(Build(provider, webSearchEnabled: true), "what is everyone mining?");

        Assert.True(provider.LastRequest!.WebSearch);
    }

    /// <summary>
    /// Never wired up at all is the same as off. Worth its own case because the delegate is
    /// optional and a null one defaulting to <c>true</c> would enable a network feature by
    /// forgetting to configure it.
    /// </summary>
    [Fact]
    public async Task AnUnwiredHostDoesNotSearch()
    {
        var provider = FakeLlmProvider.Answering("Mining is popular.");

        await RunAsync(Build(provider, webSearchEnabled: null), "what is everyone mining?");

        Assert.False(provider.LastRequest!.WebSearch);
    }

    /// <summary>
    /// The endpoint overrules the setting. Capabilities are state: the Commander asked for
    /// search, the endpoint has none, and the turn runs without it rather than failing.
    /// </summary>
    [Fact]
    public async Task AnEndpointWithoutWebSearchOverrulesTheSetting()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Mining is popular."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            WebSearch = false,
        };

        await RunAsync(Build(provider, webSearchEnabled: true), "what is everyone mining?");

        Assert.False(provider.LastRequest!.WebSearch);
    }

    /// <summary>
    /// A paused turn is text that stopped part-way. Reporting it as answered is the failure
    /// this catches: the reply reads like a finished sentence and nothing else says otherwise.
    /// </summary>
    [Fact]
    public async Task APausedTurnIsUnsureRatherThanAnswered()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Commanders are reporting that the best price is"),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Paused));

        var result = await RunAsync(Build(provider, webSearchEnabled: true), "what is painite selling for?");

        Assert.Equal(TurnOutcome.Unsure, result.Outcome);
    }

    /// <summary>
    /// A completed turn with the same text is answered, so the case above is pinned to the stop
    /// reason rather than to anything about the text.
    /// </summary>
    [Fact]
    public async Task TheSameTextCompletedIsAnswered()
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Commanders are reporting that the best price is"),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed));

        var result = await RunAsync(Build(provider, webSearchEnabled: true), "what is painite selling for?");

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
    }

    /// <summary>
    /// Searches are billed on top of tokens, at a penny each. A turn that searched twice and
    /// spent almost no tokens still costs two pence, and the running total has to say so.
    /// </summary>
    [Fact]
    public async Task SearchesAreCountedIntoTheTurnPrice()
    {
        var usage = new LlmUsage(100, 50, 0, 0) { WebSearchRequests = 2 };
        var provider = FakeLlmProvider.Answering("Painite is up.", usage);

        var result = await RunAsync(Build(provider, webSearchEnabled: true), "what is painite selling for?");

        // claude-opus-5 at $5/$25 per million: 100 in and 50 out comes to $0.00175. The two
        // searches are $0.02, which is more than ten times the whole rest of the turn.
        Assert.True(result.Cost!.Priced);
        Assert.Equal(0.02175m, result.Cost.Dollars);
    }

    /// <summary>The same usage without searches, so the figure above is attributable.</summary>
    [Fact]
    public async Task TheSameTokensWithoutASearchCostAlmostNothing()
    {
        var provider = FakeLlmProvider.Answering("Painite is up.", new LlmUsage(100, 50, 0, 0));

        var result = await RunAsync(Build(provider, webSearchEnabled: true), "what is painite selling for?");

        Assert.Equal(0.00175m, result.Cost!.Dollars);
    }

    /// <summary>
    /// Search working says nothing at all. The line is a per-turn cost at prompt position 7, so
    /// a Commander who has search on must not be billed for a sentence about a limit they do not
    /// have.
    /// </summary>
    [Fact]
    public void NothingIsSaidWhenSearchWorks()
    {
        Assert.Null(Capabilities.Builtin.ConversationCapability.LiveSearch(enabled: true, available: true));
    }

    /// <summary>
    /// The Commander's own switch, named as theirs, because the remedy is one toggle they own.
    /// </summary>
    [Fact]
    public void TheSettingBeingOffNamesTheSetting()
    {
        var line = Capabilities.Builtin.ConversationCapability.LiveSearch(enabled: false, available: true);

        Assert.NotNull(line);
        Assert.Contains("switched off", line, StringComparison.Ordinal);
        Assert.Contains("Settings", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// The endpoint half, which is not the Commander's doing and is not fixed by the toggle.
    /// </summary>
    [Fact]
    public void AnEndpointWithNoSearchNamesTheEndpoint()
    {
        var line = Capabilities.Builtin.ConversationCapability.LiveSearch(enabled: true, available: false);

        Assert.NotNull(line);
        Assert.Contains("endpoint", line, StringComparison.Ordinal);
        Assert.DoesNotContain("switched off", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The endpoint wins when both are off.</b> Telling a Commander to flip a switch that
    /// will not help is worse than saying nothing — they turn it on, nothing changes, and the
    /// explanation they were given is now the reason they distrust the next one.
    /// </summary>
    [Fact]
    public void TheEndpointWinsWhenBothAreOff()
    {
        var line = Capabilities.Builtin.ConversationCapability.LiveSearch(enabled: false, available: false);

        Assert.NotNull(line);
        Assert.Contains("endpoint", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Settings", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both lines have to be usable by a model that is about to answer a current-information
    /// question, so both say not to answer as though it had been checked.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EveryLineForbidsAnsweringAsThoughChecked(bool enabled, bool available)
    {
        var line = Capabilities.Builtin.ConversationCapability.LiveSearch(enabled, available);

        Assert.NotNull(line);
        Assert.Contains("as though it had been checked", line, StringComparison.Ordinal);
    }
}
