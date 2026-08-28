using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Memory;

/// <summary>
/// The memory capability's surface (Phase 31).
/// <para>
/// The interesting assertions are all about <b>which of the three tools the model can see</b>, and
/// that is arithmetic as much as it is safety: the SRV profile had 361 bytes of headroom when this
/// phase began, so one tool is advertised and two are not.
/// </para>
/// </summary>
public class MemoryCapabilityTests
{
    private static CapabilityRegistry Registry(TempInstall install) => TestSurface.For(install).Registry;

    private static IReadOnlyList<string> Advertised(CapabilityRegistry registry, ControlContext context) =>
        [.. ToolProfiles.For(registry, context, actionsEnabled: true).Tools.Select(tool => tool.Name)];

    /// <summary>
    /// The write path is the one the headroom was spent on: a store the model cannot write to only
    /// ever fills up when somebody opens a panel, and the moment a Commander says something worth
    /// keeping is a moment they are talking rather than clicking.
    /// </summary>
    [Fact]
    public void TheWritePathIsOfferedToTheModelInEveryOrdinaryMode()
    {
        using var install = new TempInstall();
        var registry = Registry(install);

        ControlContext[] modes =
        [
            ControlContext.None, ControlContext.Docked, ControlContext.Landed, ControlContext.NormalSpace,
            ControlContext.Supercruise, ControlContext.Hyperspace, ControlContext.Srv, ControlContext.OnFoot,
            ControlContext.Fighter,
        ];

        Assert.All(modes, mode => Assert.Contains("remember_about_me", Advertised(registry, mode)));
    }

    /// <summary>
    /// And the other two are not — which is what keeps the profile under the ceiling. Reading the
    /// store back needs no model in the path at all: the phrases below reach it directly.
    /// </summary>
    [Fact]
    public void ReadingAndForgettingAreNotAdvertisedAtAll()
    {
        using var install = new TempInstall();
        var advertised = Advertised(Registry(install), ControlContext.Srv);

        Assert.DoesNotContain("get_memories", advertised);
        Assert.DoesNotContain("forget_memory", advertised);
    }

    [Fact]
    public void AskingWhatIsRememberedRoutesWithNoModelInThePath()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var routed = surface.Router.Match("what do you remember about me");

        Assert.NotNull(routed);
        Assert.Equal("get_memories", routed.ToolName);
    }

    /// <summary>
    /// Emptying is the one action in the phase that cannot be undone, so it has no phrase at all —
    /// "forget everything about me" is a sentence a transcriber can produce out of a misheard one.
    /// </summary>
    [Fact]
    public void ThereIsNoWayToEmptyTheStoreByVoiceOrByTool()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Null(surface.Router.Match("forget everything about me"));
        Assert.Null(surface.Router.Match("forget everything you know about me"));

        // And the button that does it is an Info row, which SettingsService.Apply refuses outright.
        var row = Assert.Single(
            surface.Registry.Find(PrivacyCapability.Id)!.Descriptor.Settings,
            candidate => candidate.Key == PrivacyCapability.MemoryKey);

        Assert.Equal(SettingKind.Info, row.Kind);

        // Refused for the panel as well as for the model, because an Info row has nothing to set.
        // The button beside it is the way, and a button is not on the tool surface.
        Assert.False(surface.Settings.Apply(row.Key, "anything", SettingsCaller.Model).Ok);
        Assert.False(surface.Settings.Apply(row.Key, "anything", SettingsCaller.Panel).Ok);
    }

    /// <summary>
    /// The write tool takes a sentence and nothing else. No tier, because a call that could declare
    /// its own entry the Commander's word is a call a hostile in-game message could make.
    /// </summary>
    [Fact]
    public void TheWriteToolCannotChooseHowMuchToBeBelieved()
    {
        using var install = new TempInstall();

        var tool = Assert.Single(
            Registry(install).Find(MemoryCapability.Id)!.Descriptor.Tools,
            candidate => candidate.Name == "remember_about_me");

        Assert.Equal(["fact"], tool.Parameters.Select(parameter => parameter.Name));
        Assert.False(tool.Protected);
    }

    [Fact]
    public void TheExpirySettingOffersNeverAsARealChoice()
    {
        using var install = new TempInstall();

        var row = Assert.Single(
            Registry(install).Find(MemoryCapability.Id)!.Descriptor.Settings,
            candidate => candidate.Key == MemoryCapability.ExpiryKey);

        Assert.Contains("0", row.Choices);
        Assert.Equal(TimeSpan.Zero, MemoryCapability.ExpiryOf(new D47.Core.Configuration.MemorySettings { ExpiryDays = 0 }));
        Assert.Equal(
            TimeSpan.FromDays(90),
            MemoryCapability.ExpiryOf(new D47.Core.Configuration.MemorySettings()));
    }

    /// <summary>
    /// The store is a real thing on a machine and a null on the designer, and every tool has to
    /// answer for the second case rather than throwing into a panel that is trying to render.
    /// </summary>
    [Fact]
    public void WithNoStoreComposedEveryToolStillAnswers()
    {
        using var install = new TempInstall();
        var registry = Registry(install);
        var capability = registry.Find(MemoryCapability.Id)!;

        Assert.All(capability.Descriptor.Tools, tool =>
        {
            var result = tool.Handler(ToolArguments.Empty, CancellationToken.None).GetAwaiter().GetResult();
            Assert.False(string.IsNullOrWhiteSpace(result.Content));
        });
    }
}
