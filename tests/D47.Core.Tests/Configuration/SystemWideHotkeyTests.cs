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
/// <para>
/// Driven through <b>Show or hide the overlay</b>, which is system-wide unconditionally. It used
/// to be <b>Stop speaking</b>, which is a better-known row and stopped being usable as the example
/// when push-to-talk took the job over
/// (<a href="https://github.com/dseelinger/d47/issues/218">#218</a>): that row now hides itself
/// while push-to-talk is bound, and <c>AppliesWhen</c> refuses a write as well as hiding — so every
/// case here would have been rejected for the wrong reason and passed for the wrong reason too.
/// </para>
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

        var result = surface.Settings.Apply(
            InterfaceCapability.ShowOverlayHotkeyKey, gesture, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Contains("modifier", result.Message, StringComparison.OrdinalIgnoreCase);

        // And it is not stored, so nothing later has to discover it cannot be registered.
        Assert.NotEqual(gesture, surface.Settings.Read(InterfaceCapability.ShowOverlayHotkeyKey));
    }

    [Theory]
    [InlineData("Ctrl+Alt+X")]
    [InlineData("Ctrl+OemCloseBrackets")]
    [InlineData("Shift+F9")]
    public void AKeyWithAModifierIsAccepted(string gesture)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(
            InterfaceCapability.ShowOverlayHotkeyKey, gesture, SettingsCaller.Panel);

        // Applied, or Unchanged when it is already the default — either way, accepted and stored.
        Assert.True(
            result.Status is SettingApplyStatus.Applied or SettingApplyStatus.Unchanged,
            $"{gesture} should be bindable, but got {result.Status}: {result.Message}");

        Assert.Equal(gesture, surface.Settings.Read(InterfaceCapability.ShowOverlayHotkeyKey));
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

        surface.Settings.Apply(
            InterfaceCapability.ShowOverlayHotkeyKey, "Ctrl+Alt+X", SettingsCaller.Panel);

        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(
                InterfaceCapability.ShowOverlayHotkeyKey, null, SettingsCaller.Panel).Status);
    }
}
