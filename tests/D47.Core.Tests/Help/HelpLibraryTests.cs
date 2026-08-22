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
}
