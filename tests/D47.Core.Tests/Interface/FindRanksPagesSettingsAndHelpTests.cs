using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Find returns the pages, settings places, rows and help pages a query names, best first (#1471).</summary>
public class FindRanksPagesSettingsAndHelpTests
{
    private static readonly Dictionary<PanelTab, string> Words = new() { [PanelTab.Assets] = "Asset Mgmt" };

    internal static (PanelFind Find, SettingsService Settings, PanelNavigator Nav) Build()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        settings.Bind(CapabilityRegistry.Build(
        [
            ListeningCapability.Create(settings, new ListeningCapability.ListeningSurface
            {
                InputDevices = () => ["mic-1"],
                DeviceLabel = id => id,
                SinceHeard = () => null,
                CaptureState = () => (true, null),
                TranscriberState = () => (true, "tiny.en", null),
                Binds = () => new D47.Core.Input.EliteBinds { PresetName = "KeyboardMouseOnly", SourceFile = "KeyboardMouseOnly.binds" },
                InstalledModels = () => ["tiny.en"],
                KeyLabel = key => key,
            }),
            CalloutCapability.Create(() => string.Empty),
            ShipsCapability.Create(surface: ShipsCapability.ShipsSurface.Inert),
        ]));

        var nav = new PanelNavigator();
        nav.Register(PanelTab.Assets, new NavCrumb("ships", "Ships"));
        nav.Register(PanelTab.Assets, new NavCrumb("suits", "Suits"));
        nav.Register(PanelTab.Settings, new NavCrumb("settings.phrases", "Phrases") { Spoken = ["commands", "voice commands"] });

        return (new PanelFind(nav, settings, Words), settings, nav);
    }

    private static PanelFindResult First(string query) => Build().Find.Search(query)[0];

    [Fact]
    public void AnAliasFindsItsPlaceFirst()
    {
        var first = First("ptt");

        Assert.Equal(PanelFindKind.SettingsPlace, first.Kind);
        Assert.Equal("voice-input", first.Target);
    }

    [Fact]
    public void ARowCarriesItsPlaceAndGroupInItsPath()
    {
        var first = First("push");

        Assert.Equal("Push-to-talk", first.Title);
        Assert.Equal("Settings › Hearing and sound › Voice Input › Microphone", first.Path);
    }

    [Fact]
    public void TheTitleThatStartsWithTheQueryComesBeforeAWordInsideOne()
    {
        var titles = Build().Find.Search("fuel").Select(r => r.Title).ToList();

        Assert.True(titles.IndexOf("Fuel and range") < titles.IndexOf("Carrier fuel"));
        Assert.Equal("Fuel and range", titles[0]);
    }

    [Fact]
    public void ARowDrawnOnATabReadsWithThatTabsTitle()
    {
        var first = First("hull");

        Assert.Equal("Hull pictures", first.Title);
        Assert.Equal("Asset Mgmt › Ships", first.Path);
        Assert.Equal("ships.art", first.Target);
    }

    [Fact]
    public void LettersInOrderFindATitleFromItsWordStarts()
    {
        var first = First("pshtlk");

        Assert.Equal("Push-to-talk", first.Title);
    }

    [Fact]
    public void APageComesBeforeARowWhoseHelpMentionsIt()
    {
        var results = Build().Find.Search("ships");

        Assert.Equal(PanelFindKind.Page, results[0].Kind);
        Assert.Equal("ships", results[0].Target);
        Assert.Equal("Asset Mgmt", results[0].Path);
    }

    [Fact]
    public void ARowThatDoesNotApplyIsNotFoundUntilTheSettingsMakeItApply()
    {
        var (find, settings, _) = Build();

        Assert.DoesNotContain(find.Search("take a break after"), r => r.Target == "callouts.sessionLengthHours");

        settings.Apply("callouts.sessionLength", "true", SettingsCaller.Panel);

        Assert.Contains(find.Search("take a break after"), r => r.Target == "callouts.sessionLengthHours");
    }

    [Fact]
    public void AHiddenRootIsNotFound()
    {
        var (find, _, nav) = Build();

        Assert.Contains(find.Search("suits"), r => r.Target == "suits");

        nav.SetVisible(PanelTab.Assets, "suits", false);

        Assert.DoesNotContain(find.Search("suits"), r => r.Target == "suits");
    }

    [Theory]
    [InlineData("commands")]
    [InlineData("voice commands")]
    public void SearchingCommandsFindsThePhrasesPageFirst(string query)
    {
        var pages = Build().Find.Search(query).Where(r => r.Kind == PanelFindKind.Page).ToList();

        Assert.Equal("settings.phrases", pages[0].Target);
    }

    [Fact]
    public void ABlankQueryFindsNothing()
    {
        Assert.Empty(Build().Find.Search("  "));
    }

    [Fact]
    public void TheSameQueryReturnsTheSameOrder()
    {
        var (find, _, _) = Build();

        Assert.Equal(find.Search("s"), find.Search("s"));
    }
}
