using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public sealed class DeletingElitesBmpAfterConvertingIsTheCommandersSwitchTests
{
    [Fact]
    public void ItIsOnWhenTheSettingsFileHasNoSuchKeyAndSurvivesAWriteAndARead()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        Assert.True(surface.Settings.Current.Screenshots.DeleteOriginal);
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<D47Settings>("""{"screenshots":{"convert":true}}""", Options)!.Screenshots.DeleteOriginal);

        surface.Settings.Apply(ScreenshotsCapability.DeleteOriginalKey, "false", SettingsCaller.Panel);

        Assert.False(TestSurface.For(install).Settings.Current.Screenshots.DeleteOriginal);
    }

    [Fact]
    public void TheCommanderTurnsItOffAndOnWithTwoPhrases()
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.NotNull(surface.Router.MatchSetting("keep my screenshot originals"));
        Assert.NotNull(surface.Router.MatchSetting("delete my screenshot originals"));
    }

    [Fact]
    public void TheModelIsRefusedTheChange()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(ScreenshotsCapability.DeleteOriginalKey, "false", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.True(surface.Settings.Current.Screenshots.DeleteOriginal);
    }

    [Fact]
    public void ItSitsAfterTheConvertRowAsAProtectedCheckbox()
    {
        var rows = ScreenshotsCapability.Create().Settings;
        var row = rows.Single(r => r.Key == ScreenshotsCapability.DeleteOriginalKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(row.Protected);
        Assert.Equal("Delete Elite's BMP after converting", row.Label);
        Assert.Equal(rows.ToList().FindIndex(r => r.Key == ScreenshotsCapability.ConvertKey) + 1, rows.ToList().IndexOf(row));
    }

    private static readonly System.Text.Json.JsonSerializerOptions Options = new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
}
