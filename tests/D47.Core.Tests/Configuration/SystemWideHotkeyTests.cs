using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A key claimed from the whole system cannot be a bare one: it would stop working in every
/// other application, Elite included, so the binder refuses it. The refusal has to happen on the
/// row the Commander just pressed — reporting it afterwards, on the panel behind the settings
/// window, is a message nobody sees attached to a value that was stored anyway.
/// </summary>
public class SystemWideHotkeyTests
{
    [Theory]
    [InlineData("OemCloseBrackets")]
    [InlineData("F9")]
    [InlineData("A")]
    public void ABareKeyIsRefusedForASystemWideRow(string gesture)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(SpeechCapability.ShutUpHotkeyKey, gesture, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Contains("modifier", result.Message, StringComparison.OrdinalIgnoreCase);

        // And it is not stored, so nothing later has to discover it cannot be registered.
        Assert.NotEqual(gesture, surface.Settings.Read(SpeechCapability.ShutUpHotkeyKey));
    }

    [Theory]
    [InlineData("Ctrl+Alt+X")]
    [InlineData("Ctrl+OemCloseBrackets")]
    [InlineData("Shift+F9")]
    public void AKeyWithAModifierIsAccepted(string gesture)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(SpeechCapability.ShutUpHotkeyKey, gesture, SettingsCaller.Panel);

        // Applied, or Unchanged when it is already the default — either way, accepted and stored.
        Assert.True(
            result.Status is SettingApplyStatus.Applied or SettingApplyStatus.Unchanged,
            $"{gesture} should be bindable, but got {result.Status}: {result.Message}");

        Assert.Equal(gesture, surface.Settings.Read(SpeechCapability.ShutUpHotkeyKey));
    }

    /// <summary>
    /// Push-to-talk is polled rather than registered, so a bare key is the normal arrangement
    /// there and must stay allowed. That difference is the whole reason the row declares it.
    /// </summary>
    [Fact]
    public void ABareKeyIsStillFineForAKeyThatIsOnlyPolled()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(
            ListeningCapability.PushToTalkKeyKey, "OemOpenBrackets", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
    }

    [Fact]
    public void ClearingASystemWideRowIsAlwaysAllowed()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.ShutUpHotkeyKey, "Ctrl+Alt+X", SettingsCaller.Panel);

        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(SpeechCapability.ShutUpHotkeyKey, null, SettingsCaller.Panel).Status);
    }
}
