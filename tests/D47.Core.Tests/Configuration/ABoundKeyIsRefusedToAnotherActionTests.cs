using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class ABoundKeyIsRefusedToAnotherActionTests
{
    [Theory]
    [InlineData(SettingsCaller.Panel)]
    [InlineData(SettingsCaller.Hotkey)]
    public void AHeldKeyIsRejectedAndItsHolderKeepsIt(SettingsCaller caller)
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(InterfaceCapability.FindHotkeyKey, "Ctrl+L", caller);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Equal("Ctrl+L is already bound to Focus the ask box. Clear it there first.", result.Message);
        Assert.Equal("Ctrl+L", surface.Settings.Read(InterfaceCapability.FocusAskHotkeyKey));
        Assert.Equal("Ctrl+Shift+F", surface.Settings.Read(InterfaceCapability.FindHotkeyKey));
    }

    [Fact]
    public void CaseAndModifierOrderDoNotMatter()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(InterfaceCapability.FocusAskHotkeyKey, "shift+ctrl+f", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Equal(
            InterfaceCapability.FindHotkeyKey,
            surface.Settings.HeldBy(InterfaceCapability.FocusAskHotkeyKey, "SHIFT+CTRL+f")?.Key);
    }

    [Fact]
    public void ADifferentChordIsNotHeld()
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(InterfaceCapability.FindHotkeyKey, "Ctrl+Shift+L", SettingsCaller.Panel).Status);
    }

    [Fact]
    public void ARowsOwnValueIsUnchangedNotRefused()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(InterfaceCapability.FocusAskHotkeyKey, "Ctrl+L", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Unchanged, result.Status);
    }

    [Fact]
    public void ClearingIsNeverRefused()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(InterfaceCapability.FocusAskHotkeyKey, null, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
    }

    [Fact]
    public void AHeldButtonIsRejected()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.Apply(ListeningCapability.PushToTalkButtonKey, "stick#4", SettingsCaller.Panel);

        var result = surface.Settings.Apply(ListeningCapability.CancelButtonKey, "stick#4", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Contains("is already bound to Push-to-talk button.", result.Message, StringComparison.Ordinal);
        Assert.Equal("stick#4", surface.Settings.Read(ListeningCapability.PushToTalkButtonKey));
        Assert.Null(surface.Settings.Read(ListeningCapability.CancelButtonKey));
    }

    [Fact]
    public void AButtonOnAnotherDeviceOrNumberIsNotHeld()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.Apply(ListeningCapability.PushToTalkButtonKey, "stick#4", SettingsCaller.Panel);

        Assert.Null(surface.Settings.HeldBy(ListeningCapability.CancelButtonKey, "stick#5"));
        Assert.Null(surface.Settings.HeldBy(ListeningCapability.CancelButtonKey, "throttle#4"));
    }
}
