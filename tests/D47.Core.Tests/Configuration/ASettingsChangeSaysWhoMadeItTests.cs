using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A <see cref="SettingsChanged"/> carries the caller that wrote it.</summary>
public class ASettingsChangeSaysWhoMadeItTests
{
    [Fact]
    public void AShipBindingWriteIsRaisedAsShipBinding()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        var raised = new List<SettingsChanged>();
        settings.Changed += raised.Add;

        settings.Apply("llm.aboutMe", "my own story", SettingsCaller.ShipBinding);

        Assert.Equal(SettingsCaller.ShipBinding, Assert.Single(raised).Caller);
    }

    [Fact]
    public void APanelWriteIsRaisedAsPanel()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        var raised = new List<SettingsChanged>();
        settings.Changed += raised.Add;

        settings.Apply("llm.aboutMe", "my own story", SettingsCaller.Panel);

        Assert.Equal(SettingsCaller.Panel, Assert.Single(raised).Caller);
    }

    [Fact]
    public void AReloadForAnotherCommanderIsRaisedWithNoCaller()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        var raised = new List<SettingsChanged>();
        settings.Changed += raised.Add;

        settings.UseCommander("F1");
        settings.Apply("llm.aboutMe", "my own story", SettingsCaller.Panel);
        raised.Clear();

        settings.UseCommander("F2");

        Assert.NotEmpty(raised);
        Assert.All(raised, change => Assert.Null(change.Caller));
    }
}
