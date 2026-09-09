using D47.Core.Help;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>The ELI5 bands, read as the panel reads them.</summary>
public class HelpLibraryTests
{
    /// <summary>The sweep.</summary>
    [Fact]
    public void EveryShippedPageEitherHasNoBandOrParses()
    {
        var broken = new List<string>();

        foreach (var id in HelpLibrary.Pages)
        {
            try
            {
                HelpLibrary.Parse(HelpLibrary.PageFor(id), id);
            }
            catch (Exception failure)
            {
                broken.Add($"{id}.md: {failure.Message}");
            }
        }

        Assert.True(broken.Count == 0, string.Join(Environment.NewLine, broken));
    }

    /// <summary>Every page is reachable as a resource, so the csproj glob really did glob.</summary>
    [Fact]
    public void ThePagesAreEmbedded()
    {
        Assert.Contains("engineers", HelpLibrary.Pages);
        Assert.Contains("privacy", HelpLibrary.Pages);
        Assert.True(HelpLibrary.Pages.Count >= 40, $"Only {HelpLibrary.Pages.Count} pages embedded.");
    }

    /// <summary>The first band written for the panel, read end to end.</summary>
    [Fact]
    public void TheEngineersBandReadsAsFourIllustratedSteps()
    {
        var article = HelpLibrary.For("engineers");

        Assert.NotNull(article);
        Assert.Equal("Engineers", article.Title);
        Assert.Equal("Who can improve your ship, where they are, and who to go and get next.", article.Intro);
        Assert.Equal(4, article.Sections.Count);

        Assert.Equal(["1", "2", "3", "4"], article.Sections.Select(s => s.Number));
        Assert.Equal("Two lists.", article.Sections[0].Heading);
        Assert.All(article.Sections, section => Assert.NotNull(section.Figure));

        // The last step is the only one carrying prose, and it is the argument for the tab.
        Assert.Null(article.Sections[0].Body);
        Assert.Contains("oracle", article.Sections[3].Body);
    }

    /// <summary>A figure keeps its own coordinate space and its shapes in paint order.</summary>
    [Fact]
    public void AFigureCarriesItsViewBoxAndItsShapes()
    {
        var figure = HelpLibrary.For("engineers")!.Sections[0].Figure!;

        Assert.Equal(880, figure.Width);
        Assert.Equal(250, figure.Height);

        var first = Assert.IsType<HelpRectangle>(figure.Shapes[0]);
        Assert.Equal(HelpColour.Surface, first.Fill);
        Assert.Equal(HelpColour.Accent, first.Stroke);
        Assert.Equal(2.5, first.StrokeWidth);
        Assert.Equal(10, first.Radius);

        var label = figure.Shapes.OfType<HelpLabel>().First();
        Assert.Equal("DIRECTORY", label.Text);
        Assert.Equal(HelpAnchor.Middle, label.Anchor);
        Assert.True(label.Bold);
    }

    /// <summary>Nothing smaller than 14 in a band.</summary>
    [Fact]
    public void NoBandSetsTextBelowTheHeadsetFloor()
    {
        const double Floor = 14;

        var small = new List<string>();

        foreach (var id in HelpLibrary.Pages)
        {
            if (HelpLibrary.For(id) is not { } article)
            {
                continue;
            }

            foreach (var label in article.Sections
                         .Select(section => section.Figure)
                         .OfType<HelpFigure>()
                         .SelectMany(figure => figure.Shapes.OfType<HelpLabel>())
                         .Where(label => label.Size < Floor))
            {
                small.Add($"{id}.md: \"{label.Text}\" is {label.Size}px, below the {Floor}px floor");
            }
        }

        Assert.True(small.Count == 0, string.Join(Environment.NewLine, small));
    }

    [Fact]
    public void APageWithoutABandIsSimplyNotOffered()
    {
        Assert.Null(HelpLibrary.Parse("---\ntitle: Nothing\n---\n\nJust prose.\n", "nothing"));
        Assert.Null(HelpLibrary.Parse(null, "missing"));
    }

