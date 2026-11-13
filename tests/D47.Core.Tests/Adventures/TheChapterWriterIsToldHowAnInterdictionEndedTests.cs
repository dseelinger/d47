using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class TheChapterWriterIsToldHowAnInterdictionEndedTests
{
    private const string Interdicted =
        """{ "timestamp":"@", "event":"Interdicted", "Submitted":true, "Interdictor":"$npc_name_decorate_2;", "Interdictor_Localised":"Orla Vance", "IsPlayer":false, "Faction":"Zeta Collective" }""";

    private static string Text(params string[] lines)
    {
        var standing = AdventureFold.Start(LanternRoute(Accepted));
        var minute = 1;

        foreach (var line in lines)
        {
            standing = AdventureFold.Apply(standing, Event(line.Replace("@", Stamp(Accepted.AddMinutes(minute++)), StringComparison.Ordinal)));
        }

        return ChapterNotables.Told(standing.Notables).Single(entry => entry.Kind == NotableKind.Interdicted).Text;
    }

    [Fact]
    public void ADestroyedInterdictorIsSaid()
    {
        const string Kill = """{ "timestamp":"@", "event":"Bounty", "PilotName":"$npc_name_decorate_2;", "PilotName_Localised":"Orla Vance", "Target":"sidewinder", "TotalReward":500 }""";

        Assert.Equal("Pulled out of supercruise by a ship of Zeta Collective; they submitted, then destroyed it.", Text(Interdicted, Kill));
    }

    [Fact]
    public void ADeathIsSaidWithoutSayingWhoFired()
    {
        const string Died = """{ "timestamp":"@", "event":"Died" }""";

        Assert.Equal("Pulled out of supercruise by a ship of Zeta Collective; they submitted, then died.", Text(Interdicted, Died));
    }

    [Theory]
    [InlineData("""{ "timestamp":"@", "event":"SupercruiseEntry", "StarSystem":"Somewhere" }""")]
    [InlineData("""{ "timestamp":"@", "event":"FSDJump", "StarSystem":"Somewhere" }""")]
    [InlineData("""{ "timestamp":"@", "event":"Docked", "StationName":"Some Station", "MarketID":1 }""")]
    public void ReachingSupercruiseJumpingOrDockingIsGettingAway(string line)
    {
        Assert.Equal("Pulled out of supercruise by a ship of Zeta Collective; they submitted, then got away.", Text(Interdicted, line));
    }

    [Fact]
    public void ABountyOnAnotherPilotLeavesItAsWritten()
    {
        const string Other = """{ "timestamp":"@", "event":"Bounty", "PilotName_Localised":"Somebody Else", "Target":"sidewinder", "TotalReward":500 }""";

        Assert.Equal("Pulled out of supercruise by a ship of Zeta Collective; they submitted.", Text(Interdicted, Other));
    }

    [Fact]
    public void ALaterEventDoesNotRewriteAnEndedInterdiction()
    {
        const string Died = """{ "timestamp":"@", "event":"Died" }""";
        const string Jump = """{ "timestamp":"@", "event":"FSDJump", "StarSystem":"Somewhere" }""";

        Assert.EndsWith("then died.", Text(Interdicted, Died, Jump));
    }
}
