using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class CrimeAtZeroMeansNoCrimeObjectivesTests
{
    private static EventFilter Test(string field, string value) => new([new EventField(field, value)]);

    [Theory]
    [InlineData("CollectCargo", "Stolen", "true", true)]
    [InlineData("CollectItems", "Stolen", "true", true)]
    [InlineData("MarketSell", "StolenGoods", "true", true)]
    [InlineData("CollectCargo", "Stolen", "false", false)]
    [InlineData("MarketSell", "StolenGoods", "false", false)]
    [InlineData("Docked", "Stolen", "true", false)]
    public void AnEventStepIsACrimeOnlyWhenItTestsStolenGoodsAsTrue(string name, string field, string value, bool crime) =>
        Assert.Equal(crime, CrimeObjectives.Is(new AdventureTrigger { Kind = TriggerKind.Event, Event = name, Where = Test(field, value) }));

    [Fact]
    public void AnEventStepWithNoStolenTestIsNotACrime() =>
        Assert.False(CrimeObjectives.Is(new AdventureTrigger { Kind = TriggerKind.Event, Event = "CollectCargo" }));

    [Theory]
    [InlineData("Mission_Piracy", true)]
    [InlineData("Mission_Smuggle", true)]
    [InlineData("Mission_OnFoot_Heist_Covert_MB", true)]
    [InlineData("Mission_OnFoot_AssassinationIllegal_Covert_MB", true)]
    [InlineData("Mission_Courier", false)]
    public void AMissionIsACrimeWhenItsFamilyIs(string family, bool crime) =>
        Assert.Equal(crime, CrimeObjectives.Is(new AdventureTrigger { Kind = TriggerKind.Mission, MissionFamily = family }));

    [Fact]
    public async Task AShortStoryWithCrimeRefusedPlansNoCrimeMissionAndStillPlansOtherMissions()
    {
        var generator = PlannedChapterWorld.Generator(null);
        var families = new List<string>();

        for (var seed = 1u; seed <= 200; seed++)
        {
            var planned = await generator.ShortPlanAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Short, CrimeRefused: true), AdventureGenerator.ShortWants.None, seed, TestContext.Current.CancellationToken);

            if (planned.Plan is { } plan)
            {
                families.AddRange(plan.Objectives.SelectMany(objective => objective.Steps).Where(step => step.Kind == TriggerKind.Mission).Select(step => step.MissionFamily!));
            }
        }

        Assert.NotEmpty(families);
        Assert.DoesNotContain(families, family => MissionFamilies.IsIllegal(family) || CrimeObjectives.IsFamily(family));
    }

    [Fact]
    public void WithCrimeAtZeroNoDrawnEventStepTestsStolenGoods()
    {
        var catalogue = StepEventCatalogue.Parse(
            "journal\tCollectCargo\tyes\t\t\tCargo was collected.\n",
            "CollectCargo\tStolen\tboolean\t\tyes\nCollectCargo\tStolen\tboolean\ttrue\tyes\nCollectCargo\tStolen\tboolean\tfalse\tyes\n");
        var weights = CareerWeights.From(new Dictionary<Career, int> { [Career.Crime] = 0 }, CareerStatistics.Empty);
        var ship = new ShipLoadout { Type = "cobramkiii", CargoCapacity = 64, Modules = [] };
        var events = Enumerable.Range(0, 500)
            .SelectMany(seed => ChapterDraw.Draw(weights, RankState.Empty, ship, odyssey: true, [], null, comfort: false, CareerStatistics.Empty, 0, (uint)seed, events: catalogue))
            .Where(slot => slot.Kind == TriggerKind.Event)
            .ToList();

        Assert.NotEmpty(events);
        Assert.DoesNotContain(events, slot => CrimeObjectives.Is(TriggerKind.Event, null, slot.Event, slot.Where));
    }

}
