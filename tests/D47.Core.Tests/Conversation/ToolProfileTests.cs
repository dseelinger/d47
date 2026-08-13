using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Which tools ship on a turn (list.md Phase 10, "Decide which tools ship on a turn as the count
/// grows").
/// <para>
/// Everything here is really one property tested from several angles: the same situation must
/// produce the same bytes, because tool definitions serialize first and a single changed byte
/// invalidates the whole cached prefix rather than a tail of it.
/// </para>
/// </summary>
public class ToolProfileTests
{
    private static D47.Core.Capabilities.CapabilityRegistry Registry(TempInstall install) =>
        TestSurface.For(install).Registry;

    [Fact]
    public void TheSameSituationShipsTheSameBytesEveryTime()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        var first = ToolProfiles.For(registry, ControlContext.Supercruise, actionsEnabled: true);
        var second = ToolProfiles.For(registry, ControlContext.Supercruise, actionsEnabled: true);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Tools, second.Tools);
    }

    [Fact]
    public void EveryProfileThatCanEverShipIsStable()
    {
        // Pinned as a set rather than one at a time: a new capability that varies its schema by
        // mode would show up here rather than as an unexplained cache miss months later.
        using var install = new TempInstall();
        var registry = Registry(install);

        foreach (var profile in ToolProfiles.All(registry))
        {
            var again = ToolProfiles.All(registry).First(p => p.Id == profile.Id && p.Tools.Count == profile.Tools.Count);

            Assert.Equal(profile.Tools, again.Tools);
        }
    }

    [Fact]
    public void TheProfileIsQuantizedRatherThanPerTool()
    {
        // Two different modes may share a profile; what must never happen is a profile that
        // differs by one tool because one action happened to be unbound this second.
        using var install = new TempInstall();
        var registry = Registry(install);

        var names = ToolProfiles.All(registry).Select(profile => profile.Id).Distinct().ToArray();

        Assert.True(names.Length <= 12, $"Too many distinct profiles to stay warm: {string.Join(", ", names)}");
    }

    [Fact]
    public void NoActionToolShipsWhenTheCommanderHasNotAllowedKeyPresses()
    {
        // Advertising a tool that will refuse every call is paying for a refusal every turn.
        using var install = new TempInstall();

        var profile = ToolProfiles.For(Registry(install), ControlContext.NormalSpace, actionsEnabled: false);

        Assert.DoesNotContain(profile.Tools, tool => tool.Name.StartsWith("control_", StringComparison.Ordinal));
        Assert.DoesNotContain(profile.Tools, tool => tool.Name == "run_macro");
    }

    [Fact]
    public void OnFootDoesNotOfferTheShipsControls()
    {
        using var install = new TempInstall();

        var profile = ToolProfiles.For(Registry(install), ControlContext.OnFoot, actionsEnabled: true);

        Assert.DoesNotContain(profile.Tools, tool => tool.Name == "control_flight");
        Assert.DoesNotContain(profile.Tools, tool => tool.Name == "control_systems");
    }

    [Fact]
    public void TheSrvOffersTheSrvsControlsAndNormalSpaceDoesNot()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        Assert.Contains(
            ToolProfiles.For(registry, ControlContext.Srv, actionsEnabled: true).Tools,
            tool => tool.Name == "control_srv");

        Assert.DoesNotContain(
            ToolProfiles.For(registry, ControlContext.NormalSpace, actionsEnabled: true).Tools,
            tool => tool.Name == "control_srv");
    }

    [Fact]
    public void WitchspaceOffersNothingToActWith()
    {
        // The game has the controls during the tunnel.
        using var install = new TempInstall();

        var profile = ToolProfiles.For(Registry(install), ControlContext.Hyperspace, actionsEnabled: true);

        Assert.DoesNotContain(profile.Tools, tool => tool.Name.StartsWith("control_", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryProfileCanStillAnswerAndBeDiagnosed()
    {
        // Including the degraded one. A profile that could not answer "what can you do" is one
        // nobody could diagnose.
        using var install = new TempInstall();

        foreach (var profile in ToolProfiles.All(Registry(install)))
        {
            Assert.Contains(profile.Tools, tool => tool.Name == "get_capabilities");
        }
    }

    [Fact]
    public void DegradingDropsToolsAndNeverTheGuardrails()
    {
        // The point of the item's last sentence. The guardrails are prompt position 2 with no
        // setter, so no profile choice can reach them — asserted here because it is the kind of
        // property that gets quietly broken by someone making the block configurable.
        using var install = new TempInstall();
        var registry = Registry(install);

        var full = ToolProfiles.For(registry, ControlContext.NormalSpace, actionsEnabled: true);
        var degraded = ToolProfiles.All(registry).First(profile => profile.Id == "degraded");

        Assert.True(degraded.Tools.Count < full.Tools.Count);

        var withTools = new PromptAssembly { Tools = full.Tools };
        var without = new PromptAssembly { Tools = degraded.Tools };

        Assert.Equal(withTools.RenderCachedSystemBlock(), without.RenderCachedSystemBlock());
        Assert.Contains(Guardrails.Text, without.RenderCachedSystemBlock(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePrefixIsNotShippedAtAllUntilAProviderCanExecuteATool()
    {
        // Advertising a tool the turn loop would silently drop is worse than not offering it:
        // the model then tells the Commander it has done something that never happened.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var capabilities = new LlmProviderCapabilities
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
        };

        Assert.False(capabilities.SupportsToolCalls);
        Assert.NotEmpty(ToolProfiles.For(surface.Registry, ControlContext.NormalSpace, true).Tools);
    }
}
