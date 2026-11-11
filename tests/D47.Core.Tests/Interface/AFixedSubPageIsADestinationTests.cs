using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A page one level below a root, declared with a constant key, is somewhere a switch or a phrase can send the panel (#1149).</summary>
public class AFixedSubPageIsADestinationTests
{
    private static readonly NavCrumb Catalogue = new("stories", "Catalogue");

    private static readonly NavCrumb ShortStory = new("adventure.ask", "Short Story");

    private static PanelNavigator Furnished(Func<bool>? canAsk = null)
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Stories, new NavCrumb("adventures", "Stories"));
        nav.Register(PanelTab.Navigation, new NavCrumb("routing.plan", "Plan"));

        Assert.True(nav.Declare("adventures", ShortStory, canAsk ?? (() => true)));
        Assert.True(nav.Declare("adventures", Catalogue));
        Assert.True(nav.Declare("routing.plan", new NavCrumb("routing.progress", "Progress") { Spoken = ["route"] }));

        nav.Register(PanelTab.Assets, new NavCrumb("engineers", "Engineers"));
        Assert.True(nav.Declare(
            "engineers",
            new NavCrumb("engineers.route", "Route") { Spoken = ["engineers route", "engineer route"], SpokenOnly = true }));

        return nav;
    }

    [Fact]
    public void ShowingASubPageLeavesTheTrailAtItsRootAndIt()
    {
        var nav = Furnished();

        Assert.True(nav.Show("stories"));

        Assert.Equal(PanelTab.Stories, nav.Tab);
        Assert.Equal(["adventures", "stories"], nav.Trail.Select(crumb => crumb.Key));
        Assert.Equal("stories", nav.DestinationKey);
    }

    [Fact]
    public void ShowingASubPageReplacesWhateverTheRootWasDrilledTo()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Stories);
        nav.Drill(ShortStory);
        nav.Drill(new NavCrumb("story.read.1", "A chapter"));

        Assert.True(nav.Show("stories"));
        Assert.Equal(["adventures", "stories"], nav.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void ShowingTheSubPageAlreadyOpenMovesNothing()
    {
        var nav = Furnished();
        nav.Show("stories");

        var raised = 0;
        nav.Changed += (_, _) => raised++;

        Assert.False(nav.Show("stories"));
        Assert.Equal(0, raised);
    }

    [Fact]
    public void ShowingARootStillLeavesItsTrailAsItWas()
    {
        var nav = Furnished();
        nav.Show("stories");
        nav.Select(PanelTab.Transcript);

        Assert.True(nav.Show("adventures"));

        Assert.Equal(["adventures", "stories"], nav.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void AKeyAlreadyTakenIsRefused()
    {
        var nav = Furnished();

        Assert.False(nav.Declare("adventures", new NavCrumb("stories", "Another catalogue")));
        Assert.False(nav.Declare("adventures", new NavCrumb("conversation", "Conversation again")));
        Assert.False(nav.Declare("nowhere", new NavCrumb("orphan", "Orphan")));

        nav.Register(PanelTab.Commander, new NavCrumb("adventure.ask", "A root named like a sub-page"));

        Assert.False(nav.Has(PanelTab.Commander));
        Assert.Equal(["Catalogue"], nav.Destinations.Where(page => page.Key == "stories").Select(page => page.Word));
    }

    [Fact]
    public void ASubPageThatCannotOpenIsListedButNotOpened()
    {
        var nav = Furnished(canAsk: () => false);

        Assert.Contains(nav.Destinations, page => page.Key == "adventure.ask");

        Assert.False(nav.Show("adventure.ask"));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
        Assert.Equal(["adventures"], nav.TrailOf(PanelTab.Stories).Select(crumb => crumb.Key));
    }

    [Fact]
    public void EachRootIsListedFollowedByItsSubPages()
    {
        var nav = Furnished();

        Assert.Equal(
            ["conversation", "adventures", "adventure.ask", "stories", "engineers", "engineers.route", "routing.plan", "routing.progress"],
            nav.Destinations.Select(page => page.Key));

        var catalogue = nav.Destinations.Single(page => page.Key == "stories");

        Assert.Equal("Catalogue", catalogue.Word);
        Assert.Equal("Catalogue (Stories)", catalogue.Describe());
        Assert.Equal("adventures", catalogue.Root.Key);
    }

    [Fact]
    public void ARootDrilledToSomethingElseIsStillTheRoot()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Stories);
        nav.Drill(new NavCrumb("story.read.1", "A chapter"));

        Assert.Equal("adventures", nav.DestinationKey);
    }

    [Fact]
    public void SayingASubPagesNameMovesThePanel()
    {
        var nav = Furnished();

        Assert.Equal("Catalogue.", PanelPhrases.Apply("open the catalogue", nav));
        Assert.Equal(["adventures", "stories"], nav.Trail.Select(crumb => crumb.Key));

        Assert.Equal("Progress.", PanelPhrases.Apply("Progress.", nav));
        Assert.Equal(PanelTab.Navigation, nav.Tab);
        Assert.Equal("routing.progress", nav.DestinationKey);
    }

    [Fact]
    public void ASentenceThatOnlyContainsASubPagesNameIsLeftAlone()
    {
        var nav = Furnished();

        Assert.Null(PanelPhrases.Apply("plot a route to the progress beacon", nav));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Theory]
    [InlineData("route")]
    [InlineData("show route")]
    [InlineData("open the route")]
    [InlineData("route page")]
    public void SayingRouteOpensPlanProgress(string phrase)
    {
        var nav = Furnished();

        Assert.NotNull(PanelPhrases.Apply(phrase, nav));
        Assert.Equal(PanelTab.Navigation, nav.Tab);
        Assert.Equal(["routing.plan", "routing.progress"], nav.Trail.Select(crumb => crumb.Key));
        Assert.Equal("routing.progress", nav.DestinationKey);
    }

    [Theory]
    [InlineData("engineers route")]
    [InlineData("engineer route")]
    public void SayingEngineersRouteOpensTheEngineersRoutePage(string phrase)
    {
        var nav = Furnished();

        Assert.NotNull(PanelPhrases.Apply(phrase, nav));
        Assert.Equal(PanelTab.Assets, nav.Tab);
        Assert.Equal("engineers.route", nav.DestinationKey);
    }
}
