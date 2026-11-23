using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Game = D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>Show on galaxy map on Search › Systems and Search › System opens Astrometrics › Galaxy map on the system, selected, in the window and on the big panel (#2706).</summary>
[Trait("Category", "Integration")]
public class AGalaxyMapOpensOnASystemFromSearchTests
{
    private static readonly GalaxyMapStar Far = new("Far Nowhere", new StarPosition(500, 20, -300));

    private static List<Button> Glyphs(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => AutomationProperties.GetName(button) == MapGlyph.Name)];

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Show(Control content, double width = 600)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Dark);

        var window = new Window { Content = content, Width = width, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    [AvaloniaFact]
    public void TheMapGlyphSitsTwoPixelsAfterTheCopyGlyphAndPressingItDoesNotPressTheRow()
    {
        var shown = new List<string>();
        var rowPressed = false;
        var name = new TextBlock { Text = "Deciat" };
        var row = new Button { Content = CopyGlyph.Beside(name, "Deciat", _ => Task.FromResult(true), () => shown.Add("Deciat")) };

        row.Click += (_, _) => rowPressed = true;

        var window = Show(row);
        var map = Assert.Single(Glyphs(row));
        var copy = row.GetVisualDescendants().OfType<Button>().Single(button => CopyGlyph.GetCopies(button) == "Deciat");
        var gap = map.TranslatePoint(new Point(0, 0), row)!.Value.X - copy.TranslatePoint(new Point(copy.Bounds.Width, 0), row)!.Value.X;

        Assert.Equal(2, gap, 0.5);

        Press(map);

        Assert.Equal(["Deciat"], shown);
        Assert.False(rowPressed);

        window.Close();
    }

    [AvaloniaFact]
    public void ANameWithNoMapActionHasNoMapGlyphAndNoGap()
    {
        var name = new TextBlock { Text = "Deciat" };
        var row = new StackPanel { Children = { CopyGlyph.Beside(name, "Deciat", _ => Task.FromResult(true)) } };
        var window = Show(row);

        Assert.Empty(Glyphs(row));
        Assert.IsType<Button>(((Grid)row.Children[0]).Children[1]);

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemNoLayerDrawsIsSelectedCentredAndMarkedAtTheZoomItHad()
    {
        var (window, _, page) = Game.InWindow(Game.Played());
        var before = page.Map().Viewport;

        page.ShowSystem(Far);
        Dispatcher.UIThread.RunJobs();

        var map = page.Map();

        Assert.Equal(Far.Name, map.Selected!.Name);
        Assert.Equal(Far.At.X, map.Viewport.CentreX);
        Assert.Equal(Far.At.Z, map.Viewport.CentreZ);
        Assert.Equal(before.LightYearsAcross, map.Viewport.LightYearsAcross);
        Assert.Equal(before.Heading, map.Viewport.Heading);
        Assert.Equal(before.Tilt, map.Viewport.Tilt);
        Assert.True(page.StripShown());
        Assert.Equal("FAR NOWHERE", page.StripName());
        Assert.EndsWith(GalaxyMapPage.NotVisited, page.StripFacts());

        using (var capture = window.CaptureRenderedFrame()!)
        {
            capture.SaveCapture("galaxy-map-selected-undrawn.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void ClearSelectionRemovesAnUndrawnSystemsSelection()
    {
        var (window, _, page) = Game.InWindow(Game.Played());

        page.ShowSystem(Far);
        Game.Tile(page, GalaxyMapPage.ClearSelection).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(page.Map().Selected);
        Assert.False(page.StripShown());

        window.Close();
    }

    [AvaloniaFact]
    public void TheSelectionOfASystemNoLayerDrawsSurvivesAZoom()
    {
        var (window, _, page) = Game.InWindow(Game.Played());

        page.ShowSystem(Far);
        page.Map().View(page.Map().Viewport with { LightYearsAcross = page.Map().Viewport.LightYearsAcross * 2 });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Far.Name, page.Map().Selected!.Name);
        Assert.True(page.StripShown());

        window.Close();
    }

    [AvaloniaFact]
    public void AVisitedSystemReadsVisitedInTheStripAndTheMap()
    {
        var (window, _, page) = Game.InWindow(Game.Played());
        var sol = new GalaxyMapStar("Sol", new StarPosition(0, 0, 0));

        page.ShowSystem(sol);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(GalaxyMapPage.NotVisited, page.StripFacts());

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemALayerDrawsIsSelectedAsThatLayersTarget()
    {
        var game = Game.Played();
        var (window, _, page) = Game.InWindow(game);
        var hop = game.Route.Hops[4];
        var at = hop.Position!.Value;
        var star = new GalaxyMapStar(hop.StarSystem, new StarPosition(at.X, at.Y, at.Z));

        page.ShowSystem(star);
        Dispatcher.UIThread.RunJobs();

        var selected = page.Map().Selected;
        var facts = page.StripFacts();
        var kinds = page.StripKinds();

        page.Map().Clear();
        Dispatcher.UIThread.RunJobs();

        var tapped = page.Map().TapAt(page.Map().Projected(star.At));

        Assert.Equal(tapped, selected);
        Assert.Equal(page.StripFacts(), facts);
        Assert.Equal(page.StripKinds(), kinds);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRequestWaitsForAMapNotYetBuiltOnTheWindowAndOnTheBigPanel()
    {
        foreach (var headset in new[] { false, true })
        {
            var panel = Game.Panel(Game.Played(), headset, root: RoutingPages.PlanRoot);
            var window = new Window { Content = panel, Width = 960, Height = 930 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(panel.GetVisualDescendants().OfType<GalaxyMapPage>());

            ((IPanelTabHost)panel).ShowOnGalaxyMap!(Far);
            Dispatcher.UIThread.RunJobs();

            var page = panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

            Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.Root.Key);
            Assert.Equal(Far.Name, page.Map().Selected!.Name);
            Assert.Equal(Far.At.X, page.Map().Viewport.CentreX);

            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheRequestOnABuiltMapSelectsAtOnce()
    {
        var (window, panel, page) = Game.InWindow(Game.Played());

        ((IPanelTabHost)panel).ShowOnGalaxyMap!(Far);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Far.Name, page.Map().Selected!.Name);

        window.Close();
    }

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(systemAddress == 1
                ? new StarSystemProfile { Name = "Plotted", SystemAddress = 1, Position = new StarPosition(1, 2, 3) }
                : new StarSystemProfile { Name = "Unplotted", SystemAddress = 2 });

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    [AvaloniaTheory]
    [InlineData(1L, 1)]
    [InlineData(2L, 0)]
    public void TheSystemPageHasTheGlyphOnlyForASystemWithAPosition(long address, int glyphs)
    {
        var shown = new List<GalaxyMapStar>();
        var page = new StarSystemPage(
            new StarSystemSurface(new Systems(), () => new JournalLocation("Sol", null, false, null)),
            _ => Task.FromResult(true))
        {
            ShowOnMap = shown.Add,
        };
        var window = Show(page, 1280);

        Assert.Empty(Glyphs(page));

        page.OpenSystem(address);

        var stop = DateTime.UtcNow.AddSeconds(10);

        while (!page.GetVisualDescendants().OfType<Segment>().Any() && DateTime.UtcNow < stop)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(glyphs, Glyphs(page).Count);

        if (glyphs == 1)
        {
            Press(Glyphs(page)[0]);

            Assert.Equal([new GalaxyMapStar("Plotted", new StarPosition(1, 2, 3))], shown);
        }

        window.Close();
    }

    private sealed class Galaxy : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(
                "LTT 7786",
                2,
                [
                    new SystemSummary { Name = "Alioth", Distance = 3, Position = new StarPosition(-33, 72, -20) },
                    new SystemSummary { Name = "Unplaced", Distance = 9 },
                ]));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASystemsRowHasTheGlyphOnlyWithAPositionAndPressingItOpensTheMapNotTheSystem(bool headset)
    {
        var game = Game.Played();
        var (panel, window) = SearchPanelHosting.ShowSearchForm(
            TestSurface.Settings(),
            new Galaxy(),
            new GalaxySearchBoard(),
            new Systems(),
            SystemSearchPage.RootKey,
            headset: headset,
            furnish: furnished => Game.EnableRouting(furnished, game));

        await panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().RunAsync(new Dictionary<string, string>());
        Dispatcher.UIThread.RunJobs();

        var glyph = Assert.Single(Glyphs(panel));

        Press(glyph);

        Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.Root.Key);
        Assert.Equal("Alioth", panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single().Map().Selected!.Name);

        window.Close();
    }
}
