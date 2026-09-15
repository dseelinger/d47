using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>"Reset the panel": the phrase reaching it with no model, and the words for the outcome (#162).</summary>
public class ResettingThePanelsPositionTests
{
    private sealed record Fixture(CapabilityRegistry Registry, KeywordRouter Router, List<string> Reset);

    private static Fixture Build(VrResetOutcome outcome = VrResetOutcome.Reset)
    {
        var reset = new List<string>();
        var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        var registry = CapabilityRegistry.Build(
        [
            VrCapability.Create(
                settings,
                new VrCapability.HeadsetSurface
                {
                    Report = () => (VrState.Active, null),
                    Nudge = (_, _) => VrNudgeOutcome.Moved,
                    ResetPlacement = slot =>
                    {
                        reset.Add(slot);
                        return outcome;
                    },
                }),
        ]);

        return new Fixture(registry, new KeywordRouter(registry), reset);
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance)
    {
        var match = fixture.Router.MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal("restore_headset_panel_position", match.ToolName);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("reset the panel")]
    [InlineData("reset the VR panel")]
    [InlineData("reset panel position")]
    public async Task SayingItResetsThePanelOnScreenWithNoModelInThePath(string said)
    {
        var fixture = Build();

        var result = await Say(fixture, said);

        Assert.False(result.IsError);
        Assert.Equal([VrCapability.CurrentSlot], fixture.Reset);
    }

    [Fact]
    public async Task WithNoHeadsetSessionItStillSaysTheResetHappened()
    {
        var result = await Say(Build(VrResetOutcome.ResetNoHeadset), "reset the panel");

        Assert.False(result.IsError);
        Assert.Contains("attaches", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithASessionItSaysThePanelIsBack()
    {
        var result = await Say(Build(VrResetOutcome.Reset), "reset the panel");

        Assert.False(result.IsError);
        Assert.Contains("back where a fresh install puts it", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Panel you are looking at placement", VrCapability.CurrentSlot)]
    [InlineData("Panel placement", VrCapability.PanelSlot)]
    [InlineData("Mini panel placement", VrCapability.MiniSlot)]
    [InlineData("Captions", null)]
    public void EachPlacementHeadingNamesItsOwnSlot(string group, string? slot)
    {
        Assert.Equal(slot, VrCapability.SlotForPlacementGroup(group));
    }
}
