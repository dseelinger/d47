using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>After the beacon scan and before the finale, a long-haul story's chapter is asked for at a long reach only when it draws exploration or exobiology.</summary>
public sealed class ALongHaulChapterMustServeItTests
{
    private static DrawnActivity Slot(TriggerKind kind) => new(kind, null, 1);

    private static AdventureTrigger Rank(string career) => new() { Kind = TriggerKind.Rank, Career = career, Rank = 2 };

    [Theory]
    [InlineData(TriggerKind.Discover)]
    [InlineData(TriggerKind.Map)]
    [InlineData(TriggerKind.Footfall)]
    [InlineData(TriggerKind.Organic)]
    public void ADiscoveryMapFootfallOrOrganicSlotServesALongHaul(TriggerKind kind) =>
        Assert.True(ChapterFit.ServesLongHaul([Slot(TriggerKind.Bounty), Slot(kind)], null));

    [Theory]
    [InlineData(TriggerKind.Srv)]
    [InlineData(TriggerKind.Signal)]
    [InlineData(TriggerKind.Codex)]
    [InlineData(TriggerKind.Wreck)]
    [InlineData(TriggerKind.Uss)]
    [InlineData(TriggerKind.Boost)]
    [InlineData(TriggerKind.Bounty)]
    public void AnyOtherSlotDoesNot(TriggerKind kind) =>
        Assert.False(ChapterFit.ServesLongHaul([Slot(kind)], null));

    [Fact]
    public void ADrawWithNoSlotsDoesNot() => Assert.False(ChapterFit.ServesLongHaul(null, null));

    [Theory]
    [InlineData("Explore", true)]
    [InlineData("Exobiologist", true)]
    [InlineData("Combat", false)]
    [InlineData("Trade", false)]
    public void OnlyAnExplorationOrExobiologyRankObjectiveServesALongHaul(string career, bool expected) =>
        Assert.Equal(expected, ChapterFit.ServesLongHaul([Slot(TriggerKind.Srv)], Rank(career)));

    [Fact]
    public void AnObjectiveThatIsNotARankDoesNot() =>
        Assert.False(ChapterFit.ServesLongHaul(null, new AdventureTrigger { Kind = TriggerKind.PowerRank, Rank = 3 }));

    /// <summary>Fixtures for a three-month story whose Commander scanned a beacon in an earlier story, so chapter one is the first after the scan.</summary>
    private static StoryFixtures AfterTheScan(bool goals)
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere), RoundScriptedLlmProvider.Saying(NothingObjected)),
            card: Card with { Length = StoryPacing.ThreeMonths.Key, Goals = goals });

        fixtures.Stories.Save("F1", new Story
        {
            Id = Other.Id,
            Title = Other.Title,
            PublicLayer = Other.Describe(),
            Length = StoryPacing.OneYear.Key,
            State = StoryState.Finished,
            PickedAt = Now.AddDays(-200),
            BeaconScanAt = Now.AddDays(-199),
            BeaconSystems = [BeaconAddress],
            StoppedAt = Now.AddDays(-1),
        });
        fixtures.Director.Game = () => Loaded(300_000_000);
        return fixtures;
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AGoalsChapterThatWorksCombatIsAskedForAtSessionReach()
    {
        using var fixtures = AfterTheScan(goals: true);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        var ask = Assert.Single(fixtures.Asks);

        Assert.Equal("Combat", ask.Story!.Goal?.Draw);
        Assert.Equal(AdventureReach.Session, ask.Reach);
        Assert.False(ask.Story.LongHaul);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterIsAskedForAtTheReachItsDrawServes()
    {
        using var fixtures = AfterTheScan(goals: false);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        var ask = Assert.Single(fixtures.Asks);
        var serves = ChapterFit.ServesLongHaul(ask.Story!.Slots, ask.Story.Goal?.Objective);

        Assert.Equal(serves ? AdventureReach.Anywhere : AdventureReach.Session, ask.Reach);
        Assert.Equal(serves, ask.Story.LongHaul);
    }
}
