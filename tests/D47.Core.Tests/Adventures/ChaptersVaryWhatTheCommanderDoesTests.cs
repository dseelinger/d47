using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// Nothing in the chapter writer's prompt leans toward one activity: the counted kinds and mission families come in an
/// order fixed per draft, every catalogued family is offered, the activities of the last chapters are named so the next
/// can avoid them, and a comfort-zone tie does not always go to bounties.
/// </summary>
public sealed class ChaptersVaryWhatTheCommanderDoesTests
{
    private static Adventure Doing(string key, params AdventureTrigger[] triggers) => LanternRoute() with
    {
        Key = key,
        Beats = [.. triggers.Select((trigger, index) => Beat($"Objective {index + 1}", null, trigger, "A line."))],
    };

    private static AdventureTrigger Counted(TriggerKind kind, string? family = null) => new() { Kind = kind, MissionFamily = family, Count = 1 };

    [Fact]
    public void TheLastThreeChaptersActivitiesAreNamedNewestFirst()
    {
        var chapter = new AdventureChapter(
            Doing("previous", Counted(TriggerKind.Bounty), Counted(TriggerKind.Mission, "Mission_Courier")),
            [Doing("oldest", Counted(TriggerKind.Mine)), Doing("middle", Counted(TriggerKind.Organic)), Doing("recent", Counted(TriggerKind.Bond), Counted(TriggerKind.Bounty))]);

        Assert.Equal(["bounty", "mission:Mission_Courier", "bond", "organic"], AdventureGenerator.RecentActivities(chapter));
    }

    [Fact]
    public void PlacesAreNotActivities() =>
        Assert.Empty(AdventureGenerator.RecentActivities(new AdventureChapter(LanternRoute(), [])));

    [Fact]
    public void TheOrderIsFixedBySeedAndKeepsEveryItem()
    {
        var items = Enumerable.Range(1, 30).ToList();

        Assert.Equal(AdventureGenerator.Shuffled(items, "The Lantern Route"), AdventureGenerator.Shuffled(items, "The Lantern Route"));
        Assert.Equal(items, AdventureGenerator.Shuffled(items, "The Lantern Route").Order());
        Assert.NotEqual(AdventureGenerator.Shuffled(items, "The Lantern Route"), AdventureGenerator.Shuffled(items, "The Second Column"));
    }

    [Fact]
    public void EveryCataloguedFamilyIsOfferedExceptThoseSetAside()
    {
        Assert.Contains("Mission_Smuggle", MissionFamilies.Offered);
        Assert.Contains("Mission_PassengerVIP", MissionFamilies.Offered);
        Assert.Contains("Mission_Sightseeing", MissionFamilies.Offered);
        Assert.Contains("Mission_OnFoot_Reboot", MissionFamilies.Offered);
        Assert.DoesNotContain(MissionFamilies.Offered, MissionFamilies.IsSetAside);
        Assert.True(MissionFamilies.Offered.Count > 30);
    }

    [Fact]
    public void AComfortTieCountsFromTheRowItIsGiven()
    {
        var statistics = Stories.StoryFixtures.Loaded(1, statistics: "\"Combat\":{ \"Bounties_Claimed\":0 }").Statistics;

        Assert.Equal("bounty", ChapterFit.LeastDone(statistics)?.Name);
        Assert.Equal("bond", ChapterFit.LeastDone(statistics, first: 1)?.Name);
        Assert.Equal("passenger mission", ChapterFit.LeastDone(statistics, first: 5)?.Name);
        Assert.Equal("bounty", ChapterFit.LeastDone(statistics, first: 6)?.Name);
        Assert.Equal("mine", ChapterFit.LeastDone(statistics, ["bond"], first: 1)?.Name);
    }
}
