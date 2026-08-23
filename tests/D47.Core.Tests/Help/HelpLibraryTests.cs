using D47.Core.Help;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>
/// The ELI5 bands, read as the panel reads them. These are gates rather than examples: the bands
/// are authored in markdown that nothing compiles, so the only thing standing between a slip of
/// the pen and a broken diagram in a headset is this file.
/// </summary>
public class HelpLibraryTests
{
    /// <summary>
    /// <b>The sweep.</b> Every embedded page must either carry no band or parse cleanly — an
    /// element outside the drawable set, a colour written as a literal, or a malformed number all
    /// throw, and this is where they are heard.
    /// <para>
    /// It matters most for the pages nobody is looking at. A band added to a capability page a
    /// year from now is covered by this the day it lands, with no test to remember to write.
    /// </para>
    /// </summary>
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
        Assert.Equal("Who can improve your ship, where they are, and who to go and get next.", article.Lede);
        Assert.Equal(4, article.Sections.Count);

        Assert.Equal(["1", "2", "3", "4"], article.Sections.Select(s => s.Number));
        Assert.Equal("Two lists.", article.Sections[0].Heading);
        Assert.All(article.Sections, section => Assert.NotNull(section.Figure));

        // The last step is the only one carrying prose, and it is the argument for the tab.
        Assert.Null(article.Sections[0].Body);
        Assert.Contains("oracle", article.Sections[3].Body);
    }

    /// <summary>
    /// A figure keeps its own coordinate space and its shapes in paint order.
    /// </summary>
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

    /// <summary>
    /// <b>Nothing smaller than 14 in a band.</b> The big headset panel is 1024 pixels across a
    /// 1.1 m quad at 1.1 m, which is 19 pixels per degree — so 14 px is about 31 arcminutes of cap
    /// height against a ~20 arcminute floor for text meant to be read (list.md Phase 39). A figure
    /// is scaled to the panel's width, near enough one-to-one there, so the number in the markup
    /// is the number in the headset.
    /// <para>
    /// The web would render 9 px perfectly legibly on a monitor a foot away, which is exactly why
    /// this cannot be left to the eye of whoever writes the next band.
    /// </para>
    /// </summary>
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

    /// <summary>A page with no band is not an error. Most of them still have none.</summary>
    [Fact]
    public void APageWithoutABandIsSimplyNotOffered()
    {
        Assert.Null(HelpLibrary.Parse("---\ntitle: Nothing\n---\n\nJust prose.\n", "nothing"));
        Assert.Null(HelpLibrary.Parse(null, "missing"));
    }

    /// <summary>
    /// The cards block at the foot of a page opens a second <c>div</c>, so the band's end cannot
    /// be found by looking for a closing tag. It is counted.
    /// </summary>
    [Fact]
    public void TheBandEndsWhereItsOwnDivsBalance()
    {
        const string Page = """
            ---
            title: Two blocks
            ---

            <div class="d47-eli5"><div class="d47-frame">
            <p class="lede">The first.</p>
            <section><h2><span class="num">1</span> One.</h2></section>
            </div></div>

            ## The details

            <div class="d47-eli5"><div class="d47-frame">
            <p class="lede">The second, which must not be read.</p>
            </div></div>
            """;

        var article = HelpLibrary.Parse(Page, "two-blocks");

        Assert.NotNull(article);
        Assert.Equal("The first.", article.Lede);
        Assert.Single(article.Sections);
    }

    /// <summary>
    /// A colour written as a literal is refused. It would look right to whoever wrote it and
    /// ignore the Commander's theme for everybody else.
    /// </summary>
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

    /// <summary>
    /// An element the panel cannot draw is refused rather than dropped. Silently skipping it is
    /// how a diagram loses its arrows on one surface and keeps them on the other.
    /// </summary>
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
        <p class="lede">A lede.</p>
        <section><h2><span class="num">1</span> A step.</h2>{figure}</section>
        </div></div>
        """;

    /// <summary>
    /// The cards at the foot of a band, and the distinction that matters on a surface with no
    /// browser: a page beside this one is something the panel already carries, and everything
    /// else is an address.
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

        // A path one folder up reaches a general help page, which this build also carries — so it
        // is a destination rather than an address. Prefixed, because docs/conversation.md and
        // docs/capabilities/conversation.md would otherwise both be called "conversation".
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
        <p class="lede">A lede.</p>
        <section><h2><span class="num">1</span> A step.</h2></section>
        <div class="next"><div class="next-title">Where to go next</div><div class="cards">{cards}</div></div>
        </div></div>
        """;

    /// <summary>
    /// The frame is found by name, not by being the first child div. It is a styling wrapper,
    /// and it stopped carrying any styling the day the whole site took the palette — so a band
    /// written without one has to keep working, and the cards block must never be mistaken for
    /// it. Positionally, it would have been: the cards are the next div along.
    /// </summary>
    [Fact]
    public void ABandWithNoFrameStillParses()
    {
        const string Page = """
            ---
            title: No frame
            ---

            <div class="d47-eli5">
            <p class="lede">Straight in.</p>
            <section><h2><span class="num">1</span> A step.</h2></section>
            <div class="next"><div class="cards">
            <a class="card" href="ships.html"><span class="ct">Ships →</span><span class="cd">The fleet.</span></a>
            </div></div>
            </div>
            """;

        var article = HelpLibrary.Parse(Page, "no-frame");

        Assert.NotNull(article);
        Assert.Equal("Straight in.", article.Lede);
        Assert.Single(article.Sections);
        Assert.Equal("A step.", article.Sections[0].Heading);
        Assert.Equal("ships", Assert.Single(article.Links).Article);
    }

    /// <summary>
    /// The three general pages are carried too, under a prefix. Overview and Installing are read
    /// on a monitor, but <em>Talking to Directive 47</em> is exactly the sort of thing wanted in a
    /// headset — and until they were embedded it was the one band written that nothing could
    /// reach.
    /// </summary>
    [Fact]
    public void TheGeneralPagesAreCarriedToo()
    {
        foreach (var id in new[] { "general-index", "general-install", "general-conversation" })
        {
            Assert.True(HelpLibrary.For(id) is not null, $"{id} parsed to null");
        }

        // And they did not take a capability's name with them on the way in. Compared as raw
        // pages rather than as parsed articles, because docs/capabilities/conversation.md has no
        // band yet — the collision is between two files, not between two bands.
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

    /// <summary>
    /// <b>The class is a list, and a second word must not cost a card.</b> This is the one failure
    /// in the settings-jump work that is silent: match the attribute whole and the marked card is
    /// dropped, the band draws one card short, and nothing anywhere is wrong enough to say so.
    /// </summary>
    [Fact]
    public void ACardWithASecondClassIsStillACard()
    {
        var article = HelpLibrary.Parse(
            """
            <div class="d47-eli5"><div class="d47-frame">
            <p class="lede">A lede.</p>
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
    /// A settings card names its section, and an ordinary one names none — so the panel can tell
    /// "take me to those rows" from "read about this too" without a second attribute.
    /// </summary>
    [Fact]
    public void OnlyTheMarkedCardNamesASettingsSection()
    {
        var article = HelpLibrary.Parse(
            """
            <div class="d47-eli5"><div class="d47-frame">
            <p class="lede">A lede.</p>
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

        // An address is not one of this machine's pages, so it cannot be naming a capability
        // either. Marked or not, it stays a link out.
        Assert.Null(article.Links[2].Settings);
        Assert.Equal("https://example.com/", article.Links[2].Href);
    }

    /// <summary>
    /// The page about the Transcript page (asked for 2026-08-23), which is where the help mark on
    /// the default reading now goes. Its three cards are the three settings sections that decide
    /// what that page says — and each has to be marked, or the jump is an ordinary drill.
    /// </summary>
    [Fact]
    public void TheTranscriptPageOffersThreeSettingsSections()
    {
        var article = HelpLibrary.For("general-transcript");

        Assert.NotNull(article);
        Assert.Equal("The Transcript page", article.Title);
        Assert.Equal(4, article.Sections.Count);

        Assert.Equal(
            new[] { "listening", "conversation", "speech" },
            article.Links.Select(link => link.Settings).ToArray());
    }

    /// <summary>
    /// <b>A bare name means "beside this page", and the general pages are not beside the
    /// capabilities.</b> Read without knowing which folder the card was written in,
    /// <c>conversation.html</c> on the Overview page resolved to the Language model page — so the
    /// card saying <em>Talking to Directive 47</em> opened one about providers and billing. The
    /// same complaint that started this work, one page over.
    /// </summary>
    [Fact]
    public void ABareNameOnAGeneralPageMeansTheGeneralPageBesideIt()
    {
        const string Cards =
            """
            <div class="d47-eli5"><div class="d47-frame">
            <p class="lede">A lede.</p>
            <div class="cards">
            <a class="card" href="conversation.html"><span class="ct">Beside →</span></a>
            <a class="card" href="capabilities/speech.html"><span class="ct">Below →</span></a>
            </div>
            </div></div>
            """;

        var general = HelpLibrary.Parse(Cards, "general-index")!;

        Assert.Equal("general-conversation", general.Links[0].Article);
        Assert.Equal("speech", general.Links[1].Article);

        // And the same markup read as a capability page means the other two pages entirely. The
        // climb down is refused there, because a capability page is already in that folder.
        var capability = HelpLibrary.Parse(Cards, "persona")!;

        Assert.Equal("conversation", capability.Links[0].Article);
        Assert.Null(capability.Links[1].Article);
    }

    /// <summary>
    /// Every card on every shipped page reaches something. A sibling id that no page answers to is
    /// a link the panel draws as an address to a page the site does not have either.
    /// </summary>
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
    /// Every section a card jumps to has a page of its own to fall back on, which is what the
    /// headset gets. A marked card whose sibling has no band would be a dead button there.
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
}