    /// <summary>
    /// The cards block at the foot of a page opens a second <c>div</c>, so the band's end cannot be
    /// found by looking for a closing tag.
    /// </summary>
    [Fact]
    public void TheBandEndsWhereItsOwnDivsBalance()
    {
        const string Page = """
            ---
            title: Two blocks
            ---

            <div class="d47-eli5"><div class="d47-frame">
            <p class="intro">The first.</p>
            <section><h2><span class="num">1</span> One.</h2></section>
            </div></div>

            ## The details

            <div class="d47-eli5"><div class="d47-frame">
            <p class="intro">The second, which must not be read.</p>
            </div></div>
            """;

        var article = HelpLibrary.Parse(Page, "two-blocks");

        Assert.NotNull(article);
        Assert.Equal("The first.", article.Intro);
        Assert.Single(article.Sections);
    }

    [Fact]
    public void AColourLiteralIsRefused()
    {
        var failure = Assert.Throws<FormatException>(() => HelpLibrary.Parse(Band(
            """<svg viewBox="0 0 10 10"><rect x="0" y="0" width="1" height="1" fill="#FF7100"/></svg>"""), "bad"));

        Assert.Contains("colour by role", failure.Message);
    }

    /// <summary>And so is a role that is not one of the nine.</summary>
    [Fact]
    public void AnInventedRoleIsRefused()
    {
        var failure = Assert.Throws<FormatException>(() => HelpLibrary.Parse(Band(
            """<svg viewBox="0 0 10 10"><rect x="0" y="0" width="1" height="1" fill="var(--highlight)"/></svg>"""), "bad"));

        Assert.Contains("--highlight", failure.Message);
    }

    /// <summary>An element the panel cannot draw is refused rather than dropped.</summary>
    [Fact]
    public void AnElementOutsideTheDrawableSetIsRefused()
    {
        var failure = Assert.Throws<FormatException>(() => HelpLibrary.Parse(Band(
            """<svg viewBox="0 0 10 10"><image href="ship.png"/></svg>"""), "bad"));

        Assert.Contains("<image>", failure.Message);
    }

    private static string Band(string figure) =>
        $"""
        ---
        title: Test
        ---

        <div class="d47-eli5"><div class="d47-frame">
        <p class="intro">An intro.</p>
        <section><h2><span class="num">1</span> A step.</h2>{figure}</section>
        </div></div>
        """;

    /// <summary>
    /// The cards at the foot of a band, and the distinction that matters on a surface with no browser:
    /// a page beside this one is something the panel already carries, and everything else is an
    /// address.
    /// </summary>
    [Fact]
    public void ABandsCardsSplitIntoSiblingPagesAndAddresses()
    {
        var article = HelpLibrary.Parse(Cards(
            """
            <a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The fleet.</span></a>
            <a class="card" href="../conversation.html"><span class="ct">Talking →</span><span class="cd">General.</span></a>
            <a class="card" href="https://example.invalid/x"><span class="ct">Away →</span><span class="cd">Off site.</span></a>
            """), "test");

        Assert.NotNull(article);
        Assert.Equal(3, article.Links.Count);

        // The arrow is a web affordance; a button in the panel is already a button.
        Assert.Equal(["Ships", "Talking", "Away"], article.Links.Select(link => link.Title));
        Assert.Equal("The fleet.", article.Links[0].Blurb);

        Assert.Equal("ships", article.Links[0].Article);
        Assert.Null(article.Links[0].Href);

        // A path one folder up reaches a general help page, which this build also carries — so it is a
        // destination rather than an address.
        Assert.Equal("general-conversation", article.Links[1].Article);
        Assert.Null(article.Links[1].Href);

        Assert.Null(article.Links[2].Article);
        Assert.Equal("https://example.invalid/x", article.Links[2].Href);
    }

    /// <summary>The Engineers band names three siblings and nothing off the site.</summary>
    [Fact]
    public void TheEngineersBandPointsAtThreeSiblingPages()
    {
        var links = HelpLibrary.For("engineers")!.Links;

        Assert.Equal(
            ["engineering", "ships", "checklists"],
            links.Select(link => link.Article));

        Assert.All(links, link => Assert.Null(link.Href));
        Assert.All(links, link => Assert.False(string.IsNullOrWhiteSpace(link.Blurb)));
    }

    /// <summary>A band with no cards claims no links rather than a null nobody checked.</summary>
    [Fact]
    public void ABandWithNoCardsHasNoLinks()
    {
        var article = HelpLibrary.Parse(Band("""<svg viewBox="0 0 10 10"/>"""), "bare");

        Assert.NotNull(article);
        Assert.Empty(article.Links);
    }

    private static string Cards(string cards) =>
        $"""
        ---
        title: Test
        ---

        <div class="d47-eli5"><div class="d47-frame">
        <p class="intro">An intro.</p>
        <section><h2><span class="num">1</span> A step.</h2></section>
        <div class="next"><div class="next-title">Where to go next</div><div class="cards">{cards}</div></div>
        </div></div>
        """;

