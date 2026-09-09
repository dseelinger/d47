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

/// <summary>Where a help page's links go.</summary>
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
        Intro = "An intro.",
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

    /// <summary>A sibling with a band is a drill, not a browser.</summary>
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
    /// opening a panel that says there is nothing to read.
    /// </summary>
    [AvaloniaFact]
    public void ASiblingWithNoBandYetFallsOutToItsPageOnTheSite()
    {
        var opened = new List<string>();

        // Whichever page still has none, asked of the library rather than named here — naming one makes this
        // test go red the day somebody writes that band, which is backwards.
        var bandless = HelpLibrary.Pages.First(id => HelpLibrary.For(id) is null);

        var (window, page) = Open(
            Article(new HelpLink { Title = "Elsewhere", Article = bandless }),
            Standing(),
            openUrl: opened.Add);

        Press(page, "Elsewhere").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([DocsSite.Capability(bandless)], opened);

        window.Close();
    }

    /// <summary>Every band ends with the page it is the short form of.</summary>
    [AvaloniaFact]
    public void EveryPageOffersTheLongFormItIsTheShortFormOf()
    {
        var opened = new List<string>();

        var (window, page) = Open(Article(), Standing(), openUrl: opened.Add);

        Press(page, "More details online")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([DocsSite.Capability("engineers")], opened);

        window.Close();
    }

    /// <summary>The headset draws an address rather than a control that cannot work.</summary>
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
    /// A path up out of the capability folder reaches a general help page, which has no band and never
    /// will have one — it is not a capability.
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

    /// <summary>The real one, now that two bands exist.</summary>
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

    /// <summary>The index is what a mark falls back to.</summary>
    [AvaloniaFact]
    public void AlevelWithNoPageOfItsOwnOpensTheIndex()
    {
        var nav = new PanelNavigator();

        // A root that claims help for a page nobody has illustrated — asked of the library rather than named
        // here, for the reason the sibling test above gives.
        var bandless = HelpLibrary.Pages.First(id => HelpLibrary.For(id) is null);

        nav.Register(PanelTab.Utilities, new NavCrumb("clocks", "Clocks") { Help = bandless });
        nav.Select(PanelTab.Utilities);

        Assert.True(HelpLevel.Open(nav));
        Assert.Equal("help:help", nav.Trail[^1].Key);
    }

    /// <summary>The help page lists every page that has one, generated from the library.</summary>
    [AvaloniaFact]
    public void TheHelpPageListsEveryIllustratedPage()
    {
        var nav = Standing();
        var (window, page) = Open(HelpLibrary.For("help")!, nav, openUrl: null);

        var shown = page.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.Contains("EVERYTHING DRAWN IN HERE", shown);

        var illustrated = HelpLibrary.Pages
            .Where(id => id != "help")
            .Select(HelpLibrary.For)
            .OfType<HelpArticle>()
            .ToList();

        Assert.NotEmpty(illustrated);

        foreach (var article in illustrated)
        {
            Assert.Contains(article.Title, shown);
        }

        // And pressing one drills into it.
        Press(page, "Engineers").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("help:engineers", nav.Trail[^1].Key);

        window.Close();
    }

    /// <summary>A settings card offers the rows, not a second explanation.</summary>
    [AvaloniaFact]
    public void ACardNamingASettingsSectionGoesThereRatherThanIntoMoreHelp()
    {
        var nav = Standing();
        var revealed = new List<string>();

        var page = HelpPageView.Build(
            Article(new HelpLink { Title = "Speech", Article = "speech", Settings = "speech" }),
            nav,
            openUrl: null,
            openSettings: revealed.Add);

        var window = new Window { Content = page, Width = 900, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var before = nav.Trail.Count;

        Press(page, "Speech").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["speech"], revealed);
        Assert.Equal(before, nav.Trail.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSameCardIsAnOrdinaryDrillWhereThereIsNoSettingsTab()
    {
        var nav = Standing();

        var (window, page) = Open(
            Article(new HelpLink { Title = "Speech", Article = "speech", Settings = "speech" }),
            nav,
            openUrl: null);

        Press(page, "Speech").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.True(nav.Modal, "another level of help took the panel");
        Assert.Equal("help:speech", nav.Trail[^1].Key);

        window.Close();
    }
}
