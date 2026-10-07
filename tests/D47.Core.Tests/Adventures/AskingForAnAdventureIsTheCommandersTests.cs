using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>"ask for an adventure" reaches its tool with or without a draft, and only the Commander can call it.</summary>
public sealed class AskingForAnAdventureIsTheCommandersTests
{
    private static CapabilityRegistry Registry(AdventureCapability.AdventureDesk desk) =>
        CapabilityRegistry.Build([AdventureCapability.Create(desk: desk)]);

    [Fact]
    public void ThePhraseReachesTheToolWithNoDraft()
    {
        var router = new KeywordRouter(Registry(new AdventureCapability.AdventureDesk()));

        Assert.Equal(AdventureCapability.AskTool, router.MatchToolCommand("ask for an adventure")?.ToolName);
    }

    [Fact]
    public void ItIsNotAdvertisedToTheModel()
    {
        var advertised = ToolSurface.All(Registry(new AdventureCapability.AdventureDesk()))
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name);

        Assert.DoesNotContain(AdventureCapability.AskTool, advertised);
    }

    [Fact]
    public async Task TheModelIsRefusedAndTheCommanderIsAskedForABrief()
    {
        var asked = 0;
        var registry = Registry(new AdventureCapability.AdventureDesk { Ask = () => { asked++; return null; } });

        var refused = await registry.InvokeAsync(
            AdventureCapability.AskTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Equal(0, asked);

        var result = await registry.InvokeAsync(
            AdventureCapability.AskTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("What should it be about?", result.Content);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task WhyAskingIsShutIsWhatTheCommanderHears()
    {
        const string Shut = "Asking for one needs a language model, and none is configured.";
        var registry = Registry(new AdventureCapability.AdventureDesk { Ask = () => Shut });

        var result = await registry.InvokeAsync(
            AdventureCapability.AskTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(Shut, result.Content);
    }
}
