using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.ControlReading;

namespace D47.App.Tests;

/// <summary>Search › Signals chooses a kind or a name, runs find_signal and draws the last answer (#1306).</summary>
public sealed class TheSignalsSearchPageDrawsTheLastAnswerTests
{
    private const string HazardousRes = "$MULTIPLAYER_SCENARIO79_TITLE;";

    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Tsv = string.Join(
        "\n",
        SignalIndex.Header,
        "1001\tAlpha\t3\t0\t0\tResourceExtraction\t" + HazardousRes + "\t2026-10-10",
        "1002\tBeta\t0\t4\t0\tResourceExtraction\t" + HazardousRes + "\t2026-10-15",
        "1003\tGamma\t0\t0\t60\tResourceExtraction\t" + HazardousRes + "\t2026-10-15") + "\n";

    private sealed class Index(SignalIndex? index) : ISignalIndexSource
    {
        public Task<SignalIndex?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(index);
    }

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Clipboard : IClipboard
    {
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class World : GalaxySearchWorld
    {
        public World(bool ownScans = true)
            : base(new Systems())
        {
            Own = new SignalSightings(new MemoryFileSystem(), NullLogger.Instance);

            if (ownScans)
            {
                Own.Fold(
                [
                    "{\"timestamp\":\"2026-10-18T10:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Delta\",\"SystemAddress\":2001,\"StarPos\":[1,2,0]}",
                    "{\"timestamp\":\"2026-10-18T09:00:00Z\",\"event\":\"FSSSignalDiscovered\",\"SystemAddress\":2001,\"SignalName\":\"" + HazardousRes + "\",\"SignalType\":\"ResourceExtraction\"}",
                ]);
            }

            Own.FoldHistory([]);

            Registry = CapabilityRegistry.Build(
            [
                GalaxyCapability.Create(
                    null,
                    () => "LTT 7786",
                    Settings,
                    now: () => Now,
                    searches: Board,
                    systems: new Systems(),
                    signalIndex: new Index(SignalIndex.Read(new StringReader(Tsv))),
                    signals: Own,
                    currentPosition: () => StarPosition.Origin),
            ]);
        }

        public SignalSightings Own { get; }

        public Task<ToolResult> AskAsync(params (string Name, string Value)[] values) =>
            AskedByVoice.AskAsync(Registry, Routing, "find_signal", values);
    }

    private static (PanelView Panel, Window Window) Shown(World world, bool headset = false, bool openRoot = true) =>
        SearchPanelHosting.Show(world.Surface, openRoot ? SignalSearchPage.RootKey : null, headset, world.Routing, new Clipboard());

    private static SignalSearchPage Page(Control root) => root.GetVisualDescendants().OfType<SignalSearchPage>().Single();

    private static async Task RunAsync(PanelView panel, GalaxySearchBoard board)
    {
        var before = board.Last(GalaxySearchKind.Signals);
        Press(Named(panel, GalaxySearchPage.RunName));

        for (var waited = 0; waited < 500 && ReferenceEquals(board.Last(GalaxySearchKind.Signals), before); waited++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void SearchOffersSignalsAfterBodies()
    {
        var (panel, window) = Shown(new World());

        Assert.Equal(
            [StarSystemPage.RootKey, SystemSearchPage.RootKey, StationSearchPage.RootKey, OutfittingSearchPage.RootKey, BodySearchPage.RootKey, SignalSearchPage.RootKey, RareGoodsPage.RootKey],
            panel.Nav.Roots(PanelTab.Search).Select(root => root.Key));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ChoosingAKindSendsItsDisplayName()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("Hazardous RES");
        await RunAsync(panel, world.Board);

        Assert.Equal("Hazardous RES", world.Board.Last(GalaxySearchKind.Signals)!.Arguments["signal"]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TypingANameClearsThePickerAndSendsTheName()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("Hazardous RES");
        page.Type("memorial");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("memorial", page.FormArguments()["signal"]);

        await RunAsync(panel, world.Board);

        Assert.Equal("memorial", world.Board.Last(GalaxySearchKind.Signals)!.Arguments["signal"]);

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingAKindClearsTheName()
    {
        var (panel, window) = Shown(new World());
        var page = Page(panel);

        page.Type("memorial");
        page.Pick("Titan");

        Assert.Equal("Titan", Assert.Single(page.FormArguments()).Value);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ASpokenSearchRedrawsAnOpenPageAndOpensTheRootOnEverySurface()
    {
        var world = new World();
        var (open, openHost) = Shown(world);
        var (elsewhere, elsewhereHost) = Shown(world, openRoot: false);
        var (headset, headsetHost) = Shown(world, headset: true, openRoot: false);

        await world.AskAsync(("signal", "Hazardous RES"));

        Assert.All([open, elsewhere, headset], panel =>
        {
            Assert.Equal(PanelTab.Search, panel.Tab);
            Assert.Equal(SignalSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
            Assert.Equal(GalaxySearchView.Answer, Page(panel).View);
            Assert.Equal(4, Rows(panel).Count);
        });

        Assert.Equal("Hazardous RES", Page(open).FormArguments()["signal"]);

        openHost.Close();
        elsewhereHost.Close();
        headsetHost.Close();
    }

    [AvaloniaFact]
    public async Task APressedRowOpensTheStarSystemPageOnItsSystem()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync(("signal", "Hazardous RES"));

        var row = Rows(panel).OfType<Button>().First(button => (string?)button.Tag == "Beta");
        Press(row);

        Assert.Equal(SearchFurnishing.FoundKeyPrefix + 1002, panel.Nav.TrailOf(PanelTab.Search)[^1].Key);

        window.Close();
    }

    [AvaloniaFact]
    public async Task EverySystemNameCarriesACopyGlyph()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        await world.AskAsync(("signal", "Hazardous RES"));

        var copies = panel.GetVisualDescendants().OfType<Button>().Select(CopyGlyph.GetCopies).OfType<string>().ToList();
        Assert.Equal(["LTT 7786", "Delta", "Alpha", "Beta", "Gamma"], copies);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryPressableControlIsAtLeastFortyFourPixelsTall(bool headset)
    {
        var world = new World();
        var (panel, window) = Shown(world, headset);
        var page = Page(panel);

        await world.AskAsync(("signal", "Hazardous RES"));

        if (headset)
        {
            Press(Named(panel, GalaxySearchPage.EditName));
        }

        SearchPageView.SignalPicker(page).IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        var pressable = page.GetVisualDescendants()
            .Where(control => control is Button or CheckBox or RadioButton && control.IsEffectivelyVisible && control.Bounds.Height > 0)
            .ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, control => Assert.True(control.Bounds.Height >= 44, $"{control.GetType().Name} {control.Name} is {control.Bounds.Height}px tall."));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ThePageDrawsAnAnswer()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        var page = Page(panel);

        page.Pick("Hazardous RES");
        await RunAsync(panel, world.Board);

        Assert.Equal(GalaxySearchView.Answer, page.View);
        Assert.Contains("4 SIGNALS", VisibleText(panel));

        if (TestSurface.CapturesWanted)
        {
            Save(window, "signals-answered.png");
        }

        window.Close();

        var (headset, headsetWindow) = Shown(world, headset: true);
        await world.AskAsync(("signal", "Hazardous RES"));

        if (TestSurface.CapturesWanted)
        {
            Save(headsetWindow, "signals-headset-answer.png");
            Press(Named(headset, GalaxySearchPage.EditName));
            Save(headsetWindow, "signals-headset-form.png");
        }

        headsetWindow.Close();
    }

    [AvaloniaFact]
    public void AnAnswerOfOneUsesTheSingularNoun()
    {
        var world = new World();
        world.Board.Post(new GalaxySearchPosting(GalaxySearchKind.Signals, new Dictionary<string, string>(StringComparer.Ordinal), null, new SignalSearchResult(null, 1, [new SignalFinding("Hazardous RES", "Alpha", 3, Now, false, 1001)]), "Signals found.", Now));
        var (panel, window) = Shown(world);

        Assert.Contains("1 SIGNAL", VisibleText(panel));
        Assert.DoesNotContain("1 SIGNALS", VisibleText(panel));

        window.Close();
    }
}
