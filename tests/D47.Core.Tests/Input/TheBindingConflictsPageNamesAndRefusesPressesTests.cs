using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>What the Binding conflicts row and page say, and which presses the page refuses (#2383).</summary>
public sealed class TheBindingConflictsPageNamesAndRefusesPressesTests
{
    private const string Path = @"C:\d47-test\page.binds";

    private const string Throttle = "{wgi/nrid/throttle}";

    private static readonly SettingsService Settings = TestSurface.For(new MemoryInstall(), everyOptionalSurface: true).Settings;

    private static readonly IReadOnlyList<SettingRow> Rows = [.. Settings.Sections.SelectMany(section => section.Rows)];

    /// <summary>Ctrl+Alt+P for night vision and the headlook reset, and the throttle's fifth button for the light.</summary>
    private static EliteBinds Binds()
    {
        var files = new MemoryFileSystem();

        files.WriteText(Path, """
            <Root PresetName="Custom">
              <NightVisionToggle>
                <Primary Device="Keyboard" Key="Key_P">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </NightVisionToggle>
              <HeadLookReset>
                <Primary Device="Keyboard" Key="Key_P">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </HeadLookReset>
              <ShipSpotLightToggle>
                <Primary Device="Keyboard" Key="Key_L" />
                <Secondary Device="4098BD65" Key="Joy_5" />
              </ShipSpotLightToggle>
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

    private static readonly IReadOnlyList<HotasReading> TwoInterfaces = [Reading(Throttle), Reading("{wgi/nrid/throttle-block-1}")];

    private static IReadOnlyList<string> ForPress(string slotKey, string value, IReadOnlyList<HotasReading> readings) =>
        BindingConflicts.ForPress(D47Settings.Defaults, Rows, slotKey, value, Binds(), readings);

    [Fact]
    public void AKeyEliteBindsIsFoundWithEveryActionOnIt()
    {
        Assert.Equal(
            ["NightVisionToggle", "HeadLookReset"],
            ForPress(InterfaceCapability.ShowOverlayHotkeyKey, "Ctrl+Alt+P", OneInterface));
    }

    [Fact]
    public void AKeyEliteDoesNotBindIsFree()
    {
        Assert.Empty(ForPress(InterfaceCapability.ShowOverlayHotkeyKey, "Ctrl+Alt+K", OneInterface));
    }

    /// <summary>Find is not system-wide, so it never acts while Elite has the foreground.</summary>
    [Fact]
    public void AKeyOnARowThatOnlyWorksInD47IsNeverAConflict()
    {
        Assert.Empty(ForPress(InterfaceCapability.FindHotkeyKey, "Ctrl+Alt+P", OneInterface));
    }

    [Fact]
    public void AButtonIsAConflictOnlyOnAControllerShowingOneInterface()
    {
        var button = new HotasButton(Throttle, 4).ToString();

        Assert.Equal(["ShipSpotLightToggle"], ForPress(InterfaceCapability.ShowOverlayButtonKey, button, OneInterface));
        Assert.Empty(ForPress(InterfaceCapability.ShowOverlayButtonKey, button, TwoInterfaces));
    }

    [Fact]
    public void AButtonConflictIsNamedByItsOwnRow()
    {
        var conflict = new BindingConflict(
            "Show or hide the overlay", InterfaceCapability.ShowOverlayButtonKey, null, null, BindingKind.Button, "button 5", ["ShipSpotLightToggle"]);

        Assert.Equal("Show or hide the overlay button", BindingConflicts.ControlName(conflict, Rows));
    }

    [Fact]
    public void ASwitchConflictIsNamedWithItsPosition()
    {
        var conflict = new BindingConflict("Gear", null, "Gear", "button 8", BindingKind.SwitchPosition, "button 8", ["LandingGearToggle"]);

        Assert.Equal("Switch Gear, button 8", BindingConflicts.ControlName(conflict, Rows));
    }

    [Theory]
    [InlineData(0, "No D47 key or button is bound in Elite")]
    [InlineData(1, "1 of D47's keys and buttons is bound in Elite too: A")]
    [InlineData(2, "2 of D47's keys and buttons are bound in Elite too: A and B")]
    [InlineData(3, "3 of D47's keys and buttons are bound in Elite too: A, B and C")]
    [InlineData(5, "5 of D47's keys and buttons are bound in Elite too: A, B, C and 2 more")]
    public void TheRowNamesTheFirstThreeAndCountsTheRest(int count, string says)
    {
        Assert.Equal(says, BindingConflicts.Summary([.. "ABCDE".Take(count).Select(letter => letter.ToString())]));
    }

    [Fact]
    public void ARefusedKeyNamesTheEliteActionsAndTheKeyKept()
    {
        Assert.Equal(
            "Elite binds Ctrl+Alt+P to NightVisionToggle and HeadLookReset. Press another key, or click elsewhere to keep Ctrl+Alt+O.",
            BindingConflicts.EliteBindsIt("Ctrl+Alt+P", ["NightVisionToggle", "HeadLookReset"], button: false, "Ctrl+Alt+O"));
    }

    [Fact]
    public void ARefusedButtonAsksForAnotherButton()
    {
        Assert.Equal(
            "Elite binds button 5 to ShipSpotLightToggle. Press another button, or click elsewhere to keep button 11.",
            BindingConflicts.EliteBindsIt("button 5", ["ShipSpotLightToggle"], button: true, "button 11"));
    }

    [Theory]
    [InlineData(InterfaceCapability.ShowOverlayHotkeyKey, "Settings › Screens › Overlay")]
    [InlineData(InterfaceCapability.ShowOverlayButtonKey, "Settings › Screens › Overlay")]
    [InlineData(ListeningCapability.PushToTalkKeyKey, "Settings › Hearing and sound › Voice Input")]
    [InlineData(ListeningCapability.BindingConflictsKey, "Settings › Screens › Window")]
    [InlineData(SwitchCapability.ListKey, "Settings › Acting on the game › Macros and switches")]
    public void EachControlSaysWhereItIsInSettings(string key, string trail)
    {
        Assert.Equal(trail, Settings.PlaceTrailOf(key));
    }

    [Fact]
    public void AKeyNoPlaceDrawsHasNoTrail()
    {
        Assert.Null(Settings.PlaceTrailOf("no.such.key"));
    }
}
