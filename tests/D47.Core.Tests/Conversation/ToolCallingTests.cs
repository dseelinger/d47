using D47.Core.Capabilities;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The agentic half of tool calling: a tool_use reply is executed against the registry and fed
/// back as a tool_result, and the model gets another turn with the answer in hand.
/// <para>
/// This is what <c>SupportsToolCalls</c> was gated on. Until it existed, every registered tool
/// was reachable only through the model-free routes, so a tool taking arguments the Commander
/// spoke — "how far is Colonia" — had no caller at all (architecture.md, open question 5).
/// </para>
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

    private static TurnLoop Build(CapabilityRegistry registry, ILlmProvider provider)
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
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
        // A tool_result with no matching tool_use above it is a protocol error, not a
        // recoverable one, so the pairing is asserted rather than assumed.
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
        // A capability failing is a state, not a crash — and the model can usually say something
        // true about the failure, which is better than d47 going quiet.
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

    [Fact]
    public async Task ARunawayToolLoopStopsAtTheCeilingAndStillAnswers()
    {
        // The failure this prevents is a billed request per round, forever. Reaching the ceiling
        // has to produce an answer rather than a cutoff, so the last round ships no tools and
        // the model is left with nothing to do but talk.
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
        Assert.Equal(3, spy.Calls.Count);
        Assert.Empty(provider.Requests[3].Prompt.Tools);
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

        // The prose is still readable straight off the transcript: tool blocks are how an answer
        // was reached, not part of what was said.
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
        // Advertising a tool the loop would drop is worse than not offering it: the model then
        // tells the Commander it has done something that never happened.
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
        // An eight-call lookup priced as a single question is the number the Commander is least
        // able to check for themselves.
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
