using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Bringing Elite to the front.</summary>
public class FocusTheGameTests
{
    private static (CapabilityRegistry Registry, List<int> Calls) Built(FocusResult answer)
    {
        var calls = new List<int>();

        var registry = CapabilityRegistry.Build(
            [
                FocusCapability.Create(() =>
                {
                    calls.Add(1);
                    return Task.FromResult(answer);
                }),
            ]);

        return (registry, calls);
    }

    /// <summary>Every published phrase routes here, with no model in the path.</summary>
    [Fact]
    public void EveryPhraseReachesTheCapabilityWithNoModel()
    {
        var (registry, _) = Built(FocusResult.Raised);
        var router = new KeywordRouter(registry);

        Assert.All(
            FocusCapability.Phrases,
            phrase =>
            {
                var match = router.Match(phrase, InputSource.Spoken);

                Assert.NotNull(match);
                Assert.Equal(FocusCapability.Id, match!.CapabilityId);
                Assert.Equal("focus_the_game", match.ToolName);
            });
    }

    /// <summary>Not reachable by the model, which is the whole modality decision.</summary>
    [Fact]
    public void TheModelIsNotOfferedIt()
    {
        var (registry, _) = Built(FocusResult.Raised);

        var tool = registry.Find(FocusCapability.Id)!.Descriptor.Tools.Single();

        Assert.True(tool.Protected, "the focus tool is reachable by the model");
    }

    /// <summary>The request asked for "Elite" on its own.</summary>
    [Theory]
    [InlineData("what is my elite rank in combat")]
    [InlineData("am I elite yet")]
    [InlineData("how far off elite am I in exploration")]
    [InlineData("elite")]
    public void AQuestionAboutTheEliteRankIsNotAnInstructionToRaiseTheGame(string asked)
    {
        var (registry, _) = Built(FocusResult.Raised);
        var router = new KeywordRouter(registry);

        Assert.Null(router.Match(asked, InputSource.Spoken));
    }

    /// <summary>A refusal is spoken.</summary>
    [Theory]
    [InlineData(FocusResult.Refused, "would not let me")]
    [InlineData(FocusResult.NotRunning, "cannot find Elite")]
    [InlineData(FocusResult.AlreadyThere, "already in front")]
    [InlineData(FocusResult.Raised, "in front")]
    public void EveryOutcomeSaysSomething(FocusResult result, string expected)
    {
        Assert.Contains(expected, FocusCapability.Describe(result), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The phrase actually runs it, rather than merely matching.</summary>
    [Fact]
    public async Task SayingItRaisesTheGameOnce()
    {
        var (registry, calls) = Built(FocusResult.Raised);

        var result = await registry.InvokeAsync("focus_the_game", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Single(calls);
        Assert.Contains("in front", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// With nothing composed to raise the window it says so rather than going quiet — a capability
    /// being off looks like an answer that cannot act, not like an absence.
    /// </summary>
    [Fact]
    public async Task WithNoWayToReachTheWindowItSaysSo()
    {
        var registry = CapabilityRegistry.Build([FocusCapability.Create(null)]);

        var result = await registry.InvokeAsync("focus_the_game", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("no way to reach", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
