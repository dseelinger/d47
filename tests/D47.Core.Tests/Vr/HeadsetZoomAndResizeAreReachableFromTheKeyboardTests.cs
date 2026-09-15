using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Zoom and resize mode, reachable by a system-wide gesture with no controller (#189).</summary>
public class HeadsetZoomAndResizeAreReachableFromTheKeyboardTests
{
    [Fact]
    public void AFreshInstallCarriesTheFourDefaults()
    {
        var hotkeys = new D47Settings().Hotkeys;

        Assert.Equal("Ctrl+Alt+OemPlus", hotkeys.ZoomHeadsetIn);
        Assert.Equal("Ctrl+Alt+OemMinus", hotkeys.ZoomHeadsetOut);
        Assert.Equal("Ctrl+Alt+D0", hotkeys.ResetHeadsetZoom);
        Assert.Equal("Ctrl+Alt+S", hotkeys.ResizeHeadsetPanel);
    }

    [Fact]
    public void TheFourRowsAreOnTheHeadsetPageAndSystemWide()
    {
        var registry = Build().Registry;
        var rows = registry.Find(VrCapability.Id)!.Descriptor.Settings;

        foreach (var key in new[]
                 {
                     VrCapability.ZoomInHotkeyKey,
                     VrCapability.ZoomOutHotkeyKey,
                     VrCapability.ResetZoomHotkeyKey,
                     VrCapability.ResizeHotkeyKey,
                 })
        {
            var row = Assert.Single(rows, r => r.Key == key);
            Assert.True(row.SystemWide, $"{key} is not marked system-wide.");
            Assert.True(row.Protected, $"{key} is not protected from the model.");
        }
    }

    [Fact]
    public void StepZoomMovesTheLadderForAHotkeyCaller()
    {
        var settings = Build().Settings;

        var step = VrCapability.StepZoom(settings, "in", SettingsCaller.Hotkey);

        Assert.Equal(SettingApplyStatus.Applied, step.Applied.Status);
        Assert.Equal(110, step.Zoom);
        Assert.Equal(110, settings.Current.Vr.Panel.Zoom);
    }

    [Fact]
    public void StepZoomResetGoesBackToTheDefaultRung()
    {
        var settings = Build().Settings;
        settings.Apply("vr.panel.scale", "150", SettingsCaller.Panel);

        var step = VrCapability.StepZoom(settings, "reset", SettingsCaller.Hotkey);

        Assert.Equal(SettingApplyStatus.Applied, step.Applied.Status);
        Assert.Equal(ZoomLadder.Default, step.Zoom);
        Assert.Equal(ZoomLadder.Default, settings.Current.Vr.Panel.Zoom);
    }

    [Fact]
    public void StepZoomAtTheTopRungIsUnchanged()
    {
        var settings = Build().Settings;
        settings.Apply("vr.panel.scale", "300", SettingsCaller.Panel);

        var step = VrCapability.StepZoom(settings, "in", SettingsCaller.Hotkey);

        Assert.Equal(SettingApplyStatus.Unchanged, step.Applied.Status);
        Assert.Equal(300, step.Zoom);
    }

    private sealed record Fixture(SettingsService Settings, CapabilityRegistry Registry);

    private static Fixture Build()
    {
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
                }),
        ]);

        settings.Bind(registry);
        settings.Apply(VrCapability.EnabledKey, "true", SettingsCaller.Panel);
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        return new Fixture(settings, registry);
    }
}
