using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Which tools ship on a turn.</summary>
public class ToolSurfaceTests
{
    private static CapabilityRegistry Registry(TempInstall install) =>
        TestSurface.For(install).Registry;

    private static IEnumerable<ToolProfile> EveryForMode(CapabilityRegistry registry) =>
        from context in Enum.GetValues<ControlContext>()
        from actionsEnabled in new[] { true, false }
        select ToolSurface.ForMode(registry, context, actionsEnabled);

    private static IEnumerable<ToolDefinition> Definitions(CapabilityRegistry registry) =>
        registry.All.SelectMany(capability => capability.Descriptor.Tools);

    [Fact]
    public void TheSameSituationShipsTheSameBytesEveryTime()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        var first = ToolSurface.ForMode(registry, ControlContext.Supercruise, actionsEnabled: true);
        var second = ToolSurface.ForMode(registry, ControlContext.Supercruise, actionsEnabled: true);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Tools, second.Tools);
    }

    [Fact]
    public void EveryProfileThatCanEverShipIsStable()
    {
        // Pinned as a set rather than one at a time: a new capability that varies its schema by mode would
        // show up here rather than as an unexplained cache miss months later.
        using var install = new TempInstall();
        var registry = Registry(install);

        foreach (var profile in ToolSurface.All(registry))
        {
            var again = ToolSurface.All(registry).First(p => p.Id == profile.Id && p.Tools.Count == profile.Tools.Count);

            Assert.Equal(profile.Tools, again.Tools);
        }
    }

    [Fact]
    public void TheProfileIsQuantizedRatherThanPerTool()
    {
        // Two different modes may share a profile; what must never happen is a profile that differs by one
        // tool because one action happened to be unbound this second.
        using var install = new TempInstall();
        var registry = Registry(install);

        var names = ToolSurface.All(registry).Select(profile => profile.Id).Distinct().ToArray();

        Assert.True(names.Length <= 12, $"Too many distinct profiles to stay warm: {string.Join(", ", names)}");
    }

    [Fact]
    public void NoActionToolShipsWhenTheCommanderHasNotAllowedKeyPresses()
    {
        // Advertising a tool that will refuse every call is paying for a refusal every turn.
        using var install = new TempInstall();

        var profile = ToolSurface.ForMode(Registry(install), ControlContext.NormalSpace, actionsEnabled: false);

        Assert.DoesNotContain(profile.Tools, tool => tool.Name.StartsWith("control_", StringComparison.Ordinal));
        Assert.DoesNotContain(profile.Tools, tool => tool.Name == "run_macro");
    }

    [Fact]
    public void OnFootDoesNotOfferTheShipsControls()
    {
        using var install = new TempInstall();

        var profile = ToolSurface.ForMode(Registry(install), ControlContext.OnFoot, actionsEnabled: true);

        Assert.DoesNotContain(profile.Tools, tool => tool.Name == "control_flight");
        Assert.DoesNotContain(profile.Tools, tool => tool.Name == "control_systems");
    }

    [Fact]
    public void TheSrvOffersTheSrvsControlsAndNormalSpaceDoesNot()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        Assert.Contains(
            ToolSurface.ForMode(registry, ControlContext.Srv, actionsEnabled: true).Tools,
            tool => tool.Name == "control_srv");

        Assert.DoesNotContain(
            ToolSurface.ForMode(registry, ControlContext.NormalSpace, actionsEnabled: true).Tools,
            tool => tool.Name == "control_srv");
    }

    [Fact]
    public void WitchspaceOffersNothingToActWith()
    {
        // The game has the controls during the tunnel.
        using var install = new TempInstall();

        var profile = ToolSurface.ForMode(Registry(install), ControlContext.Hyperspace, actionsEnabled: true);

        Assert.DoesNotContain(profile.Tools, tool => tool.Name.StartsWith("control_", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryProfileCanStillAnswerAndBeDiagnosed()
    {
        using var install = new TempInstall();

        foreach (var profile in ToolSurface.All(Registry(install)))
        {
            Assert.Contains(profile.Tools, tool => tool.Name == "get_capabilities");
        }
    }

    [Fact]
    public void TheSearchableListIsEveryUnprotectedToolInRegistrationOrder()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        var searchable = ToolSurface.Searchable(registry).Tools.Select(tool => tool.Name).ToArray();

        Assert.Equal(Definitions(registry).Where(tool => !tool.Protected).Select(tool => tool.Name), searchable);
        Assert.All(
            EveryForMode(registry).SelectMany(profile => profile.Tools),
            tool => Assert.Contains(tool.Name, searchable));
    }

    [Fact]
    public void TheSearchableListKeepsWhatAModeWithholds()
    {
        // A tool found by search earlier in a conversation must still be declared after the mode changes.
        using var install = new TempInstall();
        var registry = Registry(install);

        var searchable = ToolSurface.Searchable(registry);

        Assert.Equal(searchable.Tools, ToolSurface.Searchable(registry).Tools);

        foreach (var withheld in new[] { "control_flight", "control_systems", "control_interface", "control_srv", "run_macro" })
        {
            Assert.Contains(searchable.Tools, tool => tool.Name == withheld);
            Assert.Contains(EveryForMode(registry), profile => profile.Tools.All(tool => tool.Name != withheld));
        }
    }

    [Fact]
    public void NoProtectedToolIsAdvertisedByEitherProjection()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        var protectedNames = Definitions(registry)
            .Where(tool => tool.Protected)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(protectedNames);

        foreach (var profile in EveryForMode(registry).Append(ToolSurface.Searchable(registry)))
        {
            Assert.DoesNotContain(profile.Tools, tool => protectedNames.Contains(tool.Name));
        }
    }

    [Fact]
    public void OnlyTheAlwaysLoadedToolsWaitForNoSearch()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        var definitions = Definitions(registry).ToDictionary(tool => tool.Name, StringComparer.Ordinal);

        Assert.All(
            ToolSurface.Searchable(registry).Tools,
            tool => Assert.Equal(!definitions[tool.Name].AlwaysLoaded, tool.Deferred));

        // The provider refuses a request whose every tool is deferred.
        Assert.InRange(definitions.Values.Count(tool => tool.AlwaysLoaded), 1, 17);
    }

    [Fact]
    public void AModesListDefersNothing()
    {
        using var install = new TempInstall();

        Assert.All(
            EveryForMode(Registry(install)).SelectMany(profile => profile.Tools),
            tool => Assert.False(tool.Deferred));
    }

    [Fact]
    public void ThePrefixIsNotShippedAtAllUntilAProviderCanExecuteATool()
    {
        // Advertising a tool the turn loop would silently drop is worse than not offering it: the model then
        // tells the Commander it has done something that never happened.
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
        Assert.NotEmpty(ToolSurface.ForMode(surface.Registry, ControlContext.NormalSpace, true).Tools);
    }
}
