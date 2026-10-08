using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A tool result's picture reaches the model on the turn that asked for it, and no later turn.</summary>
public class APictureIsSentForOneTurnOnlyTests
{
    private const string Question = "what's on my scanner";

    private const string ResultText = "A picture of the Commander's screen.";

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03];

    private static TurnLoop Build(ILlmProvider provider, RecordingLogger<TurnLoop>? logger = null)
    {
        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "screen",
                Group = "Knowledge",
                Name = "Screen",
                Summary = "Looks at the screen.",
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "look_at_screen",
                        Description = "A picture of the Commander's screen.",
                        ReturnsImage = true,
                        Handler = (_, _) => Task.FromResult(ToolResult.Ok(ResultText) with
                        {
                            Image = new ImageAttachment(Jpeg, "image/jpeg", 1280, 720),
                        }),
                    },
                    new ToolDefinition
                    {
                        Name = "get_fuel",
                        Description = "How much fuel is aboard.",
                        Handler = (_, _) => Task.FromResult(ToolResult.Ok("Eight tonnes.")),
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
            logger ?? new RecordingLogger<TurnLoop>(),
            provider,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task RunAsync(TurnLoop loop, string input)
    {
        await foreach (var _ in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
        }
    }

    private static IEnumerable<ConversationContent.ToolResult> Results(IEnumerable<ConversationMessage> history) =>
        history.SelectMany(message => message.Content).OfType<ConversationContent.ToolResult>();

    [Fact]
    public async Task TheModelReadsThePictureOnItsTurnAndTheSettledHistoryKeepsOnlyTheText()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", "look_at_screen", "{}"),
            RoundScriptedLlmProvider.Saying("A Sidewinder, two kilometres out."),
            RoundScriptedLlmProvider.Saying("Still a Sidewinder."));

        var loop = Build(provider);

        await RunAsync(loop, Question);

        var sent = Assert.Single(Results(provider.Requests[1].Prompt.History));
        Assert.Same(Jpeg, sent.Image?.Data);

        var kept = Assert.Single(Results(loop.History));
        Assert.Null(kept.Image);
        Assert.Equal(ResultText, kept.Content);

        await RunAsync(loop, "and now");

        Assert.All(Results(provider.Requests[2].Prompt.History), result => Assert.Null(result.Image));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AToolThatReturnsAPictureIsOfferedOnlyWhereTheModelReadsOne(bool images, bool toolSearch)
    {
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Nothing."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            ToolSearch = toolSearch,
            Images = images,
        };

        await RunAsync(Build(provider), Question);

        var offered = provider.LastRequest!.Prompt.Tools.Select(tool => tool.Name).ToList();

        Assert.Contains("get_fuel", offered);
        Assert.Equal(images, offered.Contains("look_at_screen"));
    }

    [Fact]
    public async Task ARoundRefusedForSizeDropsEarlierTurnsAndKeepsThePicture()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Hello, Commander."),
            RoundScriptedLlmProvider.Calling("call_1", "look_at_screen", "{}"),
            [new LlmStreamEvent.Failed("The prompt is too long.", Transient: false) { ContextExceeded = true }],
            RoundScriptedLlmProvider.Saying("A Sidewinder."));

        var loop = Build(provider);

        await RunAsync(loop, "hello");
        await RunAsync(loop, Question);

        var retried = provider.Requests[3].Prompt.History;

        Assert.DoesNotContain(retried, message => message.Text.Contains("hello", StringComparison.Ordinal));
        Assert.Same(Jpeg, Assert.Single(Results(retried)).Image?.Data);
    }

    [Fact]
    public async Task ThePictureNeverReachesTheLog()
    {
        var logger = new RecordingLogger<TurnLoop>();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", "look_at_screen", "{}"),
            [new LlmStreamEvent.Failed("Refused.", Transient: false) { ContextExceeded = true }],
            RoundScriptedLlmProvider.Saying("A Sidewinder."));

        await RunAsync(Build(provider, logger), Question);

        var base64 = Convert.ToBase64String(Jpeg);

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains(base64, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("System.Byte", StringComparison.Ordinal));
    }
}
