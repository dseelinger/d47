using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Both headset panels have a resolution of their own, and a dragged one is kept as dragged (#107).</summary>
public class TheMiniPanelsPixelsAreTheCommandersTests
{
    private static SettingsService Settings(string mode)
    {
        var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        settings.Bind(Capabilities.CapabilityRegistry.Build(
        [
            VrCapability.Create(
                settings,
                new VrCapability.HeadsetSurface
                {
                    Report = () => (D47.Core.Vr.VrState.Unavailable, "No runtime in a test."),
                    Nudge = (_, _) => D47.Core.Vr.VrNudgeOutcome.NoHeadset,
                }),
        ]));
        settings.Apply(VrCapability.EnabledKey, "true", SettingsCaller.Panel);
        settings.Apply(VrCapability.ModeKey, mode, SettingsCaller.Panel);

        return settings;
    }

    private static string? Read(SettingsService settings, string key) =>
        settings.Find(key)!.Binding!.Read(settings.Current);

    [Fact]
    public void TheMiniPanelHasAResolutionRowAndItStartsAtTheMiniSize()
    {
        var settings = Settings("mini");

        Assert.Equal(PanelResolution.Describe(PanelResolution.Mini), Read(settings, "vr.mini.resolution"));
        Assert.Equal(PanelResolution.Describe(PanelResolution.Mini), Read(settings, "vr.current.resolution"));
        Assert.Equal(PanelResolution.Describe(PanelResolution.Default), Read(settings, "vr.panel.resolution"));
    }

    [Fact]
    public void SettingTheMiniPanelsPixelsLeavesTheBigPanelAlone()
    {
        var settings = Settings("mini");

        var applied = settings.Apply("vr.current.resolution", "1024x640", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, applied.Status);
        Assert.Equal((1024, 640), settings.Current.Vr.Mini.ResolutionOr(PanelResolution.Mini));
        Assert.Equal(string.Empty, settings.Current.Vr.Panel.Pixels);
    }

    /// <summary>A drag produces a size no ladder has, and snapping it would change the panel's shape.</summary>
    [Fact]
    public void ADraggedSizeIsKeptRatherThanSnappedToARung()
    {
        var settings = Settings("full");

        settings.Replace("a test drag", s => s with { Vr = s.Vr with { Panel = s.Vr.Panel with { Pixels = "1333x517" } } });

        Assert.Equal("1333x517", Read(settings, "vr.panel.resolution"));
        Assert.Contains("1333x517", settings.Find("vr.panel.resolution")!.ChoicesFor(settings.Current));
    }
}
