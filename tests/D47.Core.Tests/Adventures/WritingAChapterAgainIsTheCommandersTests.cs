using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>"write this chapter again" reaches its tool without a model call, and only the Commander can call it.</summary>
public sealed class WritingAChapterAgainIsTheCommandersTests
{
    private static CapabilityRegistry Registry(AdventureCapability.AdventureDesk desk) =>
        CapabilityRegistry.Build([AdventureCapability.Create(desk: desk)]);

    [Fact]
    public void ThePhraseReachesItsToolOnTheKeywordPath()
    {
        var router = new KeywordRouter(Registry(new AdventureCapability.AdventureDesk()));

        Assert.Equal(AdventureCapability.RewriteChapterTool, router.MatchToolCommand("write this chapter again")?.ToolName);
    }

    [Fact]
    public async Task TheModelIsRefusedAndTheCommanderReachesTheRewrite()
    {
        var rewrites = 0;
        var registry = Registry(new AdventureCapability.AdventureDesk { RewriteChapter = () => { rewrites++; return null; } });

        var refused = await registry.InvokeAsync(
            AdventureCapability.RewriteChapterTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Equal(0, rewrites);

        var result = await registry.InvokeAsync(
            AdventureCapability.RewriteChapterTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(1, rewrites);
    }

    [Fact]
    public async Task WhyTheRewriteIsShutIsWhatTheCommanderHears()
    {
        const string Shut = "Resume the story first.";
        var registry = Registry(new AdventureCapability.AdventureDesk { RewriteChapter = () => Shut });

        var result = await registry.InvokeAsync(
            AdventureCapability.RewriteChapterTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(Shut, result.Content);
    }
}
