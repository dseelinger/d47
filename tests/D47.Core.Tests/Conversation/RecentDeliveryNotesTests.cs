using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

public sealed class RecentDeliveryNotesTests
{
    [Fact]
    public void OnlyTheLastTwelveDistinctWordsAreKept()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "[a1] x [b1] x [c1] x [d1] x [e1] x [f1] x [g1] x [h1] x");
        delivery.Record("ship", "[i1] x [j1] x [k1] x [l1] x [m1] x [n1] x [o1] x");

        Assert.Equal(
            "Delivery words this speaker used recently: d1, e1, f1, g1, h1, i1, j1, k1, l1, m1, n1, o1. Use none of them in a delivery note this time.",
            delivery.Describe("ship"));
    }

    [Fact]
    public void VariantsOfOneToneAreOneWord()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "[dry, deadpan] Noted.");
        delivery.Record("ship", "[dryly] Noted.");

        Assert.Equal(
            "Delivery words this speaker used recently: deadpan, dry. Use none of them in a delivery note this time.",
            delivery.Describe("ship"));
    }

    [Fact]
    public void FillerWordsAreNotRemembered()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "[chuckles] Noted.");
        delivery.Record("ship", "[almost approving, a very slight tone] Noted.");

        Assert.Contains("chuckles", delivery.Describe("ship"));
        Assert.Contains("approving, slight", delivery.Describe("ship"));
        Assert.DoesNotContain("almost", delivery.Describe("ship"));
        Assert.DoesNotContain("tone", delivery.Describe("ship"));
    }

    [Fact]
    public void SpeakersAreKeptApart()
    {
        var delivery = new RecentDelivery();

        delivery.Record("ship", "[flatly] Noted.");
        delivery.Record("tower", "[curtly] Cleared.");

        Assert.Contains("flat", delivery.Describe("ship"));
        Assert.DoesNotContain("curt", delivery.Describe("ship"));
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
        Assert.Contains("flat", provider.Trailing[1]);
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
