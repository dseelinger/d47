using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Goals;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Adventures;

public class CarrierSquadronAndTeamBeatsTests
{
    private static AdventureStanding Fold(AdventureTrigger beat, params string[] events) =>
        FoldCounting(beat, "The Beat", events);

    private static string Fleet(long system) =>
        $$"""{ "timestamp":"@", "event":"CarrierLocation", "CarrierType":"FleetCarrier", "CarrierID":11, "StarSystem":"X", "SystemAddress":{{system}}, "BodyID":10 }""";

    private static string Squadron(long system) =>
        $$"""{ "timestamp":"@", "event":"CarrierLocation", "CarrierType":"SquadronCarrier", "CarrierID":22, "StarSystem":"Y", "SystemAddress":{{system}}, "BodyID":10 }""";

    private static AdventureTrigger Of(TriggerKind kind, int count = 1) => new() { Kind = kind, Count = count };

    [Fact]
    public void ACarrierJumpBeatFiresOnTheSecondChangeOfSystemForTheFleetCarrier()
    {
        var trigger = Of(TriggerKind.CarrierJump, 2);

        Assert.Equal(0, Fold(trigger, Fleet(100)).Counted);
        Assert.Equal(0, Fold(trigger, Fleet(100), Fleet(100)).Counted);
        Assert.Equal(1, Fold(trigger, Fleet(100), Fleet(200)).Counted);
        Assert.True(Fold(trigger, Fleet(100), Fleet(200), Fleet(300)).IsDone);
    }

    [Fact]
    public void ACarrierJumpBeatIgnoresTheSquadronCarrier()
    {
        var trigger = Of(TriggerKind.CarrierJump, 1);

        Assert.Equal(1, Fold(trigger, Squadron(100), Squadron(200), Fleet(300), Squadron(400)).Current);
    }

    [Theory]
    [InlineData(TriggerKind.Wing, "WingJoin", "\"Others\":[\"A\"]")]
    [InlineData(TriggerKind.Wing, "WingAdd", "\"Name\":\"A\"")]
    [InlineData(TriggerKind.Multicrew, "JoinACrew", "\"Captain\":\"A\"")]
    [InlineData(TriggerKind.CarrierBuy, "CarrierBuy", "\"CarrierType\":\"FleetCarrier\",\"CarrierID\":5")]
    [InlineData(TriggerKind.Squadron, "JoinedSquadron", "\"SquadronName\":\"S\"")]
    [InlineData(TriggerKind.SquadronFound, "SquadronCreated", "\"SquadronName\":\"S\"")]
    public void EachTeamBeatFiresOnItsOwnEvent(TriggerKind kind, string journalEvent, string fields)
    {
        var trigger = new AdventureTrigger { Kind = kind, Count = AdventureTrigger.IsOnceKind(kind) ? null : 1 };

        Assert.True(Fold(trigger, $$"""{ "timestamp":"@", "event":"{{journalEvent}}", {{fields}} }""").IsDone);
        Assert.False(Fold(trigger, """{ "timestamp":"@", "event":"Music", "MusicTrack":"x" }""").IsDone);
    }

