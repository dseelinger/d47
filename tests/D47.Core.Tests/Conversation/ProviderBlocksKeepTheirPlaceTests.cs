using D47.Core.Capabilities;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A provider block is carried through a turn and its history in the place it arrived.</summary>
public class ProviderBlocksKeepTheirPlaceTests
{
    private const string SearchCall =
        """{"type":"server_tool_use","id":"srvtoolu_1","name":"tool_search_tool_regex","input":{"query":"distance"}}""";

    private static TurnLoop Build(ILlmProvider provider)
    {
        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "galaxy",
                Group = "Knowledge",
                Name = "Galaxy",
                Summary = "Looks things up in the galaxy.",
                Tools =
                [
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
            },
        ]);

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

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        var events = new List<TurnEvent>();

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    [Fact]
    public async Task TheAssistantMessageKeepsTextBlocksAndCallsInArrivalOrder()
    {
        var provider = new RoundScriptedLlmProvider(
            [
                new LlmStreamEvent.TextDelta("Checking the index, "),
                new LlmStreamEvent.Opaque(SearchCall),
                new LlmStreamEvent.TextDelta("then the distance."),
                new LlmStreamEvent.ToolUse("c1", "look_up_distance", """{"system":"Colonia"}"""),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.ToolUse),
            ],
            RoundScriptedLlmProvider.Saying("About 22,000 light years."));

        var events = await RunAsync(Build(provider), "how far is Colonia");

        var asked = provider.Requests[1].Prompt.History[^2];

        Assert.Equal(ConversationRole.Assistant, asked.Role);
        Assert.Collection(
            asked.Content,
            part => Assert.Equal(new ConversationContent.Text("Checking the index,"), part),
            part => Assert.Equal(new ConversationContent.Opaque("anthropic", SearchCall), part),
            part => Assert.Equal(new ConversationContent.Text("then the distance."), part),
            part => Assert.IsType<ConversationContent.ToolUse>(part));

        var spoken = string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text));

        Assert.StartsWith("Checking the index, then the distance.", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("server_tool_use", spoken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AClosingRoundWithAProviderBlockIsCommittedWhole()
    {
        var provider = new RoundScriptedLlmProvider(
        [
            new LlmStreamEvent.TextDelta("Searching. "),
            new LlmStreamEvent.Opaque(SearchCall),
            new LlmStreamEvent.TextDelta("Nothing needed."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed),
        ]);

        var loop = Build(provider);
        await RunAsync(loop, "how far is Colonia");

        Assert.Collection(
            loop.History[^1].Content,
            part => Assert.Equal(new ConversationContent.Text("Searching."), part),
            part => Assert.IsType<ConversationContent.Opaque>(part),
            part => Assert.Equal(new ConversationContent.Text("Nothing needed."), part));
    }

    [Fact]
    public async Task TrimmingDoesNotOpenTheTranscriptOnAProviderBlock()
    {
        ConversationMessage Plain(int index) => new(
            index % 2 == 0 ? ConversationRole.User : ConversationRole.Assistant,
            $"line {index.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        ConversationMessage WithBlock() => new(
            ConversationRole.Assistant,
            [new ConversationContent.Text("Searching."), new ConversationContent.Opaque("anthropic", SearchCall)]);

        // One answered turn adds two messages, so the cut falls on index 2.
        var transcript = Enumerable.Range(0, TurnLoop.TranscriptKept).Select(Plain).ToList();
        var atTheCut = WithBlock();
        var later = WithBlock();
        transcript[2] = atTheCut;
        transcript[41] = later;

        var loop = Build(FakeLlmProvider.Answering("Answered."));
        loop.UseTranscript(transcript);

        await RunAsync(loop, "how far is Colonia");

        Assert.DoesNotContain(loop.History, message => ReferenceEquals(message, atTheCut));
        Assert.DoesNotContain(loop.History[0].Content, part => part is ConversationContent.Opaque);

        var kept = Assert.Single(loop.History, message => message.Content.Any(part => part is ConversationContent.Opaque));
        Assert.Same(later, kept);
        Assert.Equal(2, kept.Content.Count);
    }
}
