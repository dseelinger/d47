using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class TheChapterWriterIsToldWhoIsOnTheCarrierTests
{
    private static readonly StorySpeaker Rider = new()
    {
        Id = "rider",
        Name = "Vale",
        Who = "A courier on the carrier, dry and patient.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bm_lewis",
        Link = 0.3,
        Aboard = [new(null, null, StorySpeakerAboard.Carrier)],
    };

    private static async Task<string> FirstPrompt(StorySecret secret)
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
                RoundScriptedLlmProvider.Saying(NothingObjected)),
            secret);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        return fixtures.Provider.Requests.First(request => request.Prompt.History[0].Text.Contains("cast member", StringComparison.Ordinal)).Prompt.History[0].Text;
    }

    [Fact]
    public async Task ACarrierMemberIsNamedOnTheCarrierAndNotInTheCockpit()
    {
        var prompt = await FirstPrompt(Secret with { Cast = [.. Secret.Cast, Rider] });

        Assert.Contains("On the Commander's fleet carrier as this chapter opens: Vale.", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Aboard the Commander's ship as this chapter opens: Vale", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("in the cockpit with the Commander", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStoryWithNoCarrierMemberGetsThePromptItGotBefore()
    {
        var prompt = await FirstPrompt(Secret);

        Assert.DoesNotContain("fleet carrier as this chapter opens", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("comes aboard the carrier", prompt, StringComparison.Ordinal);
    }
}
