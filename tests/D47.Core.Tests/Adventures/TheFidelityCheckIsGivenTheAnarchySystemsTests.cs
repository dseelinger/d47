using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;
using static D47.Core.Tests.Stories.PlannedChapterWorld;

namespace D47.Core.Tests.Adventures;

public sealed class TheFidelityCheckIsGivenTheAnarchySystemsTests
{
    [Fact]
    public async Task ALineTellingTheCommanderWhichMissionsToTakeStatesNothingToCheck()
    {
        var ask = Ask();
        var plan = await PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(Saying(Lines(plan.Objectives.Count)), Saying(NoObjections));

        var outcome = await Generator(provider).GenerateAsync(ask, new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var prompt = provider.Requests.First(request => request.ResponseSchema?.Name == "story_fidelity").Prompt.History[0].Text;
        Assert.Contains("A line that tells the Commander which missions to take", prompt);
        Assert.Contains("states nothing to check", prompt);
        Assert.Contains("A line that says the missions' targets were Anarchy-run is still a claim about how a mission went.", prompt);
    }
}
