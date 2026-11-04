using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.IllegalMissionsAreSteeredToAnarchyTests;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class TheFidelityCheckIsGivenTheAnarchySystemsTests
{
    private const string NothingObjected = """{"objections": []}""";

    private static async Task<string> FidelityPrompt(Galaxy galaxy)
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats("Dyson's Hollow")), Saying(NothingObjected));

        var outcome = await Generator(provider, galaxy).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        return provider.Requests[3].Prompt.History[0].Text;
    }

    [Fact]
    public async Task TheCheckerListsTheSameSystemsTheWriterWasGiven()
    {
        var prompt = await FidelityPrompt(new Galaxy());

        Assert.Contains("Anarchy systems within reach, from the galaxy search, which a line may state are Anarchy systems:", prompt);
        Assert.Contains("- Dyson's Hollow", prompt);
        Assert.DoesNotContain("- Ossen's Lantern", prompt);
    }

    [Fact]
    public async Task WithNoAnarchySystemInReachTheCheckerGetsNoList()
    {
        var prompt = await FidelityPrompt(new Galaxy { AnarchyInReach = false });

        Assert.DoesNotContain("Anarchy systems within reach", prompt);
    }

    [Fact]
    public async Task ALineTellingTheCommanderWhichMissionsToTakeStatesNothingToCheck()
    {
        var prompt = await FidelityPrompt(new Galaxy());

        Assert.Contains("A line that tells the Commander which missions to take", prompt);
        Assert.Contains("states nothing to check", prompt);
        Assert.Contains("A line that says the missions' targets were Anarchy-run is still a claim about how a mission went.", prompt);
    }
}
