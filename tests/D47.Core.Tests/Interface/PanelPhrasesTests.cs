using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Moving the panel by saying so (Phase 25). The third of the three routes back that
/// must agree, plus the tabs, the modes and every crumb of the trail.
/// <para>
/// The precision is the half worth testing hardest. A miss costs the Commander a press; a false
/// positive moves the page out from under them mid-sentence, which is the failure the keyword
/// router's own whole-phrase rule already exists against.
/// </para>
/// </summary>
public class PanelPhrasesTests
{
    private static PanelNavigator Furnished()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Transcript, new NavCrumb("technical", "Technical"));
        nav.Register(PanelTab.Transcript, new NavCrumb("log", "Log file"));
        nav.Register(PanelTab.Checklist, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Loadout, new NavCrumb("fleet", "Ships"));

        return nav;
    }

    [Theory]
    [InlineData("checklist")]
    [InlineData("Checklist")]
    [InlineData("checklist.")]
    [InlineData("show the checklist")]
    [InlineData("show me the checklist")]
    [InlineData("open checklist")]
    [InlineData("go to the checklist")]
    [InlineData("take me to the checklist")]
    [InlineData("select the checklist tab")]
    [InlineData("select checklist")]
    [InlineData("checklist tab")]
    [InlineData("show me the checklist tab")]
    // The destination said last, reported 2026-08-23 from the headset. "Set tab to checklist"
    // matched nothing because every combination named the destination in the middle, and "set"
    // was not an opener either — two independent reasons for one miss.
    [InlineData("set tab to checklist")]
    [InlineData("set the tab to checklist")]
    [InlineData("switch to tab checklist")]
    [InlineData("go to tab checklist")]
    [InlineData("tab checklist")]
    [InlineData("set checklist")]
    public void ATabIsReachedByName(string spoken)
    {
        var nav = Furnished();

        Assert.NotNull(PanelPhrases.Apply(spoken, nav));
        Assert.Equal(PanelTab.Checklist, nav.Tab);
    }

    /// <summary>
    /// And a phrase that merely contains the word is not a request for the tab. This is the exact
    /// failure the router's whole-phrase rule was written for: "where is Iran" answered as the
    /// Commander's own position, confidently, about something nobody asked.
    /// </summary>
    [Theory]
    [InlineData("what is on my checklist for the Corsair")]
    [InlineData("add buy limpets to my checklist")]
    [InlineData("show me the ships in Deciat")]
    [InlineData("are the settings saved")]
    // The openers added 2026-08-23 widened the grammar and must not have widened it past whole
    // phrases. Each of these contains one of the new forms and is still not a request.
    [InlineData("set tab to checklist when I am docked")]
    [InlineData("can you set the tab to checklist")]
    [InlineData("what tab checklist is on")]
    public void APhraseThatMerelyMentionsATabIsNotARequestForIt(string spoken)
    {
        var nav = Furnished();

        Assert.Null(PanelPhrases.Apply(spoken, nav));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    /// <summary>A tab the surface was never furnished with is not a destination.</summary>
    [Fact]
    public void AnUnfurnishedTabIsNotReachable()
    {
        var nav = Furnished();

        Assert.Null(PanelPhrases.Apply("show me settings", nav));
        Assert.Equal(PanelTab.Transcript, nav.Tab);
    }

    [Theory]
    [InlineData("back")]
    [InlineData("go back")]
    [InlineData("take me back")]
    [InlineData("up a level")]
    public void BackGoesBackOneLevel(string spoken)
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));

        Assert.NotNull(PanelPhrases.Apply(spoken, nav));
        Assert.Equal(["Ships", "Corsair"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>At a root there is nothing to go back from, and the phrase falls through.</summary>
    [Fact]
    public void BackAtARootMeansNothing()
    {
        var nav = Furnished();

        Assert.Null(PanelPhrases.Apply("back", nav));
    }

    /// <summary>
    /// Each crumb is a word that can be said as well as pressed, which is what makes the
    /// breadcrumb the same affordance in both modalities rather than two vocabularies.
    /// </summary>
    [Fact]
    public void ACrumbOfTheTrailIsAWord()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));

        Assert.NotNull(PanelPhrases.Apply("corsair", nav));
        Assert.Equal(["Ships", "Corsair"], nav.Trail.Select(crumb => crumb.Word));
    }

    /// <summary>The leaf is where you already are, so naming it is not a move.</summary>
    [Fact]
    public void NamingWhereYouAlreadyAreIsNotAMove()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.GoTo(new NavCrumb("ship:12", "Corsair"));

        Assert.Null(PanelPhrases.Apply("corsair", nav));
    }

    /// <summary>A mode of the tab showing, at its root and nowhere else.</summary>
    [Fact]
    public void AModeIsReachedAtTheRoot()
    {
        var nav = Furnished();

        Assert.NotNull(PanelPhrases.Apply("show me the log file", nav));
        Assert.Equal("log", nav.RootKeyOf(PanelTab.Transcript));
    }

    /// <summary>
    /// Saying the name of the tab you are on returns to its root, exactly as pressing it does.
    /// </summary>
    [Fact]
    public void SayingTheTabYouAreOnReturnsToItsRoot()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Loadout);
        nav.GoTo(new NavCrumb("ship:12", "Corsair"), new NavCrumb("slot:3", "Weapon 3"));

        Assert.NotNull(PanelPhrases.Apply("loadout", nav));
        Assert.True(nav.AtRoot);
    }

    /// <summary>Nothing at all is nothing, rather than an exception or a move.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void NothingSaidIsNothingDone(string spoken)
    {
        var nav = Furnished();

        Assert.Null(PanelPhrases.Apply(spoken, nav));
    }
}
