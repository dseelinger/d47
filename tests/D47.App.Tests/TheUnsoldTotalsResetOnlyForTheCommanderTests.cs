using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Navigation › Unsold data: both totals, and a reset the page makes as the Commander (#556).</summary>
[Trait("Category", "Integration")]
public class TheUnsoldTotalsResetOnlyForTheCommanderTests
{
    private const long SystemAddress = 3274702866819;

    private const string System = "Praea Euq BF-A d95";

    private static readonly DateTimeOffset ResetTime = new(2026, 10, 4, 19, 42, 0, TimeSpan.Zero);

    private const string Commander =
        """{"timestamp":"2026-10-04T17:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""";

    private static readonly string[] Organic =
    [
        $$"""{"timestamp":"2026-10-04T17:10:00Z","event":"Scan","ScanType":"Detailed","BodyName":"{{System}} 41","BodyID":41,"SystemAddress":{{SystemAddress}},"Landable":true,"WasFootfalled":false}""",
        Analyse("2026-10-04T17:20:00Z", 41, "Cactoida Cortexum", "$Codex_Ent_Cactoid_01_Name;"),
        Analyse("2026-10-04T17:30:00Z", 7, "Frutexa Acus", "$Codex_Ent_Shrubs_02_Name;"),
        Analyse("2026-10-04T17:40:00Z", 41, "Tessera Nova", "$Codex_Ent_Tessera_01_Name;"),
    ];

    private static readonly string[] Maps =
    [
        Scan("2026-10-04T18:00:00Z", 3),
        Mapped("2026-10-04T18:05:00Z", 3, probes: 5),
        Scan("2026-10-04T18:10:00Z", 4),
        Mapped("2026-10-04T18:15:00Z", 4, probes: 9),
    ];

    private static string Analyse(string at, int body, string species, string symbol) =>
        $$"""{"timestamp":"{{at}}","event":"ScanOrganic","ScanType":"Analyse","Genus":"$Codex_Ent_{{species.Split(' ')[0]}}_Genus_Name;","Genus_Localised":"{{species.Split(' ')[0]}}","Species":"{{symbol}}","Species_Localised":"{{species}}","SystemAddress":{{SystemAddress}},"Body":{{body}}}""";

    private static string Scan(string at, int body) =>
        $$"""{"timestamp":"{{at}}","event":"Scan","ScanType":"Detailed","BodyName":"{{System}} {{body}}","BodyID":{{body}},"StarSystem":"{{System}}","SystemAddress":{{SystemAddress}},"PlanetClass":"High metal content body","TerraformState":"","MassEM":1.738816,"WasDiscovered":true,"WasMapped":false}""";

    private static string Mapped(string at, int body, int probes) =>
        $$"""{"timestamp":"{{at}}","event":"SAAScanComplete","BodyName":"{{System}} {{body}}","SystemAddress":{{SystemAddress}},"BodyID":{{body}},"ProbesUsed":{{probes}},"EfficiencyTarget":7}""";

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private sealed class Fixture
    {
        public required GameStateStore GameState { get; init; }

        public required ExobiologyLedger Exobiology { get; init; }

        public required CartographyLedger Cartography { get; init; }

        public required CapabilityRegistry Registry { get; init; }
    }

    private static Fixture Carrying(IFileSystem? files = null)
    {
        var gameState = new GameStateStore();
        gameState.Apply(Parse(Commander));

        var path = files is null ? null : Path.Combine(@"C:\d47-test", "unsold.json");
        var exobiology = new ExobiologyLedger(path, files ?? new MemoryFileSystem(), NullLogger.Instance);
        var cartography = new CartographyLedger(path, files ?? new MemoryFileSystem(), NullLogger.Instance);
        exobiology.Load();
        cartography.Load();
        exobiology.FoldHistory([], TestContext.Current.CancellationToken);
        cartography.FoldHistory([], TestContext.Current.CancellationToken);
        exobiology.Apply([.. new[] { Commander }.Concat(Organic).Select(Parse)]);
        cartography.Apply([.. new[] { Commander }.Concat(Maps).Select(Parse)]);

        var settings = TestSurface.CreateFull().Settings;

        var registry = CapabilityRegistry.Build(
        [
            ExobiologyCapability.Create(null, () => gameState.Active, settings, now: () => ResetTime, ledger: exobiology),
            JournalCapability.Create(gameState, cartography: cartography, now: () => ResetTime),
        ]);

        return new Fixture { GameState = gameState, Exobiology = exobiology, Cartography = cartography, Registry = registry };
    }

    private static (Window Window, PanelView Panel) Open(Fixture fixture)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
            () => System,
            fixture.Registry,
            Commander: () => fixture.GameState.Active,
            Exobiology: () => fixture.Exobiology,
            Cartography: () => fixture.Cartography));

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(PanelTab.Navigation, UnsoldPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static IEnumerable<TextBlock> Texts(Visual root) => root.GetVisualDescendants().OfType<TextBlock>();

    private static bool Shows(Visual root, string text) => Texts(root).Any(block => block.Text == text);

    private static int Count(Visual root, string text) => Texts(root).Count(block => block.Text == text);

    private static void Press(Visual root, string name)
    {
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static UnsoldResetDialog? Dialog(Window window) =>
        window.GetVisualDescendants().OfType<UnsoldResetDialog>().SingleOrDefault();

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        var path = name;
        frame.SaveCapture(path);
    }

    [AvaloniaFact]
    public void UnsoldDataComesAfterOnThisBody()
    {
        var fixture = Carrying();
        var (window, panel) = Open(fixture);

        var roots = panel.Nav.Roots(PanelTab.Navigation).Select(root => root.Key).ToList();
        Assert.Equal(UnsoldPage.RootKey, roots[^1]);
        Assert.Equal(BodyPage.RootKey, roots[^2]);

        window.Close();
    }

    [AvaloniaFact]
    public void EachLedgerShowsItsValueCountsAndWhatHasNoPrice()
    {
        using var look = AppLook.Put();
        var fixture = Carrying();
        var (window, panel) = Open(fixture);

        var organic = fixture.Exobiology.Unsold("F1");
        var maps = fixture.Cartography.Unsold("F1");

        Assert.True(Shows(panel, SessionPage.Credits(organic.Total + maps.Total)));
        Assert.True(Shows(panel, SessionPage.Credits(organic.Total)));
        Assert.True(Shows(panel, SessionPage.Credits(maps.Total)));
        Assert.True(Shows(panel, "SPECIES"));
        Assert.True(Shows(panel, "FIRST FOOTFALL"));
        Assert.True(Shows(panel, "BODIES"));
        Assert.True(Shows(panel, "MAPPED EFFICIENTLY"));
        Assert.Equal(2, Count(panel, "NO PRICE · NOT IN TOTAL"));
        Assert.Equal(2, Count(panel, UnsoldPage.ResetHelp));
        Capture(window, "navigation-unsold.png");

        window.Close();
    }

    [AvaloniaFact]
    public void ConfirmingTheResetZeroesTheTotalAndSaysWhen()
    {
        using var look = AppLook.Put();
        var fixture = Carrying();
        var (window, panel) = Open(fixture);

        var before = fixture.Exobiology.Unsold("F1").Total;
        Press(panel, UnsoldPage.OrganicResetName);

        var dialog = Dialog(window);
        Assert.NotNull(dialog);
        Assert.True(dialog.Width <= Controls.Modal.Width);
        Assert.True(Shows(dialog, "RESET ORGANIC DATA"));
        Assert.True(Shows(dialog, UnsoldResetDialog.Hint("reset unsold exobiology")));
        Assert.Contains(
            dialog.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == SessionPage.Credits(before)) == true);
        Capture(window, "navigation-unsold-reset-dialog.png");

        Press(window, UnsoldResetDialog.ConfirmName);

        Assert.Null(Dialog(window));
        Assert.Empty(fixture.Exobiology.Unsold("F1").Held);
        Assert.NotEmpty(fixture.Cartography.Unsold("F1").Held);
        Assert.True(Shows(panel, "0 CR"));
        Assert.True(Shows(panel, UnsoldPage.ResetLine(ResetTime)));
        Assert.Equal(1, Count(panel, UnsoldPage.ResetHelp));
        Capture(window, "navigation-unsold-after-reset.png");

        window.Close();
    }

    [AvaloniaFact]
    public void CancellingTheResetLeavesTheTotal()
    {
        var fixture = Carrying();
        var (window, panel) = Open(fixture);

        Press(panel, UnsoldPage.ExplorationResetName);
        Assert.True(Shows(Dialog(window)!, "RESET EXPLORATION DATA"));

        Press(window, UnsoldResetDialog.CancelName);

        Assert.Null(Dialog(window));
        Assert.NotEmpty(fixture.Cartography.Unsold("F1").Held);
        Assert.Null(fixture.Cartography.ResetAt("F1"));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ThePagesResetIsTheOneTheModelIsRefused()
    {
        var fixture = Carrying();

        foreach (var tool in new[] { ExobiologyCapability.ResetTool, JournalCapability.ResetExplorationTool })
        {
            var refused = await fixture.Registry.InvokeAsync(
                tool, ToolArguments.Empty, TestContext.Current.CancellationToken, ToolCaller.Model);
            Assert.True(refused.IsError);
        }

        Assert.NotEmpty(fixture.Exobiology.Unsold("F1").Held);
        Assert.NotEmpty(fixture.Cartography.Unsold("F1").Held);
        Assert.Null(fixture.Exobiology.ResetAt("F1"));
        Assert.Null(fixture.Cartography.ResetAt("F1"));

        var (window, panel) = Open(fixture);
        Press(panel, UnsoldPage.ExplorationResetName);
        Press(window, UnsoldResetDialog.ConfirmName);

        Assert.Equal(ResetTime, fixture.Cartography.ResetAt("F1"));
        Assert.Empty(fixture.Cartography.Unsold("F1").Held);

        window.Close();
    }

    [AvaloniaFact]
    public void TheResetTimeIsReadBackAfterARestart()
    {
        var files = new MemoryFileSystem();
        var first = Carrying(files);
        first.Exobiology.Reset("F1", ResetTime);
        first.Cartography.Reset("F1", ResetTime);

        var restarted = Carrying(files);
        var (window, panel) = Open(restarted);

        Assert.Equal(2, Count(panel, UnsoldPage.ResetLine(ResetTime)));

        window.Close();
    }

    [AvaloniaFact]
    public void ANewAnalysisRedrawsThePage()
    {
        var fixture = Carrying();
        var (window, panel) = Open(fixture);

        panel.TickRouting();
        Assert.False(panel.TickRouting());

        fixture.Exobiology.Apply([Parse(Analyse("2026-10-04T18:30:00Z", 7, "Cactoida Cortexum", "$Codex_Ent_Cactoid_01_Name;"))]);

        Assert.True(panel.TickRouting());
        Assert.True(Shows(panel, SessionPage.Credits(fixture.Exobiology.Unsold("F1").Total)));
        Assert.False(panel.TickRouting());

        window.Close();
    }

    [AvaloniaFact]
    public void TheFooterAsksWhatExplorationDataIsCarried()
    {
        var (window, panel) = Open(Carrying());

        Assert.True(Shows(panel, $"Say: “{UnsoldPage.Phrase}”"));
        Assert.Contains("exploration data am i carrying", UnsoldPage.Phrase.ToLowerInvariant(), StringComparison.Ordinal);

        window.Close();
    }
}
