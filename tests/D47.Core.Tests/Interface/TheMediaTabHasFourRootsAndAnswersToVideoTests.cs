using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class TheMediaTabHasFourRootsAndAnswersToVideoTests
{
    private static PanelNavigator Furnished()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Media, new NavCrumb("music", "Music"));
        nav.Register(PanelTab.Media, new NavCrumb("podcasts", "Podcasts"));
        nav.Register(PanelTab.Media, new NavCrumb("books", "Books"));
        nav.Register(PanelTab.Media, new NavCrumb("movies", "Movies") { Spoken = ["video", "videos"] });

        return nav;
    }

    [Fact]
    public void TheTabRowReadsInTheOrderTheDesignGives()
    {
        Assert.Equal(
            [PanelTab.Transcript, PanelTab.Search, PanelTab.Commander, PanelTab.Astrometrics, PanelTab.Stories, PanelTab.Media, PanelTab.Modules, PanelTab.Settings],
            Enum.GetValues<PanelTab>());
    }

    [Theory]
    [InlineData("Media", PanelTab.Media)]
    [InlineData("Stories", PanelTab.Stories)]
    [InlineData("Adventures", PanelTab.Stories)]
    [InlineData("Checklist", PanelTab.Commander)]
    [InlineData("Loadout", PanelTab.Commander)]
    [InlineData("Assets", PanelTab.Commander)]
    [InlineData("Routing", PanelTab.Astrometrics)]
    public void AStoredTabNameStillOpensTheSameTab(string saved, PanelTab expected) =>
        Assert.Equal(expected, PanelTabNames.Parse(saved));

    [Theory]
    [InlineData("media")]
    [InlineData("media tab")]
    [InlineData("open the media tab")]
    public void MediaIsReachedByName(string spoken)
    {
        var nav = Furnished();

        Assert.NotNull(PanelPhrases.Apply(spoken, nav));
        Assert.Equal(PanelTab.Media, nav.Tab);
    }

    [Theory]
    [InlineData("video")]
    [InlineData("videos")]
    [InlineData("open videos")]
    public void VideoOpensMovies(string spoken)
    {
        var nav = Furnished();

        Assert.NotNull(PanelPhrases.Apply(spoken, nav));
        Assert.Equal(PanelTab.Media, nav.Tab);
        Assert.Equal("movies", nav.RootKeyOf(PanelTab.Media));
    }
}
