using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>"change this objective" reaches its tool, "never ask this" reaches the refusal, and only the Commander can call either.</summary>
public sealed class ChangingAnObjectiveIsTheCommandersTests
{
    private static CapabilityRegistry Registry(AdventureCapability.AdventureDesk desk) =>
        CapabilityRegistry.Build([AdventureCapability.Create(desk: desk)]);

    [Theory]
    [InlineData("change this objective", AdventureCapability.ChangeObjectiveTool)]
    [InlineData("change the objective", AdventureCapability.ChangeObjectiveTool)]
    [InlineData("never ask this", AdventureCapability.RefuseBeatTool)]
    [InlineData("not for me", AdventureCapability.RefuseBeatTool)]
    public void ThePhraseReachesItsTool(string phrase, string tool)
    {
        var router = new KeywordRouter(Registry(new AdventureCapability.AdventureDesk()));

        Assert.Equal(tool, router.MatchToolCommand(phrase)?.ToolName);
    }

    [Fact]
    public void ItIsNotAdvertisedToTheModel()
    {
        var advertised = ToolSurface.All(Registry(new AdventureCapability.AdventureDesk()))
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name);

        Assert.DoesNotContain(AdventureCapability.ChangeObjectiveTool, advertised);
    }

    [Fact]
    public async Task TheModelIsRefusedAndTheCommanderIsAskedWhatTheyWouldRatherDo()
    {
        var opened = 0;
        var registry = Registry(new AdventureCapability.AdventureDesk { ChangeObjective = () => { opened++; return null; } });

        var refused = await registry.InvokeAsync(
            AdventureCapability.ChangeObjectiveTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Equal(0, opened);

        var result = await registry.InvokeAsync(
            AdventureCapability.ChangeObjectiveTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("What would you rather do?", result.Content);
        Assert.Equal(1, opened);
    }

    [Fact]
    public async Task WhyTheChangeIsShutIsWhatTheCommanderHears()
    {
        const string Shut = "The story is not waiting on an objective.";
        var registry = Registry(new AdventureCapability.AdventureDesk { ChangeObjective = () => Shut });

        var result = await registry.InvokeAsync(
            AdventureCapability.ChangeObjectiveTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(Shut, result.Content);
    }
}
