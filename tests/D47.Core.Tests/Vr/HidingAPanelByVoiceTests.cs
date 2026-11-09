using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>"Hide VR panel" and "hide 2D panel" reach their targets with no model in the path (#2245).</summary>
public class HidingAPanelByVoiceTests
{
    private sealed record Fixture(CapabilityRegistry Registry, KeywordRouter Router, List<bool> Hidden);

    private static Fixture Build()
    {
        var hidden = new List<bool>();
        var settings = TestSurface.SettingsFor(new MemoryInstall());

        var registry = CapabilityRegistry.Build(
        [
            VrCapability.Create(
                settings,
                new VrCapability.HeadsetSurface
                {
                    Report = () => (VrState.Active, null),
                    Nudge = (_, _) => VrNudgeOutcome.Moved,
                    SetPanelHidden = hidden.Add,
                }),
            InterfaceCapability.Create(),
        ]);

        return new Fixture(registry, new KeywordRouter(registry), hidden);
    }

    [Theory]
    [InlineData("hide VR panel", false)]
    [InlineData("hide the VR overlay", false)]
    [InlineData("show VR panel", true)]
    [InlineData("show the headset panel", true)]
    public async Task APanelPhraseHidesOrShowsTheHeadsetPanel(string said, bool shown)
    {
        var fixture = Build();

        var match = fixture.Router.MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal("show_headset_panel", match.ToolName);

        var result = await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(!shown, Assert.Single(fixture.Hidden));
        Assert.Equal(shown ? "The headset panel is showing." : "The headset panel is hidden.", result.Content);
    }

    [Theory]
    [InlineData("Hide 2D panel", "false")]
    [InlineData("hide 2-D overlay", "false")]
    [InlineData("show two D overlay", "true")]
    [InlineData("show the 2D panel", "true")]
    public void ATwoDPhraseSetsShowTheOverlay(string said, string value)
    {
        var match = Build().Router.MatchSetting(said);

        Assert.NotNull(match);
        Assert.Equal(InterfaceCapability.OverlayKey, match.Row.Key);
        Assert.Equal(value, match.Value);
    }
}
