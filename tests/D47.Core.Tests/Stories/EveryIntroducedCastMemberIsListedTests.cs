using D47.Core.Tests.Conversation;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The running story's introduced cast is listed whether or not a member is primary (#2163).</summary>
public sealed class EveryIntroducedCastMemberIsListedTests
{
    private static StorySecret Cast() => Secret with
    {
        Cast =
        [
            Secret.Cast[0] with { Primary = true, Role = "the dock hand who signs the Commander's berth" },
            new StorySpeaker { Id = "halle", Name = "Halle", Who = "A broker.", Role = "the broker who lends the Commander a berth", Provider = StorySpeaker.Kokoro, Voice = "af_heart" },
            new StorySpeaker { Id = "later", Name = "Later", Who = "Not met yet.", Role = "someone met in chapter two", Provider = StorySpeaker.Kokoro, Voice = "af_heart" },
        ],
    };

    [Fact]
    public async Task BothIntroducedMembersAreListedInIntroductionOrderAndAnUnmetOneIsNot()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines())),
            Cast());

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Director.Introduced("F1", Id, "halle");
        fixtures.Director.Introduced("F1", Id, "dock-hand");

        Assert.Equal(["halle", "dock-hand"], fixtures.Director.IntroducedCast("F1").Select(member => member.Shown.Id));
        Assert.Equal(["dock-hand"], fixtures.Director.PrimaryCast(Id).Select(member => member.Shown.Id));
    }

    [Fact]
    public void NoCurrentStoryListsNobody()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(), Cast());

        Assert.Empty(fixtures.Director.IntroducedCast("F1"));
    }
}
