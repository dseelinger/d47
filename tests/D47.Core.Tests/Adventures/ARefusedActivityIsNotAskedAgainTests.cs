using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>An activity refused in a story is a key; a chapter without it is written.</summary>
public sealed class ARefusedActivityIsNotAskedAgainTests
{
    [Fact]
    public async Task AChapterWithoutTheActivityIsWritten()
    {
        var story = Stories.PlannedChapterWorld.Story with { Refused = ["bond"] };
        var ask = Stories.PlannedChapterWorld.Ask(story);
        var plan = await Stories.PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.Lines(plan.Objectives.Count)),
            RoundScriptedLlmProvider.Saying(Stories.PlannedChapterWorld.NoObjections));

        var outcome = await Stories.PlannedChapterWorld.Generator(provider).GenerateAsync(ask, AdventureFixtures.Accepted, TestContext.Current.CancellationToken);

        Assert.NotNull(outcome.Draft);
    }

    [Fact]
    public void APlaceKindHasNoKeyAndAMissionKeyCarriesItsFamily()
    {
        Assert.Null(RefusedActivities.Key(TriggerKind.Dock, null));
        Assert.Null(RefusedActivities.Key(TriggerKind.Arrive, null));
        Assert.Equal("bond", RefusedActivities.Key(TriggerKind.Bond, null));
        Assert.Equal("mission:Mission_Massacre", RefusedActivities.Key(TriggerKind.Mission, "Mission_Massacre"));
        Assert.Equal("mission", RefusedActivities.Key(TriggerKind.Mission, null));
    }

    [Fact]
    public void AMissionRefusedWithoutAFamilyRefusesEveryMission()
    {
        Assert.True(RefusedActivities.Refuses(["mission"], TriggerKind.Mission, "Mission_Courier"));
        Assert.True(RefusedActivities.Refuses(["mission"], TriggerKind.Mission, null));
        Assert.False(RefusedActivities.Refuses(["mission"], TriggerKind.Bounty, null));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheComfortZonePickSkipsARefusedActivity()
    {
        var statistics = Stories.StoryFixtures.Loaded(1, statistics: "\"Combat\":{ \"Bounties_Claimed\":0 }").Statistics;

        Assert.Equal("bounty", ChapterFit.LeastDone(statistics)?.Name);
        Assert.Equal("bond", ChapterFit.LeastDone(statistics, ["bounty"])?.Name);
    }
}
