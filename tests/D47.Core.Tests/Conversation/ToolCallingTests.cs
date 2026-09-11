using D47.Core.Capabilities;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The agentic half of tool calling: a tool_use reply is executed against the registry and fed back as
/// a tool_result, and the model gets another turn with the answer in hand.
/// </summary>
public class ToolCallingTests
{
    /// <summary>Records what it was called with, so a test can assert the arguments arrived.</summary>
    private sealed class SpyTool
    {
        public List<ToolArguments> Calls { get; } = [];

        public ToolResult Result { get; set; } = ToolResult.Ok("Sol is 0 light years away.");

        public CapabilityDescriptor Describe(string toolName = "look_up_distance") => new()
        {
            Id = "galaxy",
            Group = "Knowledge",
            Name = "Galaxy",
            Summary = "Looks things up in the galaxy.",
            Tools =
            [
                new ToolDefinition
                {
                    Name = toolName,
                    Description = "How far one system is from another.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "system",
                            Type = ToolParameterType.String,
                            Description = "The system to measure to.",
                            Required = true,
                        },
                        new ToolParameter
                        {
                            Name = "round_to",
                            Type = ToolParameterType.Integer,
                            Description = "Decimal places.",
                        },
                    ],
                    Handler = (arguments, _) =>
                    {
                        Calls.Add(arguments);
                        return Task.FromResult(Result);
                    },
                },
            ],
        };
    }

    private static TurnLoop Build(CapabilityRegistry registry, ILlmProvider provider, ILogger<TurnLoop>? logger = null)
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            logger ?? NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<(TurnResult Result, List<TurnEvent> Events)> RunAsync(TurnLoop loop, string input)
    {
        var events = new List<TurnEvent>();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);

            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return (result, events);
    }

    [Fact]
    public async Task AToolTheModelAsksForIsRunAndItsAnswerComesBack()
    {
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", "look_up_distance", """{"system":"Colonia"}"""),
            RoundScriptedLlmProvider.Saying("Colonia is a long way out."));

        var (result, _) = await RunAsync(Build(registry, provider), "how far is Colonia");

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Equal("Colonia is a long way out.", result.Text);

        // Two requests, not one: the second is the model being shown what came back.
        Assert.Equal(2, provider.CallCount);
        var call = Assert.Single(spy.Calls);
        Assert.True(call.TryGetString("system", out var system));
        Assert.Equal("Colonia", system);
    }

    [Fact]
    public async Task TheToolResultIsSentBackPairedWithTheCallThatAskedForIt()
    {
        // A tool_result with no matching tool_use above it is a protocol error, not a recoverable one, so the
        // pairing is asserted rather than assumed.
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_7", "look_up_distance", """{"system":"Sol"}"""),
            RoundScriptedLlmProvider.Saying("Right here."));

        await RunAsync(Build(registry, provider), "how far is Sol");

        var second = provider.Requests[1].Prompt.History;

        var asked = second
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolUse>()
            .Single();

        var answered = second
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .Single();

        Assert.Equal("call_7", asked.Id);
        Assert.Equal(asked.Id, answered.ToolUseId);
        Assert.Equal("Sol is 0 light years away.", answered.Content);
        Assert.False(answered.IsError);
    }

    [Fact]
    public async Task ATypedArgumentSurvivesTheTripThroughJson()
    {
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("c", "look_up_distance", """{"system":"Beagle Point","round_to":2}"""),
            RoundScriptedLlmProvider.Saying("Far."));

        await RunAsync(Build(registry, provider), "distance to Beagle Point");

        var call = Assert.Single(spy.Calls);
        Assert.True(call.TryGetInt32("round_to", out var places));
        Assert.Equal(2, places);
    }

    [Fact]
    public async Task AToolThatFailsIsReportedToTheModelRatherThanEndingTheTurn()
    {
        // A capability failing is a state, not a crash — and the model can usually say something true about
        // the failure, which is better than d47 going quiet.
        var spy = new SpyTool { Result = ToolResult.Error("The galaxy service did not answer.") };
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("c", "look_up_distance", """{"system":"Colonia"}"""),
            RoundScriptedLlmProvider.Saying("I couldn't reach the galaxy service just then."));

        var (result, events) = await RunAsync(Build(registry, provider), "how far is Colonia");

        Assert.Equal(TurnOutcome.Answered, result.Outcome);

        var finished = events.OfType<TurnEvent.ToolFinished>().Single();
        Assert.False(finished.Succeeded);

        var sentBack = provider.Requests[1].Prompt.History
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .Single();

        Assert.True(sentBack.IsError);
    }

    /// <summary>A failed call's arguments are unrecoverable after the fact, so the log line carries them (#36).</summary>
    [Fact]
    public async Task AFailedCallsArgumentsAreInTheLogLine()
    {
        var spy = new SpyTool { Result = ToolResult.Error("The galaxy service did not answer.") };
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("c", "look_up_distance", """{"system":"Shinrata Desra"}"""),
            RoundScriptedLlmProvider.Saying("I couldn't reach the galaxy service just then."));

        var logger = new RecordingLogger<TurnLoop>();

        await RunAsync(Build(registry, provider, logger), "how far is Shinrata Desra");

        Assert.Contains(
            logger.Entries,
            entry => entry.Message.Contains("error", StringComparison.Ordinal)
                     && entry.Message.Contains("""{"system":"Shinrata Desra"}""", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARunawayToolLoopStopsAtTheCeilingAndStillAnswers()
    {
        // The failure this prevents is a billed request per round, forever.
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        IReadOnlyList<LlmStreamEvent>[] alwaysCalling =
        [
            .. Enumerable
                .Range(0, 3)
                .Select(i => RoundScriptedLlmProvider.Calling($"c{i}", "look_up_distance", """{"system":"Sol"}""")),
            RoundScriptedLlmProvider.Saying("I'll stop looking and answer: Sol is right here."),
        ];

        var provider = new RoundScriptedLlmProvider(alwaysCalling);
        var loop = Build(registry, provider);
        loop.MaxToolRounds = 3;

        var (result, _) = await RunAsync(loop, "how far is Sol");

        Assert.Equal(TurnOutcome.Answered, result.Outcome);

        // Three rounds that could call, then one that could not.
        Assert.Equal(4, provider.CallCount);

        // The same call with the same arguments three times over: only the first is actually run (#87).
        Assert.Single(spy.Calls);
        Assert.Empty(provider.Requests[3].Prompt.Tools);
    }

    [Fact]
    public async Task ASecondIdenticalCallInOneTurnIsNotRunAgain()
    {
        // The scenario in #87: the same tool with the same arguments, asked for repeatedly inside one turn
        // and refused the same way every time. Only the first attempt should reach the capability, because
        // what it does once — speak an announcement — must not happen five times.
        var spy = new SpyTool { Result = ToolResult.Error("Refused: the game is not online.") };
        var registry = CapabilityRegistry.Build([spy.Describe("plot_course")]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("c1", "plot_course", """{"system":"Kamitra"}"""),
            RoundScriptedLlmProvider.Calling("c2", "plot_course", """{"system":"Kamitra"}"""),
            RoundScriptedLlmProvider.Saying("I could not plot that."));

        var loop = Build(registry, provider);
        var (result, events) = await RunAsync(loop, "plot a course to Kamitra");

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Single(spy.Calls);

        var finishes = events.OfType<TurnEvent.ToolFinished>().ToList();
        Assert.Equal(2, finishes.Count);
        Assert.All(finishes, finished => Assert.False(finished.Succeeded));

        var secondResult = provider.Requests[2].Prompt.History
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .Last();

        Assert.Contains("Already tried once this turn", secondResult.Content);
    }

    [Fact]
    public async Task TwoDifferentToolsInOneTurnAreBothRun()
    {
        // The dedup in #87 is keyed on the call, not the turn: a second, different tool is not caught by it.
        var spy = new SpyTool();
        var otherSpy = new SpyTool();
        var descriptor = spy.Describe("look_up_distance") with
        {
            Tools = [.. spy.Describe("look_up_distance").Tools, .. otherSpy.Describe("plot_course").Tools],
        };
        var registry = CapabilityRegistry.Build([descriptor]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("c1", "look_up_distance", """{"system":"Sol"}"""),
            RoundScriptedLlmProvider.Calling("c2", "plot_course", """{"system":"Sol"}"""),
            RoundScriptedLlmProvider.Saying("Done."));

        await RunAsync(Build(registry, provider), "look up Sol then plot to it");

        Assert.Single(spy.Calls);
        Assert.Single(otherSpy.Calls);
    }

    [Fact]
    public async Task TextFromASecondRoundDoesNotRunOntoTheFirst()
    {
        // The other half of #87: two rounds that both speak before the turn ends must not be joined into
        // one unspaced sentence when read together.
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            [
                new LlmStreamEvent.TextDelta("Kamitra's on the clipboard."),
                new LlmStreamEvent.ToolUse("c1", "look_up_distance", """{"system":"Kamitra"}"""),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.ToolUse),
            ],
            RoundScriptedLlmProvider.Saying("Course is in for the plotting."));

        var (_, events) = await RunAsync(Build(registry, provider), "plot a course to Kamitra");

        var spoken = string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text));

        Assert.Contains("clipboard. Course is in", spoken);
    }

    [Fact]
    public async Task TheToolRoundsAreRememberedSoTheNextTurnAccountsForThem()
    {
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", "look_up_distance", """{"system":"Colonia"}"""),
            RoundScriptedLlmProvider.Saying("About 22,000 light years."));

        var loop = Build(registry, provider);
        await RunAsync(loop, "how far is Colonia");

        Assert.Contains(
            loop.History.SelectMany(message => message.Content).OfType<ConversationContent.ToolUse>(),
            call => call.Name == "look_up_distance");

        Assert.Contains(
            loop.History.SelectMany(message => message.Content).OfType<ConversationContent.ToolResult>(),
            result => result.ToolUseId == "call_1");

        // The prose is still readable straight off the transcript: tool blocks are how an answer was reached,
        // not part of what was said.
        Assert.Equal("About 22,000 light years.", loop.History[^1].Text);
    }

    [Fact]
    public async Task AFailedTurnCommitsNothingIncludingItsToolRounds()
    {
        // Half an exchange, ending in a call nobody answered, is worse than no memory of it.
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", "look_up_distance", """{"system":"Colonia"}"""),
            [new LlmStreamEvent.Failed("Overloaded.", Transient: false)]);

        var loop = Build(registry, provider);
        var (result, _) = await RunAsync(loop, "how far is Colonia");

        Assert.Equal(TurnOutcome.Failed, result.Outcome);
        Assert.Empty(loop.History);
    }

    [Fact]
    public async Task NoToolIsAdvertisedToAProviderThatCannotExecuteOne()
    {
        // Advertising a tool the loop would drop is worse than not offering it: the model then tells the
        // Commander it has done something that never happened.
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("I can't look that up."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = false,
        };

        await RunAsync(Build(registry, provider), "how far is Colonia");

        Assert.NotNull(provider.LastRequest);
        Assert.Empty(provider.LastRequest.Prompt.Tools);
    }

    [Fact]
    public async Task ToolsAreAdvertisedToAProviderThatCanExecuteThem()
    {
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Looking."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
        };

        await RunAsync(Build(registry, provider), "how far is Colonia");

        Assert.NotNull(provider.LastRequest);
        Assert.Contains(provider.LastRequest.Prompt.Tools, tool => tool.Name == "look_up_distance");
    }

    [Fact]
    public async Task EveryRoundIsBilledNotJustTheLastOne()
    {
        // An eight-call lookup priced as a single question is the number the Commander is least able to check
        // for themselves.
        var spy = new SpyTool();
        var registry = CapabilityRegistry.Build([spy.Describe()]);

        var provider = new RoundScriptedLlmProvider(
            [
                new LlmStreamEvent.ToolUse("c", "look_up_distance", """{"system":"Sol"}"""),
                new LlmStreamEvent.Completed(new LlmUsage(100, 20, 0, 0), LlmStopReason.ToolUse),
            ],
            RoundScriptedLlmProvider.Saying("Right here.", new LlmUsage(300, 40, 0, 0)));

        var (result, _) = await RunAsync(Build(registry, provider), "how far is Sol");

        Assert.NotNull(result.Cost);
        Assert.Equal(400, result.Cost.Usage.InputTokens);
        Assert.Equal(60, result.Cost.Usage.OutputTokens);
    }
}
