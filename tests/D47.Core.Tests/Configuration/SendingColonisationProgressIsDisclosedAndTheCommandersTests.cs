using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class SendingColonisationProgressIsDisclosedAndTheCommandersTests
{
    private static EgressEntry Entry(bool reads, bool writes) =>
        EgressDisclosure.Entry(
            EgressDisclosure.ColonisationProgress,
            new D47Settings { Knowledge = new KnowledgeSettings { ColonisationPlanner = reads, ColonisationProgress = writes } },
            llmKeyPresent: false);

    [Fact]
    public void TheSwitchIsOffByDefault()
    {
        Assert.False(new D47Settings().Knowledge.ColonisationProgress);
    }

    [Fact]
    public void OnItNamesTheHostAndWhatIsSent()
    {
        var entry = Entry(reads: true, writes: true);

        Assert.True(entry.Active);
        Assert.Equal(
            "ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net (Raven Colonial)",
            entry.Destination);
        Assert.Equal(
            "The shown Commander's Raven Colonial key and the Commander name it belongs to. For each construction "
            + "site you deliver "
            + "to that already has a project on the planner: the tonnes you deliver, what the depot says is still "
            + "needed, and that the site is complete when it completes. Only what happens while d47 is running; "
            + "nothing from older journals.",
            entry.What);
        Assert.Equal("Your deliveries and each site's remaining needs, sent with your planner key.", entry.Summary);
        Assert.Contains(EgressDisclosure.ColonisationProgress, EgressDisclosure.Ids);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OffOrWithoutReadsNothingIsSent(bool reads, bool writes)
    {
        var entry = Entry(reads, writes);

        Assert.False(entry.Active);
        Assert.Equal("Colonisation progress is not sent to the planner.", entry.What);
    }

    [Fact]
    public void TheModelIsRefusedTheSwitch()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(ColonisationCapability.ProgressKey, "true", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.False(surface.Settings.Current.Knowledge.ColonisationProgress);
    }

    [Fact]
    public void TheModelIsRefusedTheKey()
    {
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.UseCommander("F1", "Tester");

        var result = surface.Settings.Apply(ColonisationCapability.KeyRow, "not-a-real-key", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
    }

    [Fact]
    public void TheKeyIsKeptPerCommander()
    {
        var row = ColonisationCapability.Create(() => null).Settings
            .Single(setting => setting.Key == ColonisationCapability.KeyRow);

        Assert.Equal(SettingScope.Commander, row.Scope);
        Assert.Equal("ravencolonial.apiKey", row.SecretName);
    }

    [Fact]
    public void ThePanelCanTurnItOn()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var result = surface.Settings.Apply(ColonisationCapability.ProgressKey, "true", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.True(surface.Settings.Current.Knowledge.ColonisationProgress);
    }
}
