using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Zooming the panel and entering resize mode, with both hands on a stick and no model (#107).</summary>
public class ZoomAndResizeAreSaidFromInsideTheHeadsetTests
{
    private sealed record Fixture(
        SettingsService Settings,
        CapabilityRegistry Registry,
        KeywordRouter Router,
        List<bool> Resizes);

    private static Fixture Build(string mode)
    {
        var resizes = new List<bool>();
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
                    Resize = on =>
                    {
                        resizes.Add(on);
                        return on ? VrResizeOutcome.On : VrResizeOutcome.Off;
                    },
                }),
        ]);

        settings.Bind(registry);
        settings.Apply(VrCapability.EnabledKey, "true", SettingsCaller.Panel);
        settings.Apply(VrCapability.ModeKey, mode, SettingsCaller.Panel);

        return new Fixture(settings, registry, new KeywordRouter(registry), resizes);
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance, string tool)
    {
        var match = fixture.Router.MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal(tool, match.ToolName);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ZoomingInStepsThePanelOnScreenUpTheLadder()
    {
        var fixture = Build("full");

        var result = await Say(fixture, "zoom the panel in", "zoom_headset_panel");

        Assert.False(result.IsError);
        Assert.Equal(110, fixture.Settings.Current.Vr.Panel.Zoom);
        Assert.Equal(100, fixture.Settings.Current.Vr.Mini.Zoom);
    }

    [Fact]
    public async Task ZoomingOutAndResettingComeBackToOneHundred()
    {
        var fixture = Build("full");

        await Say(fixture, "zoom the panel out", "zoom_headset_panel");
        await Say(fixture, "panel zoom out", "zoom_headset_panel");
        Assert.Equal(80, fixture.Settings.Current.Vr.Panel.Zoom);

        await Say(fixture, "reset the panel zoom", "zoom_headset_panel");
        Assert.Equal(100, fixture.Settings.Current.Vr.Panel.Zoom);
    }

    [Fact]
    public async Task InMiniTheMiniPanelIsTheOneZoomed()
    {
        var fixture = Build("mini");

        await Say(fixture, "make the panel text bigger", "zoom_headset_panel");

        Assert.Equal(110, fixture.Settings.Current.Vr.Mini.Zoom);
        Assert.Equal(100, fixture.Settings.Current.Vr.Panel.Zoom);
    }

    [Fact]
    public async Task AtTheTopRungZoomingInSaysSo()
    {
        var fixture = Build("full");
        fixture.Settings.Apply("vr.panel.scale", "300", SettingsCaller.Panel);

        var result = await Say(fixture, "zoom the panel in", "zoom_headset_panel");

        Assert.False(result.IsError);
        Assert.Contains("largest", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResizeModeIsEnteredAndLeftByName()
    {
        var fixture = Build("full");

        await Say(fixture, "resize the panel", "resize_headset_panel");
        await Say(fixture, "stop resizing", "resize_headset_panel");

        Assert.Equal([true, false], fixture.Resizes);
    }

    [Fact]
    public void AQuestionAboutResizingIsNotAnInstructionToResize()
    {
        Assert.Null(Build("full").Router.MatchToolCommand("can you resize the panel"));
    }
}
