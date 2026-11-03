using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.TurnRuns;
using static D47.Core.Tests.Modules.FakeModuleTools;

namespace D47.Core.Tests.Modules;

/// <summary>A phrase a module gives runs its tool on the model-free path, but never one d47 or the Commander already holds.</summary>
public class AModulePhraseRunsItsToolWithNoModelTests
{
    private static CapabilityRegistry Registry() => CapabilityRegistry.Build(
    [
        new CapabilityDescriptor
        {
            Id = "board",
            Group = "Ship",
            Name = "Board",
            Summary = "The board.",
            Tools =
            [
                new ToolDefinition
                {
                    Name = "open_board",
                    Description = "Opens the board.",
                    Commands = [new ToolCommandPhrase("open the board")],
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok("The built-in board.")),
                },
            ],
        },
    ]);

    [Fact]
    public async Task APhraseRunsTheToolWithNoModel()
    {
        var registry = Registry();
        var modules = new FakeModuleTools().Offering("bounty", "Bounty Board", Tool("bounty", "open", phrases: "bounty board status"));
        var loop = TurnLoops.Build(registry, new ThrowingLlmProvider(), modules.AttachTo(registry));

        var (result, spoken) = await RunWithTextAsync(loop, "bounty board status");

        Assert.Equal(TurnRoute.ActionCommand, result.Route);
        Assert.Equal("Three bounties open.", spoken);
        Assert.Equal(("bounty", "open"), (modules.Calls.Single().ModuleId, modules.Calls.Single().Tool));
    }

    [Fact]
    public async Task AModulePhraseEqualToABuiltInPhraseDoesNotTakeItOver()
    {
        var registry = Registry();
        var modules = new FakeModuleTools().Offering("bounty", "Bounty Board", Tool("bounty", "open", phrases: "Open the board"));
        var router = modules.AttachTo(registry);
        var loop = TurnLoops.Build(registry, new ThrowingLlmProvider(), router);

        var (_, spoken) = await RunWithTextAsync(loop, "open the board");

        Assert.Equal("The built-in board.", spoken);
        Assert.Empty(modules.Calls);
        Assert.DoesNotContain(router.Book.Entries, entry => entry.ToolName == "mod_bounty_open");
    }

    [Fact]
    public void AModulePhraseEqualToACommandersPhraseIsDropped()
    {
        var registry = Registry();
        var macro = new DynamicCommand("bounty board status", "board", "open_board", new Dictionary<string, string>());
        var modules = new FakeModuleTools().Offering("bounty", "Bounty Board", Tool("bounty", "open", phrases: "bounty board status"));
        var router = modules.AttachTo(registry, () => [macro]);

        Assert.Equal("open_board", router.MatchToolCommand("bounty board status")?.ToolName);
    }

    [Fact]
    public void TwoModulesGivingOnePhraseLeaveItWithTheFirstByModuleId()
    {
        var registry = Registry();
        var modules = new FakeModuleTools()
            .Offering("zeta", "Zeta", Tool("zeta", "ping", phrases: "check the board"))
            .Offering("bounty", "Bounty Board", Tool("bounty", "open", phrases: "check the board"));
        var router = modules.AttachTo(registry);

        Assert.Equal("mod_bounty_open", router.MatchToolCommand("check the board")?.ToolName);
    }
}
