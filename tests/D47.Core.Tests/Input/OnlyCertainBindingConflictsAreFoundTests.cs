using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>D47's bindings that act while Elite is in front, checked against Elite's, with uncertain button matches left out (#2382).</summary>
public sealed class OnlyCertainBindingConflictsAreFoundTests
{
    private const string Path = @"C:\d47-test\conflicts.binds";

    private const string Throttle = "{wgi/nrid/throttle}";

    private static readonly IReadOnlyList<SettingRow> Rows =
        [.. TestSurface.For(new MemoryInstall(), everyOptionalSurface: true).Settings.Sections.SelectMany(section => section.Rows)];

    /// <summary>Overlay on Ctrl+Alt+O, the light on Ctrl+Alt+P, Scroll Lock for chaff, and two buttons on the throttle.</summary>
    private static EliteBinds Binds()
    {
        var files = new MemoryFileSystem();

        files.WriteText(Path, """
            <Root PresetName="Custom">
              <ShipSpotLightToggle>
                <Primary Device="Keyboard" Key="Key_O">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="4098BD65" Key="Joy_5" />
              </ShipSpotLightToggle>
              <NightVisionToggle>
                <Primary Device="Keyboard" Key="Key_P">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </NightVisionToggle>
              <FireChaffLauncher>
                <Primary Device="Keyboard" Key="Key_ScrollLock" />
                <Secondary Device="{NoDevice}" Key="" />
              </FireChaffLauncher>
              <LandingGearToggle>
                <Primary Device="Keyboard" Key="Key_L" />
                <Secondary Device="4098BD65" Key="Joy_9" />
              </LandingGearToggle>
            </Root>
            """);

        return EliteBinds.Parse(files, Path, "Custom", NullLogger.Instance);
    }

    private static HotasReading Reading(string id) => new()
    {
        Id = id,
        VendorId = 0x4098,
        ProductId = 0xBD65,
        Buttons = new bool[32],
    };

    private static readonly IReadOnlyList<HotasReading> OneInterface = [Reading(Throttle)];

    private static readonly IReadOnlyList<HotasReading> FourInterfaces =
    [
        Reading("{wgi/nrid/throttle-block-0}"),
        Reading(Throttle),
        Reading("{wgi/nrid/throttle-block-2}"),
        Reading("{wgi/nrid/throttle-block-3}"),
    ];

    /// <summary>The overlay's button on the throttle's fifth button, which Elite's Joy_5 names.</summary>
    private static D47Settings OverlayOnTheThrottle() =>
        D47Settings.Defaults with
        {
            Hotkeys = D47Settings.Defaults.Hotkeys with { ShowOverlayButton = new HotasButton(Throttle, 4).ToString() },
        };

    private static IReadOnlyList<BindingConflict> Find(
        D47Settings settings,
        IReadOnlyList<HotasReading> readings,
        params SwitchMapping[] switches) =>
        BindingConflicts.Find(settings, Rows, switches, Binds(), readings);

    [Fact]
    public void TheDefaultOverlayKeyConflictsWithCtrlAltOInElite()
    {
        var conflict = Assert.Single(
            Find(D47Settings.Defaults, OneInterface),
            conflict => conflict.RowKey == InterfaceCapability.ShowOverlayHotkeyKey);

        Assert.Equal("Show or hide the overlay", conflict.Control);
        Assert.Equal(BindingKind.Key, conflict.Kind);
        Assert.Equal("Ctrl+Alt+O", conflict.Gesture);
        Assert.Equal(["ShipSpotLightToggle"], conflict.EliteActions);
        Assert.Null(conflict.Switch);
    }

    /// <summary>Push-to-talk is not a system-wide hotkey, and is sampled whatever window is in front.</summary>
    [Fact]
    public void PushToTalkOnScrollLockConflictsWithEliteScrollLock()
    {
        var conflict = Assert.Single(
            Find(D47Settings.Defaults, OneInterface),
            conflict => conflict.RowKey == ListeningCapability.PushToTalkKeyKey);

        Assert.Equal("Push-to-talk", conflict.Control);
        Assert.Equal(["FireChaffLauncher"], conflict.EliteActions);
    }

    [Fact]
    public void AButtonOnAControllerShowingOneInterfaceConflicts()
    {
        var conflict = Assert.Single(
            Find(OverlayOnTheThrottle(), OneInterface),
            conflict => conflict.RowKey == InterfaceCapability.ShowOverlayButtonKey);

        Assert.Equal("Show or hide the overlay", conflict.Control);
        Assert.Equal(BindingKind.Button, conflict.Kind);
        Assert.Equal("button 5", conflict.Gesture);
        Assert.Equal(["ShipSpotLightToggle"], conflict.EliteActions);
    }

    [Fact]
    public void TheSameButtonOnAControllerShowingFourInterfacesDoesNotConflict() =>
        Assert.DoesNotContain(
            Find(OverlayOnTheThrottle(), FourInterfaces),
            conflict => conflict.Kind == BindingKind.Button);

    [Fact]
    public void AButtonOnAControllerThatIsNotAttachedDoesNotConflict() =>
        Assert.DoesNotContain(
            Find(OverlayOnTheThrottle(), []),
            conflict => conflict.Kind == BindingKind.Button);

    /// <summary>Open settings works only while d47's window has focus, so Elite never sees the key.</summary>
    [Fact]
    public void AKeyOnAHotkeyRowThatIsNotSystemWideNeverConflicts()
    {
        var settings = D47Settings.Defaults with
        {
            Hotkeys = D47Settings.Defaults.Hotkeys with { OpenSettings = "Ctrl+Alt+P" },
        };

        Assert.NotEmpty(Binds().Using("Ctrl+Alt+P"));
        Assert.DoesNotContain(
            Find(settings, OneInterface),
            conflict => conflict.RowKey == InterfaceCapability.OpenSettingsHotkeyKey);
    }

    [Fact]
    public void ASwitchPositionOnAButtonEliteBindsConflictsAndNamesTheSwitchAndPosition()
    {
        var gear = new SwitchMapping
        {
            Name = "LDG GEAR",
            DeviceId = Throttle,
            Positions =
            [
                new SwitchPosition(8, "landing_gear", DesiredState.On),
                new SwitchPosition(9, "landing_gear", DesiredState.Off),
            ],
        };

        var conflict = Assert.Single(Find(D47Settings.Defaults, OneInterface, gear), conflict => conflict.Switch is not null);

        Assert.Equal("LDG GEAR", conflict.Switch);
        Assert.Equal("button 8", conflict.Position);
        Assert.Equal(BindingKind.SwitchPosition, conflict.Kind);
        Assert.Equal(["LandingGearToggle"], conflict.EliteActions);
        Assert.Null(conflict.RowKey);
    }

    [Fact]
    public void AnUnassignedSwitchPositionNeverConflicts()
    {
        var idle = new SwitchMapping
        {
            Name = "SPARE",
            DeviceId = Throttle,
            Positions = [new SwitchPosition(8), new SwitchPosition(null)],
        };

        Assert.DoesNotContain(Find(D47Settings.Defaults, OneInterface, idle), conflict => conflict.Switch is not null);
    }
}
