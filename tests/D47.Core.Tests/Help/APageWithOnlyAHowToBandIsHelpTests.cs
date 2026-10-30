using D47.Core.Help;
using D47.Core.Interface;
using D47.Core.Tests.Interface;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>A page with either band is a help page the app can open, list and find (#1890).</summary>
public class APageWithOnlyAHowToBandIsHelpTests
{
    public static TheoryData<string> HowToPages => ["about", "bookmarks", "learned-phrases", "mining", "privacy"];

    [Theory]
    [MemberData(nameof(HowToPages))]
    public void TheHowToBandMakesItAPage(string id)
    {
        Assert.NotNull(HelpLibrary.HowTo(id));
        Assert.True(HelpLibrary.Has(id));
        Assert.Equal(HelpLibrary.HowTo(id)!.Intro, HelpLibrary.IndexLine(id));
    }

    [Theory]
    [MemberData(nameof(HowToPages))]
    public void FindListsIt(string id)
    {
        var title = HelpLibrary.Lead(id)!.Title;

        Assert.Contains(
            FindRanksPagesSettingsAndHelpTests.Build().Find.Search(title),
            result => result.Kind == PanelFindKind.HelpPage && result.Target == id);
    }

    [Fact]
    public void ThePageOpensFromItsLevel()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Assets, new NavCrumb("about", "About") { Help = "about" });
        nav.Select(PanelTab.Assets);

        Assert.True(HelpLevel.Open(nav));
        Assert.Equal("help:about", nav.Trail[^1].Key);
    }

    [Fact]
    public void TheHowToBandIsCachedLikeTheOther() =>
        Assert.Same(HelpLibrary.HowTo("engineers"), HelpLibrary.HowTo("engineers"));

    [Fact]
    public void AnIndexLineFallsBackToHowItWorks()
    {
        var page = HelpLibrary.Pages.First(id => HelpLibrary.HowTo(id) is null && HelpLibrary.For(id) is not null);

        Assert.Equal(HelpLibrary.For(page)!.Intro, HelpLibrary.IndexLine(page));
    }

    [Fact]
    public void APageWithNeitherBandIsNotHelp()
    {
        Assert.False(HelpLibrary.Has("no-such-page"));
        Assert.False(HelpLibrary.Has(null));
        Assert.Equal(string.Empty, HelpLibrary.IndexLine("no-such-page"));
    }

    [Fact]
    public void AHowToBandFindsTheFootCardsWhenTheOnlyOtherBandIsTheFoot()
    {
        const string Page = """
            ---
            title: Foot only
            ---

            <div class="d47-howto"><div class="d47-frame">
            <p class="intro">One step.</p>
            <section><h2><span class="num">1</span> Do it.</h2><p class="say">Say "do it".</p></section>
            </div></div>

            <div class="d47-eli5"><div class="d47-frame">
            <div class="cards"><a class="card" href="engineers.html"><span class="ct">Engineers →</span></a></div>
            </div></div>
            """;

        var howTo = HelpLibrary.ParseHowTo(Page, "foot-only")!;

        Assert.Equal("engineers", Assert.Single(howTo.Links).Article);
    }
}
