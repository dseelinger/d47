using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>Each of the nine hotkey rows binds a stick button beside its key, and a press acts as the key does (#1550).</summary>
public sealed class EveryHotkeyTakesAStickButtonTests
{
    private static readonly string[] HotkeyKeys =
    [
        InterfaceCapability.OpenSettingsHotkeyKey, InterfaceCapability.FocusAskHotkeyKey, InterfaceCapability.FindHotkeyKey,
        InterfaceCapability.ShowOverlayHotkeyKey, InterfaceCapability.MoveOverlayHotkeyKey,
        VrCapability.ZoomInHotkeyKey, VrCapability.ZoomOutHotkeyKey, VrCapability.ResetZoomHotkeyKey, VrCapability.ResizeHotkeyKey,
    ];

    private static SettingsService Settings() => TestSurface.For(new MemoryInstall(), everyOptionalSurface: true).Settings;

    private static HotasReading Reading(params int[] held)
    {
        var buttons = new bool[32];

        foreach (var button in held)
        {
            buttons[button] = true;
        }

        return new HotasReading { Id = "hid:stick", VendorId = 1, ProductId = 2, Buttons = buttons };
    }

    [Fact]
    public void EachHotkeyRowAlsoBindsItsActionButtonSlot()
    {
        var settings = Settings();

        var bound = HotkeyKeys.Select(key => settings.Find(key)!.AlsoBinds).ToArray();

        Assert.Equal(ActionButtons.Slots.Select(slot => slot.Key).Order(), bound.Order());
    }

    [Fact]
    public void EachButtonRowIsAProtectedButtonDrawnOnItsKeyRow()
    {
        var settings = Settings();

        foreach (var key in HotkeyKeys)
        {
            var hotkey = settings.Find(key)!;
            var button = settings.Find(hotkey.AlsoBinds!)!;

            Assert.Equal(SettingKind.HotasButton, button.Kind);
            Assert.True(button.DrawnElsewhere, button.Key);
            Assert.True(button.Protected, button.Key);
            Assert.Equal(hotkey.DocsAnchor, button.DocsAnchor);
        }
    }

    [Fact]
    public void AButtonRowWritesItsSlotAndClearsToNull()
    {
        var settings = Settings();

        foreach (var slot in ActionButtons.Slots)
        {
            var row = settings.Find(slot.Key)!;
            var written = row.Binding!.Write!(new D47Settings(), "hid:stick#3");

            Assert.Equal("hid:stick#3", slot.Read(written));
            Assert.Null(slot.Read(row.Binding.Write(written, " ")));
        }
    }

    [Fact]
    public void TheHelpSaysAButtonCanSitBesideTheKey()
    {
        var settings = Settings();

        Assert.All(HotkeyKeys, key =>
        {
            var help = settings.Find(key)!.Help;

            Assert.Contains("stick button", help, StringComparison.Ordinal);
            Assert.Contains("giving it the same kind twice replaces that one", help, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void InTheBackgroundFindDoesNothingAndTheOverlayKeyStillToggles()
    {
        Assert.False(ActionButtons.Acts(InterfaceCapability.FindButtonKey, windowActive: false));
        Assert.False(ActionButtons.Acts(InterfaceCapability.OpenSettingsButtonKey, windowActive: false));
        Assert.False(ActionButtons.Acts(InterfaceCapability.FocusAskButtonKey, windowActive: false));
        Assert.True(ActionButtons.Acts(InterfaceCapability.ShowOverlayButtonKey, windowActive: false));
        Assert.True(ActionButtons.Acts(VrCapability.ResizeButtonKey, windowActive: false));
        Assert.True(ActionButtons.Acts(InterfaceCapability.FindButtonKey, windowActive: true));
    }

    [Fact]
    public void ABindWithTheSlotUnchangedDoesNotPressAHeldButtonAgain()
    {
        var slot = ActionButtons.Slots.Single(s => s.Key == InterfaceCapability.ShowOverlayButtonKey);
        var settings = slot.Write(new D47Settings(), "hid:stick#5");
        var buttons = new ActionButtons();
        var pressed = new List<string>();
        buttons.Pressed += pressed.Add;
        buttons.Bind(settings);

        buttons.Poll([Reading()]);
        buttons.Poll([Reading(5)]);

        buttons.Bind(settings with { Ui = settings.Ui with { ZoomPercent = 125 } });
        buttons.Poll([Reading(5)]);

        Assert.Equal([slot.Key], pressed);
    }
}