    /// <summary>The frame is found by name, not by being the first child div.</summary>
    [Fact]
    public void ABandWithNoFrameStillParses()
    {
        const string Page = """
            ---
            title: No frame
            ---

            <div class="d47-eli5">
            <p class="intro">Straight in.</p>
            <section><h2><span class="num">1</span> A step.</h2></section>
            <div class="next"><div class="cards">
            <a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The fleet.</span></a>
            </div></div>
            </div>
            """;

        var article = HelpLibrary.Parse(Page, "no-frame");

        Assert.NotNull(article);
        Assert.Equal("Straight in.", article.Intro);
        Assert.Single(article.Sections);
        Assert.Equal("A step.", article.Sections[0].Heading);
        Assert.Equal("ships", Assert.Single(article.Links).Article);
    }

    /// <summary>The three general pages are carried too, under a prefix.</summary>
    [Fact]
    public void TheGeneralPagesAreCarriedToo()
    {
        foreach (var id in new[] { "general-index", "general-install", "general-conversation" })
        {
            Assert.True(HelpLibrary.For(id) is not null, $"{id} parsed to null");
        }

        // And they did not take a capability's name with them on the way in.
        Assert.Contains("title: Language model", HelpLibrary.PageFor("conversation"));
        Assert.Contains("title: Talking to Directive 47", HelpLibrary.PageFor("general-conversation"));
    }

    /// <summary>A page carries where it sits, so the in-app index can read in the site's order.</summary>
    [Fact]
    public void APageKnowsItsGroupAndItsPlaceInTheNav()
    {
        var engineers = HelpLibrary.For("engineers")!;

        Assert.Equal("Knowledge", engineers.Group);
        Assert.Equal(107, engineers.NavOrder);
    }

    /// <summary>The class is a list, and a second word must not cost a card.</summary>
    [Fact]
    public void ACardWithASecondClassIsStillACard()
    {
        var article = HelpLibrary.Parse(
            """
            <div class="d47-eli5"><div class="d47-frame">
            <p class="intro">An intro.</p>
            <div class="cards">
            <a class="card settings" href="speech.html"><span class="ct">Speech →</span><span class="cd">A blurb.</span></a>
            <a class="card" href="ships.html"><span class="ct">Ships →</span></a>
            </div>
            </div></div>
            """,
            "probe");

        Assert.NotNull(article);
        Assert.Equal(2, article.Links.Count);
        Assert.Equal("Speech", article.Links[0].Title);
    }

    /// <summary>
    /// A settings card names its section, and an ordinary one names none — so the panel can tell "take
    /// me to those rows" from "read about this too" without a second attribute.
    /// </summary>
    [Fact]
    public void OnlyTheMarkedCardNamesASettingsSection()
    {
        var article = HelpLibrary.Parse(
            """
            <div class="d47-eli5"><div class="d47-frame">
            <p class="intro">An intro.</p>
            <div class="cards">
            <a class="card settings" href="speech.html"><span class="ct">Speech →</span></a>
            <a class="card" href="ships.html"><span class="ct">Ships →</span></a>
            <a class="card settings" href="https://example.com/"><span class="ct">Away →</span></a>
            </div>
            </div></div>
            """,
            "probe")!;

        Assert.Equal("speech", article.Links[0].Settings);
        Assert.Null(article.Links[1].Settings);

        // An address is not one of this machine's pages, so it cannot be naming a capability either.
        Assert.Null(article.Links[2].Settings);
        Assert.Equal("https://example.com/", article.Links[2].Href);
    }

    /// <summary>
 /// The page about the conversation reading, which is where the help mark on
    /// it goes.
    /// </summary>
    [Fact]
    public void TheInShipPageOffersThreeSettingsSections()
    {
        var article = HelpLibrary.For("general-in-ship");

        Assert.NotNull(article);
        Assert.Equal("In Ship", article.Title);
        Assert.Equal(4, article.Sections.Count);

        Assert.Equal(
            new[] { "listening", "conversation", "speech" },
            article.Links.Select(link => link.Settings).ToArray());
    }

