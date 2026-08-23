using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Panel;
using D47.Core.Help;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Where a help page's links go (asked for 2026-08-22).
/// <para>
/// Three kinds of destination and only two affordances, because one of the two surfaces has no
/// browser. A sibling page this machine already carries becomes another level of help and is
/// pressable everywhere; everything else is an address, drawn as a button where something can
/// open it and as the address itself where nothing can.
/// </para>
/// </summary>
public class HelpLinksTests
{
    private static PanelNavigator Standing()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Engineers, new NavCrumb("directory", "Directory") { Help = "engineers" });
        nav.Select(PanelTab.Engineers);

        return nav;
    }

    private static HelpArticle Article(params HelpLink[] links) => new()
    {
        CapabilityId = "engineers",
        Title = "Engineers",
        Lede = "A lede.",
        Sections = [],
        Links = links,
    };

    private static (Window Window, Control Page) Open(
        HelpArticle article, PanelNavigator nav, Action<string>? openUrl)
    {
        var page = HelpPageView.Build(article, nav, openUrl);
        var window = new Window { Content = page, Width = 900, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static Button Press(Control page, string label) =>
        page.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    private static IReadOnlyList<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>
    /// <b>A sibling with a band is a drill, not a browser.</b> Following it in the headset pushes
    /// another level, so going back from it is the same word as going back from anything else —
    /// which is the whole reason help is a level rather than a window.
    /// </summary>
    [AvaloniaFact]
    public void ASiblingPageThatIsAlreadyOnTheMachineBecomesAnotherLevel()
    {
        var nav = Standing();

        var (window, page) = Open(
            Article(new HelpLink { Title = "Engineers", Blurb = "The people.", Article = "engineers" }),
            nav,
            openUrl: null);

        var before = nav.Trail.Count;

        Press(page, "Engineers").Command?.Execute(null);
        Press(page, "Engineers").RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(before + 1, nav.Trail.Count);
        Assert.True(nav.Modal, "another level of help took the panel");
        Assert.Equal("help:engineers", nav.Trail[^1].Key);

        window.Close();
    }

    /// <summary>
    /// A sibling whose band nobody has written yet falls back to its page on the site rather than
    /// opening a panel that says there is nothing to read. The day that band is written the same
    /// link becomes a drill, with no edit here.
    /// </summary>
    [AvaloniaFact]
    public void ASiblingWithNoBandYetFallsOutToItsPageOnTheSite()
    {
        var opened = new List<string>();

        // Whichever page still has none, asked of the library rather than named here — naming one
        // makes this test go red the day somebody writes that band, which is backwards.
        var bandless = HelpLibrary.Pages.First(id => HelpLibrary.For(id) is null);

        var (window, page) = Open(
            Article(new HelpLink { Title = "Elsewhere", Article = bandless }),
            Standing(),
            openUrl: opened.Add);

        Press(page, "Elsewhere").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([DocsSite.Capability(bandless)], opened);

        window.Close();
    }

    /// <summary>
    /// <b>Every band ends with the page it is the short form of.</b> The panel draws the band and
    /// nothing beneath it, so the tables, the schemas and the working exist only on the site — a
    /// help page with no way through would quietly hide the documentation.
    /// </summary>
    [AvaloniaFact]
    public void EveryPageOffersTheLongFormItIsTheShortFormOf()
    {
        var opened = new List<string>();

        var (window, page) = Open(Article(), Standing(), openUrl: opened.Add);

        Press(page, "Read the full page")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([DocsSite.Capability("engineers")], opened);

        window.Close();
    }

    /// <summary>
    /// <b>The headset draws an address rather than a control that cannot work.</b> There is no
    /// browser behind a quad, and a button that does nothing costs a Commander the time to find
    /// that out — the rule <c>IFilterablePage</c> already records about a search box that filters
    /// nothing. Written out, it can at least be read and typed later.
    /// </summary>
    [AvaloniaFact]
    public void WithNoBrowserTheAddressIsWrittenOutInstead()
    {
        var (window, page) = Open(
            Article(new HelpLink { Title = "Away", Href = "https://example.invalid/x" }),
            Standing(),
            openUrl: null);

        var shown = Text(page);

        Assert.Contains("https://example.invalid/x", shown);
        Assert.Contains(DocsSite.Capability("engineers"), shown);

        // And no pressable control claiming otherwise.
        Assert.DoesNotContain(
            page.GetVisualDescendants().OfType<Button>(),
            button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "Away"));

        window.Close();
    }

    /// <summary>
    /// A path up out of the capability folder reaches a general help page, which has no band and
    /// never will have one — it is not a capability. It resolves against the site root rather
    /// than against the folder it climbed out of.
    /// </summary>
    [AvaloniaFact]
    public void APathOutOfTheFolderResolvesAgainstTheSiteRoot()
    {
        var opened = new List<string>();

        var (window, page) = Open(
            Article(new HelpLink { Title = "Talking", Href = "../conversation.html" }),
            Standing(),
            openUrl: opened.Add);

        Press(page, "Talking").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([DocsSite.Root + "conversation.html"], opened);

        window.Close();
    }

    /// <summary>
    /// <b>The real one, now that two bands exist.</b> Engineers ends with a link to Checklists
    /// because that is where "put this route on my checklist" lands, and Checklists has a band of
    /// its own — so following it in the headset is a drill rather than an address a Commander
    /// cannot open. Until the second band was written this path had only a synthetic article to
    /// prove it.
    /// </summary>
    [AvaloniaFact]
    public void FollowingEngineersToChecklistsIsADrillBetweenTwoRealBands()
    {
        var engineers = HelpLibrary.For("engineers");
        Assert.NotNull(engineers);

        var toChecklists = Assert.Single(engineers.Links, link => link.Article == "checklists");
        Assert.Equal("Checklists", toChecklists.Title);

        var nav = Standing();
        var (window, page) = Open(engineers, nav, openUrl: null);

        // Drawn as a control rather than as an address, because this surface can reach it.
        Press(page, "Checklists").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("help:checklists", nav.Trail[^1].Key);
        Assert.True(nav.Modal);

        window.Close();
    }
}
