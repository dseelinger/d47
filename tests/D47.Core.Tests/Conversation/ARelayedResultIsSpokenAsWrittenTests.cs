using D47.Core.Capabilities;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// A tool result marked relayed is spoken as the tool wrote it and ends the model turn, so the model is
/// never asked to report an action it has just taken (#111).
/// </summary>
public class ARelayedResultIsSpokenAsWrittenTests
{
    private const string Plotted = "Course plotted to Colonia, 22,000 light years.";

    private static CapabilityDescriptor Navigation() => new()
    {
        Id = "navigation",
        Group = "Flight",
        Name = "Navigation",
        Summary = "Plots courses.",
        Tools =
        [
            new ToolDefinition
            {
                Name = "plot_course",
                Description = "Plots a course to a system.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "system",
                        Type = ToolParameterType.String,
                        Description = "The system to plot to.",
                        Required = true,
                    },
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Relay(Plotted)),
            },
            new ToolDefinition
            {
                Name = "look_up_distance",
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
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok("Colonia is 22,000 light years away.")),
            },
        ],
    };

    private static TurnLoop Build(ILlmProvider provider, List<ConversationMessage>? transcript = null)
    {
        var registry = CapabilityRegistry.Build([Navigation()]);

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

        if (transcript is not null)
        {
            loop.UseTranscript(transcript);
        }

        return loop;
    }

    private static async Task<(TurnResult Result, string Spoken)> RunAsync(TurnLoop loop, string input)
    {
        var spoken = new System.Text.StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.TextDelta delta:
                    spoken.Append(delta.Text);
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        return (result, spoken.ToString());
    }

    /// <summary>Text, then the calls it announced, in one round.</summary>
    private static IReadOnlyList<LlmStreamEvent> SayingAndCalling(
        string text,
        params (string Id, string Tool, string InputJson)[] calls)
    {
        var events = new List<LlmStreamEvent> { new LlmStreamEvent.TextDelta(text) };
        events.AddRange(calls.Select(call => new LlmStreamEvent.ToolUse(call.Id, call.Tool, call.InputJson)));
        events.Add(new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.ToolUse));
        return events;
    }

    [Fact]
    public async Task TheModelIsNeverAskedForTheRoundThatWouldHaveReportedTheAction()
    {
        var provider = new RoundScriptedLlmProvider(
            SayingAndCalling("Plotting a course now.", ("call_1", "plot_course", """{"system":"Colonia"}""")),
            RoundScriptedLlmProvider.Saying("Confirm the spelling that comes up."));

        var (result, spoken) = await RunAsync(Build(provider), "plot a course to Colonia");

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Equal(TurnRoute.Model, result.Route);

        // One request, not two: the second round was scripted and never asked for.
        Assert.Single(provider.Requests);

        Assert.Equal($"Plotting a course now. {Plotted}", spoken);
        Assert.Equal($"Plotting a course now. {Plotted}", result.Text);
    }

    [Fact]
    public async Task ARelayedResultAlongsideAnUnrelayedOneStillEndsTheTurnWithBothInHistory()
    {
        var transcript = new List<ConversationMessage>();

        var provider = new RoundScriptedLlmProvider(
            SayingAndCalling(
                "Plotting a course now.",
                ("call_1", "plot_course", """{"system":"Colonia"}"""),
                ("call_2", "look_up_distance", """{"system":"Colonia"}""")),
            RoundScriptedLlmProvider.Saying("It is 22,000 light years."));

        var (result, _) = await RunAsync(Build(provider, transcript), "plot a course to Colonia");

        Assert.Single(provider.Requests);
        Assert.Equal($"Plotting a course now. {Plotted}", result.Text);

        var results = transcript
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(Plotted, results[0].Content);
        Assert.Equal("Colonia is 22,000 light years away.", results[1].Content);
    }

    [Fact]
    public async Task TheHistoryLeftBehindAlternatesAndCarriesNoStrandedToolCall()
    {
        var transcript = new List<ConversationMessage>();

        var provider = new RoundScriptedLlmProvider(
            SayingAndCalling("Plotting a course now.", ("call_1", "plot_course", """{"system":"Colonia"}""")),
            RoundScriptedLlmProvider.Saying("Never asked for."));

        await RunAsync(Build(provider, transcript), "plot a course to Colonia");

        AssertAlternates(transcript);
        AssertNoStrandedPair(transcript);

        // The next turn reads that history back and adds to it.
        var second = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying("Twenty-two thousand."));
        var (result, _) = await RunAsync(Build(second, transcript), "how far was that");

        Assert.Equal("Twenty-two thousand.", result.Text);
        Assert.Equal(ConversationRole.Assistant, Assert.Single(second.Requests).Prompt.History[^2].Role);

        AssertAlternates(transcript);
        AssertNoStrandedPair(transcript);
    }

    [Fact]
    public async Task TrimmingAHistoryThatEndsInARelayedTurnStrandsNoToolPair()
    {
        // Long enough that the turn's own messages push the transcript past the bound.
        var transcript = Enumerable
            .Range(0, TurnLoop.TranscriptKept)
            .Select(index => new ConversationMessage(
                index % 2 == 0 ? ConversationRole.User : ConversationRole.Assistant,
                $"filler {index}"))
            .ToList();

        var provider = new RoundScriptedLlmProvider(
            SayingAndCalling("Plotting a course now.", ("call_1", "plot_course", """{"system":"Colonia"}""")),
            RoundScriptedLlmProvider.Saying("Never asked for."));

        await RunAsync(Build(provider, transcript), "plot a course to Colonia");

        Assert.Equal(TurnLoop.TranscriptKept, transcript.Count);
        AssertNoStrandedPair(transcript);
    }

    private static void AssertAlternates(IReadOnlyList<ConversationMessage> transcript)
    {
        for (var index = 1; index < transcript.Count; index++)
        {
            Assert.NotEqual(transcript[index - 1].Role, transcript[index].Role);
        }
    }

    private static void AssertNoStrandedPair(IReadOnlyList<ConversationMessage> transcript)
    {
        var calls = transcript
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolUse>()
            .Select(call => call.Id)
            .ToHashSet(StringComparer.Ordinal);

        var answered = transcript
            .SelectMany(message => message.Content)
            .OfType<ConversationContent.ToolResult>()
            .Select(result => result.ToolUseId);

        Assert.All(answered, id => Assert.Contains(id, calls));
    }
}