    /// <summary>
 /// And the two readings that are files have pages of their own, each about the reading
    /// rather than about a capability.
    /// </summary>
    [Theory]
    [InlineData("general-log-file", "Log File")]
    [InlineData("general-journal-file", "Journal File")]
    public void EachFileReadingHasAPageOfItsOwn(string id, string title)
    {
        var article = HelpLibrary.For(id);

        Assert.NotNull(article);
        Assert.Equal(title, article.Title);
        Assert.NotEmpty(article.Sections);
    }

    /// <summary>
    /// A bare name means "beside this page", and the general pages are not beside the capabilities.
    /// </summary>
    [Fact]
    public void ABareNameOnAGeneralPageMeansTheGeneralPageBesideIt()
    {
        const string Cards =
            """
            <div class="d47-eli5"><div class="d47-frame">
            <p class="intro">An intro.</p>
            <div class="cards">
            <a class="card" href="conversation.html"><span class="ct">Beside →</span></a>
            <a class="card" href="capabilities/speech.html"><span class="ct">Below →</span></a>
            </div>
            </div></div>
            """;

        var general = HelpLibrary.Parse(Cards, "general-index")!;

        Assert.Equal("general-conversation", general.Links[0].Article);
        Assert.Equal("speech", general.Links[1].Article);

        // And the same markup read as a capability page means the other two pages entirely.
        var capability = HelpLibrary.Parse(Cards, "persona")!;

        Assert.Equal("conversation", capability.Links[0].Article);
        Assert.Null(capability.Links[1].Article);
    }

    /// <summary>Every card on every shipped page reaches something.</summary>
    [Fact]
    public void NoShippedCardNamesAPageThatDoesNotExist()
    {
        var broken = HelpLibrary.Pages
            .Select(id => (Id: id, Article: HelpLibrary.For(id)))
            .Where(page => page.Article is not null)
            .SelectMany(page => page.Article!.Links.Select(link => (page.Id, link.Article)))
            .Where(card => card.Article is { Length: > 0 } target && HelpLibrary.PageFor(target) is null)
            .Select(card => $"{card.Id} → {card.Article}")
            .ToArray();

        Assert.True(broken.Length == 0, string.Join(", ", broken));
    }

    /// <summary>
    /// Every section a card jumps to has a page of its own to fall back on, which is what the headset
    /// gets.
    /// </summary>
    [Fact]
    public void EverySettingsCardHasABandBehindIt()
    {
        var missing = HelpLibrary.Pages
            .Select(HelpLibrary.For)
            .OfType<HelpArticle>()
            .SelectMany(article => article.Links)
            .Where(link => link.Settings is { Length: > 0 })
            .Select(link => link.Settings!)
            .Distinct(StringComparer.Ordinal)
            .Where(id => HelpLibrary.For(id) is null)
            .ToArray();

        Assert.True(missing.Length == 0, $"No band behind: {string.Join(", ", missing)}");
    }

 /// <summary>A second band on a page must not become the one the panel draws.</summary>
    [Fact]
    public void TheHowToBandIsNotWhatThePanelDraws()
    {
        var page = HelpLibrary.PageFor("switches");

        Assert.NotNull(page);

        // The page really does carry both, or this asserts nothing at all.
        Assert.Contains("d47-howto", page, StringComparison.Ordinal);
        Assert.Contains("d47-eli5", page, StringComparison.Ordinal);

        var article = HelpLibrary.For("switches");

        Assert.NotNull(article);

        var drawn = string.Join(" ", article.Sections.Select(section => section.Heading));

        Assert.Contains("The switch is a question", drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("Turn on two rows", drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryHowToBandParsesToo()
    {
        var broken = new List<string>();
        var found = 0;

        foreach (var page in HelpLibrary.Pages)
        {
            var markdown = HelpLibrary.PageFor(page);

            if (markdown is null || !markdown.Contains(HelpLibrary.HowToOpen, StringComparison.Ordinal))
            {
                continue;
            }

            found++;

            try
            {
                var band = HelpLibrary.ParseHowTo(markdown, page);

                Assert.NotNull(band);

                // A band with no sections is a band that silently lost its steps — the exact failure the
                // frame-by-name comment one method up already records once.
                if (band.Sections.Count == 0)
                {
                    broken.Add($"{page}: parsed, and has no sections");
                }
            }
            catch (Exception ex)
            {
                broken.Add($"{page}: {ex.Message}");
            }
        }

        Assert.True(broken.Count == 0, string.Join(Environment.NewLine, broken));

        // And the sweep is only worth anything if it swept something.
        Assert.True(found > 40, $"only {found} pages carry a how-to band");
    }
}
