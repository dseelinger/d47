using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A spoken Find phrase opens the one place it names, asks which when places tie, and leaves the rest alone (#1473).</summary>
public class AFindPhraseOpensTheOnePlaceItNamesTests
{
    private static PanelFind Find() => FindRanksPagesSettingsAndHelpTests.Build().Find;

    [Theory]
    [InlineData("where is the fuel setting", "fuel")]
    [InlineData("where are the fuel settings", "fuel")]
    [InlineData("where do I change push to talk", "push to talk")]
    [InlineData("where do I set push to talk", "push to talk")]
    [InlineData("where do I turn on hull pictures", "hull pictures")]
    [InlineData("where do I turn off hull pictures", "hull pictures")]
    [InlineData("show me the hull pictures setting", "hull pictures")]
    [InlineData("show me the hull pictures settings", "hull pictures")]
    [InlineData("show me the ships page", "ships")]
    [InlineData("open the fuel setting", "fuel")]
    [InlineData("open the fuel settings", "fuel")]
    [InlineData("find the fuel setting", "fuel")]
    [InlineData("find the fuel settings", "fuel")]
    [InlineData("find the ships page", "ships")]
    [InlineData("Where is the FUEL setting?", "fuel")]
    [InlineData("  show me the ships page.  ", "ships")]
    public void EachPhraseFormGivesItsQuery(string spoken, string query)
    {
        Assert.Equal(query, PanelFindPhrases.Query(spoken));
    }

    [Theory]
    [InlineData("where is my Anaconda")]
    [InlineData("find the nearest signal")]
    [InlineData("where is the fuel")]
    [InlineData("find fuel")]
    [InlineData("how do I change push to talk")]
    [InlineData("how do I turn off hull pictures")]
    [InlineData("open the ships tab")]
    [InlineData("open the settings")]
    [InlineData("show me the setting")]
    [InlineData("where do I change")]
    public void APhraseThatNamesNoSettingOrPageIsNotAFindPhrase(string spoken)
    {
        Assert.Null(PanelFindPhrases.Query(spoken));
        Assert.Null(PanelFindPhrases.Answer(spoken, Find()));
    }

    [Fact]
    public void TheFuelSettingOpensFuelAndRangeAndSaysItsPath()
    {
        var answer = PanelFindPhrases.Answer("where is the fuel setting", Find());

        Assert.NotNull(answer?.Open);
        Assert.Equal(PanelFindKind.SettingRow, answer.Open.Kind);
        Assert.Equal(CalloutCapability.FuelKey, answer.Open.Target);
        Assert.Equal($"{answer.Open.Path} › Fuel and range.", answer.Said);
    }

    [Fact]
    public void TheHullPicturesSettingOpensOnTheShipsTab()
    {
        var answer = PanelFindPhrases.Answer("show me the hull pictures setting", Find());

        Assert.NotNull(answer?.Open);
        Assert.Equal("ships.art", answer.Open.Target);
        Assert.Equal("Asset Mgmt › Ships › Hull pictures.", answer.Said);
    }

    [Fact]
    public void APhraseThatFindsNothingIsNotTaken()
    {
        Assert.Null(PanelFindPhrases.Answer("where do I turn off my shields", Find()));
    }

    [Fact]
    public void TwoPlacesThatShareTheTopRankMoveNothingAndAreBothNamed()
    {
        var (find, _, nav) = FindRanksPagesSettingsAndHelpTests.Build();
        nav.Register(PanelTab.Assets, new NavCrumb("zorblax-assets", "Zorblax"));
        nav.Register(PanelTab.Commander, new NavCrumb("zorblax-commander", "Zorblax"));

        var answer = PanelFindPhrases.Answer("find the zorblax page", find);

        Assert.NotNull(answer);
        Assert.Null(answer.Open);
        Assert.Equal("Two places: Commander › Zorblax, or Asset Mgmt › Zorblax. Which one?", answer.Said);
    }

    [Fact]
    public void ThreeTiedPlacesAreAllNamed()
    {
        var (find, _, nav) = FindRanksPagesSettingsAndHelpTests.Build();
        nav.Register(PanelTab.Assets, new NavCrumb("zorblax-assets", "Zorblax"));
        nav.Register(PanelTab.Commander, new NavCrumb("zorblax-commander", "Zorblax"));
        nav.Register(PanelTab.Navigation, new NavCrumb("zorblax-navigation", "Zorblax"));

        var answer = PanelFindPhrases.Answer("find the zorblax page", find);

        Assert.Equal(
            "Three places: Commander › Zorblax, Asset Mgmt › Zorblax, or Navigation › Zorblax. Which one?",
            answer?.Said);
    }

    [Fact]
    public void MoreThanThreeTiedPlacesNameOnlyThree()
    {
        var (find, _, nav) = FindRanksPagesSettingsAndHelpTests.Build();
        nav.Register(PanelTab.Assets, new NavCrumb("zorblax-assets", "Zorblax"));
        nav.Register(PanelTab.Commander, new NavCrumb("zorblax-commander", "Zorblax"));
        nav.Register(PanelTab.Navigation, new NavCrumb("zorblax-navigation", "Zorblax"));
        nav.Register(PanelTab.Stories, new NavCrumb("zorblax-stories", "Zorblax"));

        var answer = PanelFindPhrases.Answer("find the zorblax page", find);

        Assert.Null(answer?.Open);
        Assert.Equal(
            "Several places: Stories › Zorblax, Commander › Zorblax, or Asset Mgmt › Zorblax. Which one?",
            answer?.Said);
    }

    [Fact]
    public void APageBeatsAHelpPageOfTheSameName()
    {
        var results = Find().Search("ships");

        Assert.Contains(results, r => r.Kind == PanelFindKind.HelpPage && r.Rank == 1);

        var answer = PanelFindPhrases.Answer("show me the ships page", Find());

        Assert.Equal(PanelFindKind.Page, answer?.Open?.Kind);
        Assert.Equal("Asset Mgmt › Ships.", answer?.Said);
    }

    [Fact]
    public void AResultRankedAheadOfTheSecondIsOpenedDespiteOthersFollowing()
    {
        var (find, _, nav) = FindRanksPagesSettingsAndHelpTests.Build();
        nav.Register(PanelTab.Commander, new NavCrumb("zorblax", "Zorblax"));
        nav.Register(PanelTab.Navigation, new NavCrumb("zorblax-two", "Zorblax two"));

        var answer = PanelFindPhrases.Answer("find the zorblax page", find);

        Assert.Equal("zorblax", answer?.Open?.Target);
        Assert.Equal("Commander › Zorblax.", answer?.Said);
    }
}
