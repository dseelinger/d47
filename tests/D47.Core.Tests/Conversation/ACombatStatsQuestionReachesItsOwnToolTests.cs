using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class ACombatStatsQuestionReachesItsOwnToolTests
{
    private static KeywordRouter Router() => new(TestSurface.For(new MemoryInstall()).Registry);

    [Theory]
    [InlineData("my combat stats", "get_combat_stats")]
    [InlineData("combat statistics", "get_combat_stats")]
    [InlineData("my kill stats", "get_combat_stats")]
    [InlineData("my kills", "get_combat_stats")]
    [InlineData("where did my money go", "get_credit_ledger")]
    [InlineData("where did my credits go", "get_credit_ledger")]
    [InlineData("my earnings", "get_credit_ledger")]
    [InlineData("session spend", "get_model_status")]
    [InlineData("my career stats", "get_commander_statistics")]
    [InlineData("this session", "get_session_summary")]
    [InlineData("my bounties", "get_crime_status")]
    [InlineData("my ship", "get_ship")]
    public void TheQuestionReachesTheToolThatAnswersIt(string asked, string tool)
    {
        var match = Router().Match(asked);

        Assert.NotNull(match);
        Assert.Equal(tool, match!.ToolName);
    }

    [Fact]
    public void TheModelMayCallTheCreditLedgerTool()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var tool = registry.All.SelectMany(capability => capability.Descriptor.Tools).Single(tool => tool.Name == "get_credit_ledger");

        Assert.False(tool.Protected);
    }

    [Fact]
    public void TheModelMayCallTheTool()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var tool = registry.All.SelectMany(capability => capability.Descriptor.Tools).Single(tool => tool.Name == "get_combat_stats");

        Assert.False(tool.Protected);
    }
}
