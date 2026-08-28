using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Habits;

/// <summary>
/// The habits capability's surface (Phase 32).
/// <para>
/// <b>Nothing here reaches the model, and the assertions say so three ways.</b> Item 1 promises no
/// journal reaches a model; keeping the conclusions out of its reach too is the same boundary one
/// step further along — the component that reads hostile in-game text is not the one that gets
/// handed a list of the Commander's mistakes.
/// </para>
/// </summary>
public class HabitsCapabilityTests
{
    private static IReadOnlyList<string> Advertised(CapabilityRegistry registry, ControlContext context) =>
        [.. ToolProfiles.For(registry, context, actionsEnabled: true).Tools.Select(tool => tool.Name)];

    [Fact]
    public void NoneOfItIsAdvertisedInAnyMode()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        ControlContext[] modes =
        [
            ControlContext.None, ControlContext.Docked, ControlContext.Landed, ControlContext.NormalSpace,
            ControlContext.Supercruise, ControlContext.Hyperspace, ControlContext.Srv, ControlContext.OnFoot,
            ControlContext.Fighter,
        ];

        Assert.All(modes, mode =>
        {
            var advertised = Advertised(registry, mode);

            Assert.DoesNotContain("get_habits", advertised);
            Assert.DoesNotContain("explain_habit", advertised);
            Assert.DoesNotContain("dismiss_habit", advertised);
        });
    }

    [Fact]
    public void EveryToolIsProtected()
    {
        using var install = new TempInstall();

        var tools = TestSurface.For(install).Registry.Find(HabitsCapability.Id)!.Descriptor.Tools;

        Assert.Equal(3, tools.Count);
        Assert.All(tools, tool => Assert.True(tool.Protected));
    }

    [Fact]
    public void AskingWhatWasNoticedRoutesWithNoModelInThePath()
    {
        using var install = new TempInstall();
        var routed = TestSurface.For(install).Router.Match("what have you noticed about me");

        Assert.NotNull(routed);
        Assert.Equal("get_habits", routed.ToolName);
    }

    /// <summary>
    /// The two phrases the phase turns on. Both mean "the one you just said", which is the only
    /// kind of reference the router's closed grammar can carry — the wall Phase 31 hit from the
    /// other side when it found a memory was a sentence no phrase could hold.
    /// </summary>
    [Theory]
    [InlineData("why did you say that", "explain_habit")]
    [InlineData("why do you say that", "explain_habit")]
    [InlineData("stop telling me that", "dismiss_habit")]
    [InlineData("never mention that again", "dismiss_habit")]
    public void TheTwoPhrasesThatReferToTheLastThingSaidAreReachable(string phrase, string tool)
    {
        using var install = new TempInstall();

        var routed = TestSurface.For(install).Router.MatchToolCommand(phrase);

        Assert.NotNull(routed);
        Assert.Equal(tool, routed.ToolName);
        Assert.Empty(routed.Arguments.Values);
    }

    /// <summary>
    /// Whole-utterance matching, which is what keeps "why did you say that about the fuel" — a
    /// question for the model — from being answered by the wrong mechanism.
    /// </summary>
    [Fact]
    public void ALongerSentenceContainingThePhraseFallsThroughToTheModel()
    {
        using var install = new TempInstall();

        Assert.Null(TestSurface.For(install).Router.MatchToolCommand("why did you say that about the fuel"));
    }

    /// <summary>
    /// Reading 376 MB of journals is a thing a person asks for, and an Info row is the mechanism
    /// that makes it unreachable from the tool surface without a flag of its own.
    /// </summary>
    [Fact]
    public void MiningCannotBeStartedFromTheToolSurface()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = Assert.Single(
            surface.Registry.Find(HabitsCapability.Id)!.Descriptor.Settings,
            candidate => candidate.Key == HabitsCapability.StoreKey);

        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.False(surface.Settings.Apply(row.Key, "anything", SettingsCaller.Model).Ok);
        Assert.False(surface.Settings.Apply(row.Key, "anything", SettingsCaller.Panel).Ok);

        // And there is no phrase that starts one either. "Read my journals" is a button.
        Assert.Null(surface.Router.MatchToolCommand("read my journals"));
    }

    /// <summary>
    /// Throwing away what was noticed lives beside throwing away what was remembered, because a
    /// Commander who wants to be forgotten means both.
    /// </summary>
    [Fact]
    public void ForgettingWhatWasNoticedIsInThePrivacySection()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = Assert.Single(
            surface.Registry.Find(PrivacyCapability.Id)!.Descriptor.Settings,
            candidate => candidate.Key == PrivacyCapability.HabitsKey);

        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.False(surface.Settings.Apply(row.Key, "anything", SettingsCaller.Panel).Ok);
    }

    /// <summary>
    /// The store is a real thing on a machine and a null on the designer, and every tool has to
    /// answer for the second case rather than throwing into a panel that is trying to render.
    /// </summary>
    [Fact]
    public async Task WithNoStoreComposedEveryToolStillAnswers()
    {
        using var install = new TempInstall();
        var capability = TestSurface.For(install).Registry.Find(HabitsCapability.Id)!;

        foreach (var tool in capability.Descriptor.Tools)
        {
            var result = await tool.Handler(ToolArguments.Empty, CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(result.Content));
        }
    }
}
