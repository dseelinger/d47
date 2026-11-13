using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesTheWebAppTests
{
    private static EgressEntry Entry(bool on) =>
        EgressDisclosure.Entry(
            EgressDisclosure.WebApp,
            new D47Settings { WebApp = new WebAppSettings { Enabled = on } },
            llmKeyPresent: true);

    [Fact]
    public void OnItNamesTheWebAppWhatIsUploadedThatTheServerCanReadItAndWhatTurningItOffLeaves()
    {
        var entry = Entry(on: true);

        Assert.True(entry.Active);
        Assert.Equal("Web app", entry.Name);
        Assert.Equal(WebAppSettings.Address, entry.Destination);
        Assert.Equal(
            "Pressing Sign in opens the web app in your browser, where you sign in with Frontier, and D47 sends the "
            + "one-time code your browser hands back to link this install. After that, a snapshot of the Commander "
            + "who signed in: Frontier ID and Commander name, credits, location, ranks and reputation, ships and their "
            + "modules, your fleet carrier, engineers, ship materials, missions, checklists and goals, watches, route "
            + "plans and the panel theme. The snapshot is stored as it was sent, so D47's server can read it. "
            + "Turning Web app off deletes the snapshot and this install's upload token, and the web app keeps only "
            + "the time it was turned off.",
            entry.What);
        Assert.Equal(
            "Your sign-in, then a snapshot of your Commander for the web app, deleted when you turn this off.",
            entry.Summary);
    }

    [Fact]
    public void OffItSaysNothingIsSent()
    {
        var entry = Entry(on: false);

        Assert.False(entry.Active);
        Assert.Equal("Web app is off, so nothing is sent to it.", entry.What);
    }

    [Fact]
    public void TheEntryIsInTheFixedListTheDefaultIsOffAndTheAddressIsTheWorkers()
    {
        Assert.Contains(EgressDisclosure.WebApp, EgressDisclosure.Ids);
        Assert.False(new D47Settings().WebApp.Enabled);
        Assert.Equal("https://d47-web.dseelinger.workers.dev", WebAppSettings.Address);
    }

    [Fact]
    public void TheRowIsAProtectedToggleTiedToTheEntryAndSignInIsAPressTheModelCannotReach()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var rows = PrivacyCapability.Create(settings).Settings;
        var row = rows.Single(setting => setting.Key == PrivacyCapability.WebAppKey);
        var signIn = rows.Single(setting => setting.Key == PrivacyCapability.WebAppSignInKey);
        var off = new D47Settings();

        Assert.Equal("Web app", row.Label);
        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(row.Protected);
        Assert.Equal(EgressDisclosure.WebApp, row.EgressId);
        Assert.Equal("false", row.Binding!.Read(off));
        Assert.Equal("true", row.Binding.Read(row.Binding.Write!(off, "true")));

        Assert.Equal(SettingKind.Info, signIn.Kind);
        Assert.Equal(SettingApplyStatus.Refused, settings.Apply(PrivacyCapability.WebAppKey, "true", SettingsCaller.Model).Status);
        Assert.Equal(SettingApplyStatus.Refused, settings.Apply(PrivacyCapability.WebAppSignInKey, "x", SettingsCaller.Model).Status);
    }
}
