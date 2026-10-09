using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>how_to_get adds the rank a Power unlocks a Powerplay module at, said against the pledge (#598).</summary>
[Trait("Category", "Integration")]
public class APowerplayModuleNamesItsUnlockRankTests
{
    private const string Gate = "It needs a Powerplay pledge.";

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

    private static async Task<string> Ask(string item, CommanderGameState? state)
    {
        using var install = new TempInstall();

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(null, () => "Sol", TestSurface.For(install).Settings, gameState: () => state)]);

        var result = await registry.InvokeAsync(
            "how_to_get", new ToolArguments(new Dictionary<string, string> { ["item"] = item }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    [Fact]
    public async Task APledgedCommanderBelowTheRankIsToldTheMeritsShort()
    {
        var short34 = PowerplayRanks.MeritsNeeded(34)!.Value - 1_000;

        var said = await Ask("Pack-Hound Missile Rack", Commander(Powerplay("Li Yong-Rui", 8, 1_000)));

        Assert.EndsWith($"{Gate} Li Yong-Rui unlocks it at rank 34; you are rank 8, {short34:N0} merits short.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APledgedCommanderBelowTheRankWithNoMeritsReadIsNotToldAShortfall()
    {
        var said = await Ask(
            "Pack-Hound Missile Rack",
            Commander("""{"timestamp":"3311-01-01T00:00:00Z","event":"PowerplayJoin","Power":"Li Yong-Rui"}""", """{"timestamp":"3311-01-01T00:01:00Z","event":"PowerplayRank","Power":"Li Yong-Rui","Rank":8}"""));

        Assert.EndsWith($"{Gate} Li Yong-Rui unlocks it at rank 34; you are rank 8.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APledgedCommanderPastTheRankIsToldSo()
    {
        var said = await Ask("Pack-Hound Missile Rack", Commander(Powerplay("Li Yong-Rui", 40, 5_000_000)));

        Assert.EndsWith($"{Gate} Li Yong-Rui unlocks it at rank 34, and you are rank 40.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APledgeWithNoRankYetIsToldOnlyTheUnlockRank()
    {
        var said = await Ask(
            "Pack-Hound Missile Rack",
            Commander("""{"timestamp":"3311-01-01T00:00:00Z","event":"PowerplayJoin","Power":"Li Yong-Rui"}"""));

        Assert.EndsWith($"{Gate} Li Yong-Rui unlocks it at rank 34.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnpledgedCommanderIsToldTheSoonestPower()
    {
        var expected = $"{Gate} Every Power unlocks it; Li Yong-Rui soonest, at rank 34.";

        Assert.EndsWith(expected, await Ask("Pack-Hound Missile Rack", null), StringComparison.Ordinal);

        var left = Commander(
            Powerplay("Li Yong-Rui", 8, 1_000),
            """{"timestamp":"3311-01-01T00:01:00Z","event":"PowerplayLeave","Power":"Li Yong-Rui"}""");

        Assert.EndsWith(expected, await Ask("Pack-Hound Missile Rack", left), StringComparison.Ordinal);
    }

    [Fact]
    public async Task APledgeToAPowerTheTableDoesNotListLeavesTheGateAlone()
    {
        var said = await Ask("Pack-Hound Missile Rack", Commander(Powerplay("Nobody Atall", 8, 1_000)));

        Assert.EndsWith(Gate, said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Prismatic Shield Generator")]
    [InlineData("Pack-Hound Missile Rack")]
    public async Task EveryPowerplayModuleSaysItsRank(string module)
    {
        var said = await Ask(module, null);

        Assert.Contains("Every Power unlocks it;", said, StringComparison.Ordinal);
        Assert.Contains("soonest, at rank 34.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUngatedModuleIsAnsweredAsBefore()
    {
        var said = await Ask("Frame Shift Drive", Commander(Powerplay("Li Yong-Rui", 8, 1_000)));

        Assert.DoesNotContain("unlocks", said, StringComparison.Ordinal);
    }
}
