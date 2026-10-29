using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class AGroupResetLeavesTypedInCommanderFieldsAloneTests
{
    private static int GroupIndex(string placeId, string title) =>
        SettingsLayout.Areas.SelectMany(a => a.Places).Single(p => p.Id == placeId).Groups.ToList()
            .FindIndex(g => g.Title == title);

    [Fact]
    public void ResettingExplorationCalloutsKeepsTheHomeSystem()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.UseCommander("F1");

        settings.Apply(CalloutCapability.HomeSystemKey, "Sol", SettingsCaller.Panel);
        settings.Apply(CalloutCapability.ArrivalKey, "false", SettingsCaller.Panel);

        var moved = settings.ResetGroup("exploring", GroupIndex("exploring", "Exploration callouts"), SettingsCaller.Panel);

        Assert.Equal(1, moved);
        Assert.False(settings.IsChanged(CalloutCapability.ArrivalKey));
        Assert.Equal("Sol", settings.Read(CalloutCapability.HomeSystemKey));
    }

    [Fact]
    public void TheRowsOwnResetStillClearsTheHomeSystem()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.UseCommander("F1");

        settings.Apply(CalloutCapability.HomeSystemKey, "Sol", SettingsCaller.Panel);
        Assert.True(settings.IsChanged(CalloutCapability.HomeSystemKey));
        settings.Reset(CalloutCapability.HomeSystemKey, SettingsCaller.Panel);

        Assert.False(settings.IsChanged(CalloutCapability.HomeSystemKey));
    }

    [Fact]
    public void ResettingAboutYouKeepsWhatTheCommanderTypedButResetsTheAudience()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.UseCommander("F1");
        var audience = settings.Find("llm.scenarioAudience")!;
        var other = audience.Choices!.First(c => c != audience.DefaultValueFor(settings.Current));

        settings.Apply("llm.characterSheet", "A trader", SettingsCaller.Panel);
        settings.Apply("llm.aboutMe", "I fly at night", SettingsCaller.Panel);
        settings.Apply("llm.scenario", "A quiet run", SettingsCaller.Panel);
        settings.Apply("llm.scenarioAudience", other, SettingsCaller.Panel);

        var moved = settings.ResetGroup("about-you", GroupIndex("about-you", "About you"), SettingsCaller.Panel);

        Assert.Equal(1, moved);
        Assert.Equal("A trader", settings.Read("llm.characterSheet"));
        Assert.Equal("I fly at night", settings.Read("llm.aboutMe"));
        Assert.Equal("A quiet run", settings.Read("llm.scenario"));
        Assert.False(settings.IsChanged("llm.scenarioAudience"));
    }
}
