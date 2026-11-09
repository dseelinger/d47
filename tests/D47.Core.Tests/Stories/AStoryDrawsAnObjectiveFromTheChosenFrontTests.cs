using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>The faction row is drawn only with a chosen front in reach, and it works for that front.</summary>
public sealed class AStoryDrawsAnObjectiveFromTheChosenFrontTests
{
    private const int Seeds = 400;

    private static readonly ChapterFront Front = new("Federal Reclamation Co", "Wolf 359", 3932277482L);

    private static readonly ShipLoadout Ship = new() { Type = "cobramkiii", CargoCapacity = 64 };

    private static readonly RankState Ranks = new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Combat", 0), new RankStanding("Soldier", 0)],
    };

    private static IReadOnlyList<DrawnActivity> Draw(uint seed, ChapterFront? front) =>
        ChapterDraw.Draw(
            CareerWeights.From(new Dictionary<Career, int>(), CareerStatistics.Empty), Ranks, Ship, odyssey: true, [], null, comfort: false,
            CareerStatistics.Empty, 0, seed, front: front);

    private static CommanderGameState Game(string system) =>
        new(new CommanderIdentity("F1", "Tester")) { Location = new JournalLocation(system, null, false, null) };

    [Fact]
    public void WithNoFrontNoFactionObjectiveIsDrawn()
    {
        for (uint seed = 0; seed < Seeds; seed++)
        {
            Assert.DoesNotContain(Draw(seed, null), slot => slot.Kind == TriggerKind.Faction);
        }
    }

    [Fact]
    public void WithAFrontAFactionObjectiveIsDrawnForTheCampaignFactionInItsSystem()
    {
        var drawn = Enumerable.Range(0, Seeds).SelectMany(seed => Draw((uint)seed, Front)).Where(slot => slot.Kind == TriggerKind.Faction).ToList();

        Assert.NotEmpty(drawn);
        Assert.All(drawn, slot =>
        {
            Assert.Equal("Federal Reclamation Co", slot.Faction);
            Assert.Equal("Wolf 359", slot.System);
            Assert.Equal(3932277482L, slot.SystemAddress);
            Assert.InRange(slot.Count, 1, 3);
        });
    }

    [Fact]
    public void OnlyTheFactionRowNeedsAFront()
    {
        Assert.Equal([TriggerKind.Faction], ChapterActivities.All.Where(ChapterActivities.NeedsFront).Select(row => row.Kind));
        Assert.Null(ChapterActivities.Find(TriggerKind.Faction, null, null));
    }

    [Fact]
    public void ADrawnFactionObjectiveIsAFactionStepWithTheFrontsFactionAndSystem()
    {
        var slot = new DrawnActivity(TriggerKind.Faction, null, 2) { Faction = Front.Faction, System = Front.System, SystemAddress = Front.SystemAddress };
        var trigger = new AdventureTrigger { Kind = slot.Kind, Count = slot.Count, Faction = slot.Faction, System = slot.System, SystemAddress = slot.SystemAddress };

        Assert.Equal("earn 2 influence marks for Federal Reclamation Co in Wolf 359", trigger.Describe());
    }

    [Fact]
    public void TheBriefNamesTheFrontAsTheCommandersOwnCause()
    {
        var step = new AdventureTrigger { Kind = TriggerKind.Faction, Count = 2, Faction = Front.Faction, System = Front.System };
        var plan = new ChapterPlan([new PlannedObjective("work", [step], default!)], null);
        var text = new System.Text.StringBuilder();

        AdventureGenerator.AppendFront(text, plan);

        Assert.Contains("influence marks for Federal Reclamation Co in Wolf 359 are the Commander's own cause", text.ToString());
    }

    [Fact]
    public void ABriefWithNoFrontStepSaysNothingOfOne()
    {
        var plan = new ChapterPlan([new PlannedObjective("work", [new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 3 }], default!)], null);
        var text = new System.Text.StringBuilder();

        AdventureGenerator.AppendFront(text, plan);

        Assert.Empty(text.ToString());
    }

    [Fact]
    public async Task AFrontWithinReachIsKept()
    {
        var kept = await DrawGates.FrontInReachAsync(Front, new Galaxy(120), Game("Sol"), NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Same(Front, kept);
    }

    [Theory]
    [InlineData(5000.0)]
    [InlineData(null)]
    public async Task AFrontOutOfReachOrOfUnknownDistanceIsNotKept(double? away)
    {
        Assert.Null(await DrawGates.FrontInReachAsync(Front, new Galaxy(away), Game("Sol"), NullLogger.Instance, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AFrontWithNoKnownSystemAddressIsNotKept()
    {
        var unknown = Front with { SystemAddress = null };

        Assert.Null(await DrawGates.FrontInReachAsync(unknown, new Galaxy(10), Game("Sol"), NullLogger.Instance, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NoFrontNoGalaxyOrNoLocationKeepsNothing()
    {
        var token = TestContext.Current.CancellationToken;

        Assert.Null(await DrawGates.FrontInReachAsync(null, new Galaxy(10), Game("Sol"), NullLogger.Instance, token));
        Assert.Null(await DrawGates.FrontInReachAsync(Front, null, Game("Sol"), NullLogger.Instance, token));
        Assert.Null(await DrawGates.FrontInReachAsync(Front, new Galaxy(10), null, NullLogger.Instance, token));
    }

    private sealed class Galaxy(double? distance) : StubGalaxyService, IGalaxyService
    {
        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult(distance);
    }
}
