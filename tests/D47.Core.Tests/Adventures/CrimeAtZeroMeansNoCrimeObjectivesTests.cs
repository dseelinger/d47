using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class CrimeAtZeroMeansNoCrimeObjectivesTests
{
    private static readonly DateTimeOffset Now = IllegalMissionsAreSteeredToAnarchyTests.Now;

    private static string Spine => IllegalMissionsAreSteeredToAnarchyTests.Spine;

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

    [Theory]
    [InlineData("Mission_Piracy")]
    [InlineData("Mission_Smuggle")]
    [InlineData("Mission_OnFoot_Heist_Covert")]
    public async Task ACrimeObjectiveIsRefusedAndGoesToARepairRound(string family)
    {
        var beats = IllegalMissionsAreSteeredToAnarchyTests.Beats("Dyson's Hollow", family);
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(beats), Saying(Repairs.Replacing(beats, 2)), Saying(Repairs.Replacing(beats, 2)));

        var outcome = await Generate(provider, new AdventureAsk(Length: AdventureLength.Short, CrimeRefused: true));

        Assert.False(outcome.Succeeded);
        Assert.Contains("Objective 2 (The Job) is a crime, and the Commander's Crime rating is 0", outcome.Refusal);
        Assert.True(provider.Requests.Count >= 3);
    }

    [Theory]
    [InlineData("Mission_Piracy")]
    [InlineData("Mission_OnFoot_Heist_Covert")]
    public async Task WithCrimeAtOneOrMoreACrimeObjectiveStandsAsBefore(string family)
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(IllegalMissionsAreSteeredToAnarchyTests.Beats("Dyson's Hollow", family)));

        var outcome = await Generate(provider, new AdventureAsk(Length: AdventureLength.Short));

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task AMissionThatIsNotACrimeStandsWithCrimeAtZero()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(IllegalMissionsAreSteeredToAnarchyTests.Beats("Dyson's Hollow", "Mission_OnFoot_Assassination")));

        var outcome = await Generate(provider, new AdventureAsk(Length: AdventureLength.Short, CrimeRefused: true));

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task WithCrimeRefusedTheWriterIsToldSoAndGivenNoAnarchySystems()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(IllegalMissionsAreSteeredToAnarchyTests.Beats("Ossen's Lantern", "Mission_OnFoot_Assassination")));

        await Generate(provider, new AdventureAsk(Length: AdventureLength.Short, CrimeRefused: true));

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("You set Crime to 0, so write no crime objective", brief);
        Assert.Contains("piracy", brief);
        Assert.Contains("stolen cargo", brief);
        Assert.DoesNotContain("Anarchy systems within reach", brief);
        Assert.DoesNotContain("Dyson's Hollow", brief);
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

    private static Task<AdventureOutcome> Generate(RoundScriptedLlmProvider provider, AdventureAsk ask) =>
        IllegalMissionsAreSteeredToAnarchyTests.Generator(provider, new IllegalMissionsAreSteeredToAnarchyTests.Galaxy()).GenerateAsync(ask, Now, CancellationToken.None);
}