    [Fact]
    public void ACarrierBuyBeatIgnoresASquadronCarrier()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.CarrierBuy };

        Assert.False(Fold(trigger, """{ "timestamp":"@", "event":"CarrierBuy", "CarrierType":"SquadronCarrier", "CarrierID":5 }""").IsDone);
    }

    [Fact]
    public void ThePlainFormsAreWordsTheFileUses()
    {
        foreach (var word in new[] { "carrierbuy", "carrierjump", "wing", "multicrew", "squadron", "squadronfound" })
        {
            Assert.Contains(word, AdventureValidation.Kinds);
            Assert.True(AdventureValidation.TryKind(word, out _));
        }
    }

    [Theory]
    [InlineData(TriggerKind.CarrierBuy, 10_472_082_550, false, false, true)]
    [InlineData(TriggerKind.CarrierBuy, 6_000_000_000, false, false, false)]
    [InlineData(TriggerKind.CarrierBuy, 10_472_082_550, true, false, false)]
    [InlineData(TriggerKind.CarrierJump, 0, true, false, true)]
    [InlineData(TriggerKind.CarrierJump, 10_472_082_550, false, false, false)]
    [InlineData(TriggerKind.Wing, 0, false, true, true)]
    [InlineData(TriggerKind.Multicrew, 0, false, true, true)]
    [InlineData(TriggerKind.Squadron, 0, false, false, true)]
    [InlineData(TriggerKind.Squadron, 0, false, true, false)]
    [InlineData(TriggerKind.CarrierBuy, 5_600_000_000, false, false, false)]
    [InlineData(TriggerKind.SquadronFound, 20_000_000, false, false, true)]
    [InlineData(TriggerKind.SquadronFound, 10_000_000, false, false, false)]
    [InlineData(TriggerKind.SquadronFound, 10_000_000, false, true, false)]
    public void AKindIsAllowedOnlyWhereTheCommanderCanDoIt(TriggerKind kind, long credits, bool owns, bool inSquadron, bool allowed)
    {
        Assert.Equal(allowed, TeamBeats.Why(kind, owns, inSquadron, credits) is null);
    }

    [Fact]
    public void SquadronMembershipBeginsAtAStartupAndEndsAtALeaveOrTheNextLoad()
    {
        var state = SquadronState.None;

        SquadronState Next(string json) => state = state.Apply(Event(json));

        Assert.False(state.IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:00:00Z", "event":"SquadronStartup", "SquadronName":"GREYBEARD DELTA", "CurrentRank":3 }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:01:00Z", "event":"LeftSquadron", "SquadronName":"GREYBEARD DELTA" }""").IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:02:00Z", "event":"JoinedSquadron", "SquadronName":"X" }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:03:00Z", "event":"LoadGame" }""").IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:04:00Z", "event":"SquadronCreated", "SquadronName":"X" }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:05:00Z", "event":"DisbandedSquadron", "SquadronName":"X" }""").IsMember);
        Assert.True(Next("""{ "timestamp":"2026-10-01T10:06:00Z", "event":"JoinedSquadron", "SquadronName":"X" }""").IsMember);
        Assert.False(Next("""{ "timestamp":"2026-10-01T10:07:00Z", "event":"KickedFromSquadron", "SquadronName":"X" }""").IsMember);
    }

    [Fact]
    public void ATeamStepOfAGoalIsPlannedOnlyWhereTheCommanderCanDoIt()
    {
        AdventureTrigger Step(TriggerKind kind) => new() { Kind = kind };
        var buy = new GoalClimbStep("carrier", "Carrier") { Objective = Step(TriggerKind.CarrierBuy) };
        var found = new GoalClimbStep("squadron", "Squadron") { Objective = Step(TriggerKind.SquadronFound) };
        var rich = Inputs(Bounty) with { Credits = 10_472_082_550, Goals = [buy] };

        Assert.NotNull(ChapterPlanner.Plan(rich, Places(), 3).Plan);
        Assert.Equal(ChapterRules.Team, ChapterPlanner.Plan(rich with { Carrier = new CarrierState { CallSign = "AAA-111" } }, Places(), 3).Refusal?.Rule);
        Assert.Equal(ChapterRules.Team, ChapterPlanner.Plan(rich with { Credits = 6_000_000_000 }, Places(), 3).Refusal?.Rule);
        Assert.NotNull(ChapterPlanner.Plan(Inputs(Bounty) with { Credits = 20_000_000, Goals = [found] }, Places(), 3).Plan);
        Assert.Equal(ChapterRules.Team, ChapterPlanner.Plan(Inputs(Bounty) with { Credits = 19_999_999, Goals = [found] }, Places(), 3).Refusal?.Rule);
        Assert.Equal(ChapterRules.Team, ChapterPlanner.Plan(Inputs(Bounty) with { Credits = 50_000_000, InSquadron = true, Goals = [found] }, Places(), 3).Refusal?.Rule);
    }
}
