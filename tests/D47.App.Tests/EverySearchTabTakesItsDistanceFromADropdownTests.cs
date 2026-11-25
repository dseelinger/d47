using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.ControlReading;

namespace D47.App.Tests;

/// <summary>Bodies, Stations, Outfitting, Signals and Rare goods take their distance from one dropdown that opens on 500.</summary>
public sealed class EverySearchTabTakesItsDistanceFromADropdownTests
{
    private static readonly DateTimeOffset AskedAt = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Steps = ["10", "100", "250", "500", "1,000", "2,500", "5,000", "10,000", "25,000", "50,000", "101,000"];

    private sealed class Systems : StubStarSystemService
    {
        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);
    }

    private sealed class Galaxy : StubGalaxyService;

    private static (PanelView Panel, Window Window, GalaxySearchBoard Board) Shown(string root)
    {
        var board = new GalaxySearchBoard();
        var (panel, window) = SearchPanelHosting.ShowSearchForm(TestSurface.Settings(), new Galaxy(), board, new Systems(), root);

        return (panel, window, board);
    }

    private static Control Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Control>().Single(control => control is GalaxySearchPage or RareGoodsPage);

    private static IReadOnlyDictionary<string, string> Form(Control page) => page switch
    {
        GalaxySearchPage search => search.FormArguments(),
        RareGoodsPage rares => rares.FormArguments(),
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    public static TheoryData<string> Roots => new()
    {
        BodySearchPage.RootKey,
        StationSearchPage.RootKey,
        OutfittingSearchPage.RootKey,
        SignalSearchPage.RootKey,
        RareGoodsPage.RootKey,
    };

    [AvaloniaTheory]
    [MemberData(nameof(Roots))]
    public void TheDropdownOpensOnFiveHundredAndOffersTheStepsInOrder(string root)
    {
        using var look = AppLook.Put();
        var (panel, window, _) = Shown(root);
        var page = Page(panel);

        SearchDistance.Of(page).IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        var expected = root == BodySearchPage.RootKey ? ["Any", .. Steps] : Steps;

        Assert.Equal(expected, SearchDistance.Options(page));
        Assert.Equal("500", SearchDistance.Shown(page));
        Assert.DoesNotContain("max_distance", Form(page).Keys);
        Assert.Equal(GalaxyQuery.FarthestDistance, DistancePicker.Steps[^1]);

        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(Roots))]
    public void AStepOtherThanTheDefaultIsSentAndClearGoesBackToFiveHundred(string root)
    {
        using var look = AppLook.Put();
        var (panel, window, _) = Shown(root);
        var page = Page(panel);

        SearchDistance.Pick(page, 25000);

        Assert.Equal("25000", Form(page)["max_distance"]);

        if (page is GalaxySearchPage)
        {
            Press(Named(panel, GalaxySearchPage.ClearName));

            Assert.Equal("500", SearchDistance.Shown(page));
            Assert.DoesNotContain("max_distance", Form(page).Keys);
        }

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(StationSearchPage.RootKey, GalaxySearchKind.Stations)]
    [InlineData(OutfittingSearchPage.RootKey, GalaxySearchKind.Outfitting)]
    [InlineData(SignalSearchPage.RootKey, GalaxySearchKind.Signals)]
    public void ADistanceThatIsNotAStepIsAnEntryOfItsOwnAndRunsAsStored(string root, GalaxySearchKind kind)
    {
        using var look = AppLook.Put();
        var board = new GalaxySearchBoard();
        board.Post(new GalaxySearchPosting(kind, new Dictionary<string, string>(StringComparer.Ordinal) { ["max_distance"] = "750" }, "LTT 7786", Empty(kind), "Matched.", AskedAt));

        var (panel, window) = SearchPanelHosting.ShowSearchForm(TestSurface.Settings(), new Galaxy(), board, new Systems(), root);
        var page = Page(panel);

        SearchDistance.Of(page).IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("750", SearchDistance.Shown(page));
        Assert.Equal("750", Form(page)["max_distance"]);
        Assert.Equal(["10", "100", "250", "500", "750", "1,000", "2,500", "5,000", "10,000", "25,000", "50,000", "101,000"], SearchDistance.Options(page));

        window.Close();
    }

    [AvaloniaFact]
    public void ADistanceBeyondTheGalaxyShowsTheFarthestStepAndTheBodiesAsideSaysSo()
    {
        using var look = AppLook.Put();
        var board = new GalaxySearchBoard();
        board.Post(new GalaxySearchPosting(
            GalaxySearchKind.Bodies,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["max_distance"] = "150000" },
            "LTT 7786",
            new BodySearchResult("LTT 7786", 0, []),
            "Matched.",
            AskedAt));

        var (panel, window) = SearchPanelHosting.ShowSearchForm(TestSurface.Settings(), new Galaxy(), board, new Systems(), BodySearchPage.RootKey);
        var page = Page(panel);

        Assert.Equal("101,000", SearchDistance.Shown(page));
        Assert.Equal("101000", Form(page)["max_distance"]);
        Assert.Contains(VisibleText(panel), text => text.StartsWith("WITHIN 101000 LY OF ", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ACodexEntrySelectsAnyUntilADistanceIsPickedAndClearingItSelectsFiveHundredAgain()
    {
        using var look = AppLook.Put();
        var (panel, window, _) = Shown(BodySearchPage.RootKey);
        var page = (BodySearchPage)Page(panel);

        page.Type("codex_entry", "Bacterium Cerbrus");

        Assert.Equal("Any", SearchDistance.Shown(page));
        Assert.DoesNotContain("max_distance", page.FormArguments().Keys);

        page.Type("codex_entry", string.Empty);

        Assert.Equal("500", SearchDistance.Shown(page));

        SearchDistance.Pick(page, 25000);
        page.Type("codex_entry", "Bacterium Cerbrus");

        Assert.Equal("25,000", SearchDistance.Shown(page));
        Assert.Equal("25000", page.FormArguments()["max_distance"]);

        window.Close();
    }

    [AvaloniaFact]
    public void FiveHundredPickedWithACodexEntryIsSent()
    {
        using var look = AppLook.Put();
        var (panel, window, _) = Shown(BodySearchPage.RootKey);
        var page = (BodySearchPage)Page(panel);

        page.Type("codex_entry", "Bacterium Cerbrus");
        SearchDistance.Pick(page, 500);

        Assert.Equal("500", page.FormArguments()["max_distance"]);

        window.Close();
    }

    [Fact]
    public void SignalsAndRareGoodsOpenOnFiveHundred()
    {
        Assert.Equal(500, SignalSearchPage.DefaultDistance);
        Assert.Equal(500, RareGoodsPage.DefaultDistance);
    }

    private static object Empty(GalaxySearchKind kind) => kind switch
    {
        GalaxySearchKind.Signals => new SignalSearchResult("LTT 7786", 0, []),
        _ => new StationSearchResult("LTT 7786", 0, []),
    };
}
