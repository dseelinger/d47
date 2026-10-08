using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>The places in the panel a message names, found in its text (#951).</summary>
public class AMessageLinksThePlacesItNamesTests
{
    /// <summary>The words the tab bar draws.</summary>
    private static readonly Dictionary<PanelTab, string> Words = new()
    {
        [PanelTab.Transcript] = "TRANSCRIPT",
        [PanelTab.Search] = "SEARCH",
        [PanelTab.Stories] = "STORIES",
        [PanelTab.Commander] = "COMMANDER",
        [PanelTab.Assets] = "ASSET MGMT",
        [PanelTab.Navigation] = "NAVIGATION",
        [PanelTab.Settings] = "SETTINGS",
    };

    private static PanelNavigator Surface(bool settings = true)
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("transcript.conversation", "In Ship"));
        nav.Register(PanelTab.Transcript, new NavCrumb("transcript.log", "Log File") { Spoken = ["log"] });
        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Commander, new NavCrumb("missions", "Missions"));
        nav.Register(PanelTab.Assets, new NavCrumb("loadout.ships", "Ships"));
        nav.Register(PanelTab.Assets, new NavCrumb("loadout.carrier", "Carrier"));
        nav.Register(PanelTab.Navigation, new NavCrumb("routing.plan", "Plan"));

        if (settings)
        {
            nav.Register(PanelTab.Settings, new NavCrumb("settings", "Settings"));
            nav.Register(PanelTab.Settings, new NavCrumb("phrases", "Phrases"));
        }

        return nav;
    }

    private static IReadOnlyList<PanelPlace> Find(string text, PanelNavigator? nav = null) =>
        PanelPlaces.Find(text, nav ?? Surface(), Words);

    private static string Linked(string text, PanelPlace place) => text.Substring(place.Start, place.Length);

    [Fact]
    public void APathWithASeparatorLinksTheTabAndTheRoot()
    {
        const string text = "The full loadout is on Asset Mgmt › Ships.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Asset Mgmt › Ships", Linked(text, place));
        Assert.Equal(PanelTab.Assets, place.Tab);
        Assert.Equal("loadout.ships", place.RootKey);
    }

    [Fact]
    public void AGreaterThanSignSeparatesAPathToo()
    {
        const string text = "Pressing Check for updates in Settings > About still asks.";

        var place = Assert.Single(Find(text));

        // About is no root of Settings, so the walk stops at the tab.
        Assert.Equal("Settings", Linked(text, place));
        Assert.Equal(PanelTab.Settings, place.Tab);
        Assert.Null(place.RootKey);
    }

    [Fact]
    public void APathWhoseSecondPartNamesNothingLinksOnlyTheTab()
    {
        const string text = "Look on Commander › Nowhere for it.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Commander", Linked(text, place));
        Assert.Equal(PanelTab.Commander, place.Tab);
        Assert.Null(place.RootKey);
    }

    [Fact]
    public void ARootOfSeveralWordsIsMatchedWhole()
    {
        const string text = "It is written to Transcript › Log File every session.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Transcript › Log File", Linked(text, place));
        Assert.Equal("transcript.log", place.RootKey);
    }

    [Fact]
    public void ARootCanBeNamedByWhatIsSpokenForIt()
    {
        const string text = "See Transcript › log.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Transcript › log", Linked(text, place));
        Assert.Equal("transcript.log", place.RootKey);
    }

    [Fact]
    public void SettingsAsACapitalisedWordIsALink()
    {
        const string text = "No Chatterbox voice has been chosen. Pick one in Settings.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Settings", Linked(text, place));
        Assert.Equal(PanelTab.Settings, place.Tab);
        Assert.Null(place.RootKey);
    }

    [Theory]
    [InlineData("Your settings are saved.")]
    [InlineData("SettingsFoo is not a word the panel draws.")]
    [InlineData("Resettings are not a thing.")]
    public void SettingsInsideAnotherWordOrInLowerCaseIsNot(string text) =>
        Assert.Empty(Find(text));

    [Fact]
    public void TheTabFormLinksTheTabAndTheWordTab()
    {
        const string text = "Your route is on the Navigation tab.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Navigation tab", Linked(text, place));
        Assert.Equal(PanelTab.Navigation, place.Tab);
    }

    [Fact]
    public void TheTabFormReadsTheWordAsTheTabBarDrawsIt()
    {
        const string text = "Open the Asset Mgmt tab.";

        var place = Assert.Single(Find(text));

        Assert.Equal("Asset Mgmt tab", Linked(text, place));
        Assert.Equal(PanelTab.Assets, place.Tab);
    }

    [Theory]
    [InlineData("Commander, the route is plotted.")]
    [InlineData("Navigation is offline.")]
    [InlineData("Search the system for a station.")]
    [InlineData("Stories are told by the ship.")]
    [InlineData("Assets are worth 40 million credits.")]
    public void ATabNameOnItsOwnIsPlainText(string text) =>
        Assert.Empty(Find(text));

    [Fact]
    public void ATabThisSurfaceDoesNotHaveIsNotALink()
    {
        var nav = Surface(settings: false);

        Assert.Empty(Find("Pick one in Settings.", nav));
        Assert.Empty(Find("Check Settings › Phrases.", nav));
        Assert.Empty(Find("Search › Systems has it.", nav));
    }

    [Fact]
    public void EveryPlaceInAMessageIsFoundInOrder()
    {
        const string text = "Add a key in Settings, then open the Commander tab and Asset Mgmt › Carrier.";

        var places = Find(text);

        Assert.Equal(
            ["Settings", "Commander tab", "Asset Mgmt › Carrier"],
            places.Select(place => Linked(text, place)));
        Assert.Equal("loadout.carrier", places[2].RootKey);
    }
}
