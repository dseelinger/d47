using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

public sealed class RecentDeliveryNotesTests
{
    [Fact]
    public void OnlyTheLastSixNotesAreKept()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "[a] one [b] two [c] three [d] four");
        delivery.Record("ship", "[e] five [f] six [g] seven");

        Assert.Equal(
            "Delivery notes this speaker used recently: b, c, d, e, f, g. Choose a different one, or none.",
            delivery.Describe("ship"));
    }

    [Fact]
    public void SpeakersAreKeptApart()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "[flatly] Noted.");
        delivery.Record("tower", "[curtly] Cleared.");

        Assert.Contains("flatly", delivery.Describe("ship"));
        Assert.DoesNotContain("curtly", delivery.Describe("ship"));
    }

    [Fact]
    public void ASpeakerWithNoNotesHasNothingDescribed()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "Noted.");

        Assert.Null(delivery.Describe("ship"));
        Assert.Null(delivery.Describe("nobody"));
    }

    [Fact]
    public void TheRecentNotesFollowTheHumorInTheTrailingState()
    {
        var prompt = new PromptAssembly { LiveGameState = "State.", Humor = "Be funny.", RecentDelivery = "Notes." };

        Assert.Equal("State.\n\nBe funny.\n\nNotes.", prompt.TrailingState);
        Assert.Null(new PromptAssembly().TrailingState);
        Assert.Equal("Notes.", new PromptAssembly { RecentDelivery = "Notes." }.TrailingState);
    }

    [Fact]
    public async Task ANoteInOneReplyReachesTheNextRequestForThatSpeaker()
    {
        var provider = new EchoingProvider("[flatly] Docked.");
        var delivery = new RecentDelivery();

        for (var line = 0; line < 2; line++)
        {
            var said = await FlavourTurn.AskAsync(
                provider, null, "Persona", null, "Say it.", null, null, null, null,
                cancellationToken: TestContext.Current.CancellationToken,
                canBeDirected: true,
                recentDelivery: delivery.Describe("ship"));
            delivery.Record("ship", said!);
        }

        Assert.Null(provider.Trailing[0]);
        Assert.Contains("flatly", provider.Trailing[1]);
    }

    [Fact]
    public async Task AVoiceThatCannotBeDirectedIsNotToldTheNotes()
    {
        var provider = new EchoingProvider("Docked.");

        await FlavourTurn.AskAsync(
            provider, null, "Persona", null, "Say it.", null, null, null, null,
            cancellationToken: TestContext.Current.CancellationToken,
            canBeDirected: false,
            recentDelivery: "Notes.");

        Assert.Null(provider.Trailing[0]);
    }

    private sealed class EchoingProvider(string reply) : ILlmProvider
    {
        public List<string?> Trailing { get; } = [];

        public string Id => "echo";

        public string DisplayName => "Echo";

        public string DefaultModel => "echo";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = false,
            SupportsOperatorSystemMessages = false,
            MinimumCacheablePrefixTokens = 0,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Trailing.Add(request.Prompt.TrailingState);
            yield return new LlmStreamEvent.TextDelta(reply);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
            await Task.CompletedTask;
        }
    }
}
