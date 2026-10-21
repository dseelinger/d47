using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Configuration;

public sealed class ConvertingElitesScreenshotsIsTheCommandersSwitchTests
{
    [Fact]
    public void ItIsOnInANewSettingsFileAndSurvivesAWriteAndARead()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        Assert.True(surface.Settings.Current.Screenshots.Convert);

        surface.Settings.Apply(ScreenshotsCapability.ConvertKey, "false", SettingsCaller.Panel);

        Assert.False(TestSurface.For(install).Settings.Current.Screenshots.Convert);
    }

    [Fact]
    public void TheCommanderTurnsItOffAndOnWithTwoPhrases()
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.NotNull(surface.Router.MatchSetting("stop converting my screenshots"));
        Assert.NotNull(surface.Router.MatchSetting("start converting my screenshots"));
    }

    [Fact]
    public void TheModelIsRefusedTheChange()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(ScreenshotsCapability.ConvertKey, "false", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.True(surface.Settings.Current.Screenshots.Convert);
    }

    [Fact]
    public void TheDisclosureNamesTheFolderWhenOnAndSaysNothingIsWrittenWhenOff()
    {
        var on = EgressDisclosure.Entry(EgressDisclosure.Screenshots, new D47Settings(), llmKeyPresent: false);
        var off = EgressDisclosure.Entry(
            EgressDisclosure.Screenshots,
            new D47Settings { Screenshots = new ScreenshotSettings { Convert = false } },
            llmKeyPresent: false);

        Assert.Contains(EgressDisclosure.Screenshots, EgressDisclosure.Ids);
        Assert.Equal("Your screenshots", on.Name);
        Assert.False(on.Active);
        Assert.Contains(Path.Combine(EliteScreenshot.Folder(), "D47"), on.What, StringComparison.Ordinal);
        Assert.Contains("D47 sends them nowhere.", on.What, StringComparison.Ordinal);
        Assert.Equal("Converting Elite's screenshots is off. D47 writes nothing to your Pictures folder.", off.What);
    }
}
