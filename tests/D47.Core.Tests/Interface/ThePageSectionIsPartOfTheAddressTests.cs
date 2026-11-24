using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A page's open section is part of the address every surface shares (#2740).</summary>
public class ThePageSectionIsPartOfTheAddressTests
{
    private const string System = "search.system";
    private const string Body = "body:7";

    private static readonly PageSection Stations = new("Stations", "Stations");
    private static readonly PageSection Bodies = new("Bodies", "Bodies");
    private static readonly PageSection Overview = new("Overview", "Overview");

    private static PanelNavigator Surface(bool search = true)
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("transcript.conversation", "Conversation"));

        if (search)
        {
            nav.Register(PanelTab.Search, new NavCrumb(System, "System"));
        }

        return nav;
    }

    private static (PanelNavigator Window, PanelNavigator Big) OnTheSystemPage(bool bigHasSearch = true)
    {
        var window = Surface();
        var big = Surface(bigHasSearch);
        var mirror = new TranscriptMirror();

        mirror.Lead(window);
        mirror.Add(big);
        Assert.True(window.Select(PanelTab.Search));

        return (window, big);
    }

    [Fact]
    public void ASectionChosenOnOneSurfaceIsOpenOnTheOther()
    {
        var (window, big) = OnTheSystemPage();

        Assert.True(window.ChooseSection(System, Stations));

        Assert.Equal(Stations, big.Section);

        Assert.True(big.ChooseSection(System, Bodies));

        Assert.Equal(Bodies, window.Section);
    }

    [Fact]
    public void ChoosingASectionIsAMoveAndNotingOneIsNot()
    {
        var nav = Surface();
        Assert.True(nav.Select(PanelTab.Search));
        var moves = nav.Moves;

        Assert.True(nav.NoteSection(System, Overview));
        Assert.Equal(moves, nav.Moves);

        Assert.True(nav.ChooseSection(System, Stations));
        Assert.Equal(moves + 1, nav.Moves);

        Assert.False(nav.ChooseSection(System, Stations));
        Assert.Equal(moves + 1, nav.Moves);
    }

    [Fact]
    public void ASectionChosenOnALevelThatIsNotShowingIsNotAMove()
    {
        var nav = Surface();
        Assert.True(nav.Select(PanelTab.Search));
        Assert.True(nav.Drill(new NavCrumb(Body, "Body")));
        var moves = nav.Moves;

        Assert.True(nav.ChooseSection(System, Stations));

        Assert.Equal(moves, nav.Moves);
        Assert.Null(nav.Section);
        Assert.Equal(Stations, nav.SectionOf(System));
    }

    [Fact]
    public void ANotedSectionGoesOnlyWhereNoneIsHeld()
    {
        var (window, big) = OnTheSystemPage();

        Assert.True(window.NoteSection(System, Overview));

        Assert.Equal(Overview, big.Section);

        Assert.True(big.NoteSection(System, Stations));

        Assert.Equal(Overview, window.Section);
    }

    [Fact]
    public void ASectionChoiceRecordsNoPlaceToGoBackTo()
    {
        var (window, big) = OnTheSystemPage();
        var could = window.CanGoPrevious;

        Assert.True(window.ChooseSection(System, Stations));

        Assert.Equal(could, window.CanGoPrevious);
        Assert.Equal(could, big.CanGoPrevious);
    }

    [Fact]
    public void ASurfaceStandingInNamesTheSection()
    {
        var (window, mini) = OnTheSystemPage(bigHasSearch: false);

        Assert.True(window.ChooseSection(System, Stations));

        Assert.Equal(PanelTab.Search, mini.StandingIn?.Tab);
        Assert.Equal(Stations, mini.StandingIn?.Section);
    }

    /// <summary>A page on a surface standing in, such as one that reset itself on a jump, moves no other surface.</summary>
    [Fact]
    public void ASectionChosenWhileStandingInMovesNothing()
    {
        var (window, big) = OnTheSystemPage();

        Assert.True(window.Drill(new NavCrumb("result:1", "A result") { Local = true }));
        Assert.NotNull(big.StandingIn);
        var moves = window.Moves;

        Assert.True(big.ChooseSection(System, Overview));

        Assert.Equal(["search.system", "result:1"], window.Trail.Select(crumb => crumb.Key));
        Assert.Equal(moves, window.Moves);
        Assert.NotNull(big.StandingIn);
    }

    [Fact]
    public void ASurfaceAddedLaterArrivesOnTheSection()
    {
        var window = Surface();
        var mirror = new TranscriptMirror();

        mirror.Lead(window);
        Assert.True(window.Select(PanelTab.Search));
        Assert.True(window.ChooseSection(System, Stations));

        var big = Surface();
        mirror.Add(big);

        Assert.Equal(PanelTab.Search, big.Tab);
        Assert.Equal(Stations, big.Section);
    }

    [Fact]
    public void TheSectionBelongsToTheDeepestLevel()
    {
        var (window, big) = OnTheSystemPage();

        Assert.True(window.ChooseSection(System, Stations));
        Assert.True(window.Drill(new NavCrumb(Body, "Body")));
        Assert.True(window.ChooseSection(Body, new PageSection("Orbit", "Orbit")));

        Assert.Equal("Orbit", big.Section?.Id);
        Assert.Equal(Stations, big.SectionOf(System));

        Assert.True(window.Back());

        Assert.Equal(Stations, big.Section);
    }
}
