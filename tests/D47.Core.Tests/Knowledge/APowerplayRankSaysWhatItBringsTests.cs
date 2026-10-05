using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class APowerplayRankSaysWhatItBringsTests
{
    private static CommanderGameState Commander(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F123", "Jameson"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static string Powerplay(string power, int rank, long merits) =>
        $$"""{"timestamp":"3311-01-01T00:00:00Z","event":"Powerplay","Power":"{{power}}","Rank":{{rank}},"Merits":{{merits}}}""";

    private static CommanderGameState LiYongRui() => Commander(Powerplay("Li Yong-Rui", 8, 45_292));

    private static async Task<ToolResult> Ask(CommanderGameState? state, params (string Key, string Value)[] arguments)
    {
        var registry = CapabilityRegistry.Build([PowerplayCapability.Create(() => state)]);

        return await registry.InvokeAsync(
            PowerplayCapability.Tool,
            new ToolArguments(arguments.ToDictionary(a => a.Key, a => a.Value)),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheMaintainersRankEightLiYongRuiIsAnsweredInFull()
    {
        var result = await Ask(LiYongRui());

        Assert.False(result.IsError);
        Assert.Equal(
            "Rank 8 with Li Yong-Rui. "
            + "In Li Yong-Rui's territory your rebuy is 40% lower. "
            + "In Li Yong-Rui's territory, exploration data sales up 20%. "
            + "Rank 9, 1,708 merits away, gives a mini care package. "
            + "The next perk is at rank 11, 17,708 merits away: 33% off rebuy when a rival Power's ship kills you "
            + "outside Li Yong-Rui's territory. "
            + "The first module is the Pack-Hound Missile Rack at rank 34, 201,708 merits away.",
            result.Content);
    }

    [Fact]
    public async Task AnUnpledgedCommanderIsAskedToNameAPower()
    {
        var result = await Ask(Commander("""{"timestamp":"3311-01-01T00:00:00Z","event":"PowerplayLeave"}"""));

        Assert.Contains("not pledged", result.Content);
        Assert.Contains("Name a Power", result.Content);
    }

    [Fact]
    public void APowerTheTableDoesNotListHasNoRanks()
    {
        Assert.Equal("I have no rank table for Nobody.", PowerplayCapability.Describe(LiYongRui(), "Nobody"));
    }

    [Fact]
    public async Task RankOneHundredSaysEveryFurtherRankIsAFullCarePackage()
    {
        var result = await Ask(LiYongRui(), ("rank", "100"));

        Assert.EndsWith("Every rank past 100 gives a full care package.", result.Content);
        Assert.DoesNotContain("The next perk", result.Content);
    }

    [Fact]
    public async Task AnotherPowerGivesNoMeritsClause()
    {
        var result = await Ask(LiYongRui(), ("power", "Edmund Mahon"), ("rank", "10"));

        Assert.StartsWith("Rank 10 with Edmund Mahon.", result.Content);
        Assert.DoesNotContain("merits", result.Content);
    }

    [Fact]
    public async Task AnotherPowerWithoutARankIsAnsweredAtRankOneHundred()
    {
        var result = await Ask(LiYongRui(), ("power", "Edmund Mahon"));

        Assert.StartsWith("Rank 100 with Edmund Mahon.", result.Content);
    }

    [Fact]
    public void AnUnknownRankWithTheOwnPowerAsksForOne()
    {
        var joined = Commander("""{"timestamp":"3311-01-01T00:00:00Z","event":"PowerplayJoin","Power":"Li Yong-Rui"}""");

        Assert.Contains("do not know your rank", PowerplayCapability.Describe(joined));
    }

    [Fact]
    public async Task ARankOutsideTheTableIsRefused()
    {
        Assert.True((await Ask(LiYongRui(), ("rank", "101"))).IsError);
        Assert.True((await Ask(LiYongRui(), ("rank", "0"))).IsError);
    }

    [Fact]
    public void AtMostThreeModulesAreNamedAndTheRestCounted()
    {
        var said = PowerplayCapability.Describe(LiYongRui(), "Li Yong-Rui", 90);

        Assert.Contains("Modules unlocked: ", said);
        Assert.Matches(@"Modules unlocked: [^,]+, [^,]+, [^,]+ and \d+ more\.", said);
    }

    [Fact]
    public void TheToolIsOpenToTheModel()
    {
        var tool = PowerplayCapability.Create(() => null).Tools.Single();

        Assert.False(tool.Protected);
    }
}
