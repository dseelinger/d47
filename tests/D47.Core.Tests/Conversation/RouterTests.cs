using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class EffortRouterTests
{
    [Theory]
    [InlineData("where am I")]
    [InlineData("what's your status")]
    [InlineData("am I docked")]
    public void PlainLookupsGetTheCheapestSetting(string input) =>
        Assert.Equal(ThinkingEffort.Low, EffortRouter.ChooseFor(input));

    [Theory]
    [InlineData("carefully work out my jump range")]
    [InlineData("think hard about this build")]
    [InlineData("walk me through unlocking Palin")]
    public void AnExplicitAskToDeliberateOutranksEverything(string input) =>
        Assert.Equal(ThinkingEffort.Max, EffortRouter.ChooseFor(input));

    [Theory]
    [InlineData("plan the cheapest trade route from here")]
    [InlineData("compare the best loadout options for mining")]
    public void MultiConstraintWorkGetsHighEffort(string input) =>
        Assert.Equal(ThinkingEffort.High, EffortRouter.ChooseFor(input));

    [Fact]
    public void EmptyInputCostsNothingToDecide() =>
        Assert.Equal(ThinkingEffort.Low, EffortRouter.ChooseFor("   "));

    [Fact]
    public void TheChoiceIsDeterministic()
    {
        // Same input, same effort, every time — which is what makes the heuristic testable
        // rather than something to observe in production.
        const string Input = "plan the cheapest route to Colonia";

        Assert.Equal(EffortRouter.ChooseFor(Input), EffortRouter.ChooseFor(Input));
    }

    [Fact]
    public void ThereIsNoWayToChooseNoThinkingAtAll()
    {
        // The checklist allows low through max but no "off". Absence of the enum member is the
        // enforcement, so this asserts the shape of the type rather than a behaviour.
        var names = Enum.GetNames<ThinkingEffort>();

        Assert.DoesNotContain("None", names);
        Assert.DoesNotContain("Off", names);
        Assert.DoesNotContain("Disabled", names);
    }
}

public class KeywordRouterTests
{
    private static CapabilityRegistry Registry(TempInstall install, GameStateStore? gameState = null) =>
        CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths, new FakeVerbosityControl(), gameState ?? new GameStateStore(), "1.0.0"));

    [Theory]
    [InlineData("where am I", "journal")]
    [InlineData("what system is this", "journal")]
    [InlineData("give me a status report", "diagnostics")]
    public void DeclaredKeywordsRouteToTheirCapability(string input, string expectedCapability)
    {
        using var install = new TempInstall();

        var match = new KeywordRouter(Registry(install)).Match(input);

        Assert.NotNull(match);
        Assert.Equal(expectedCapability, match.CapabilityId);
    }

    [Fact]
    public void UnmatchedInputReturnsNothingRatherThanGuessing()
    {
        using var install = new TempInstall();

        Assert.Null(new KeywordRouter(Registry(install)).Match("compose a sonnet about hyperspace"));
    }

    [Fact]
    public void EmptyInputMatchesNothing()
    {
        using var install = new TempInstall();

        Assert.Null(new KeywordRouter(Registry(install)).Match("   "));
    }

    [Fact]
    public void OnlyZeroArgumentToolsAreReachable()
    {
        // Filling arguments from free text without a closed grammar is how a router starts
        // guessing, and guessing is the thing this path exists to avoid. set_log_verbosity takes
        // arguments, so "verbosity" must not route to it.
        using var install = new TempInstall();

        var match = new KeywordRouter(Registry(install)).Match("change the verbosity");

        Assert.True(
            match is null || match.ToolName != "set_log_verbosity",
            "An argument-taking tool must not be reachable from free text yet.");
    }

    [Fact]
    public void TheVocabularyIsAProjectionOfTheRegistry()
    {
        // Not a second list: every keyword the router matches is one a descriptor declared.
        using var install = new TempInstall();
        var registry = Registry(install);
        var router = new KeywordRouter(registry);

        foreach (var capability in registry.All)
        {
            foreach (var keyword in capability.Descriptor.Keywords)
            {
                var match = router.Match(keyword);
                Assert.NotNull(match);
            }
        }
    }
}
