using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Navigation › On this body: the body's signals, the sampling run and the live distance (#555).</summary>
public class TheBodyScreenFollowsTheWalkTests
{
    private const string Body = "Blae Drye XJ-A d14 7 a";

    private const double Radius = 1_000_000;

    private static readonly DateTimeOffset Start = new(2026, 10, 4, 16, 0, 0, TimeSpan.Zero);

    private static readonly string Here =
        $$"""{"event":"Location","StarSystem":"Blae Drye XJ-A d14","SystemAddress":123,"Body":"{{Body}}","BodyID":12,"BodyType":"Planet"}""";

    private static readonly string Scan =
        $$"""{"event":"Scan","BodyName":"{{Body}}","BodyID":12,"SystemAddress":123,"PlanetClass":"Rocky body","MassEM":0.05,"SurfaceGravity":2.746,"Landable":true,"WasDiscovered":false,"WasMapped":false}""";

    private static readonly string Mapped =
        $$"""{"event":"SAASignalsFound","BodyName":"{{Body}}","BodyID":12,"SystemAddress":123,"Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":4},{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":3}],"Genuses":[{"Genus":"$Codex_Ent_Bacterial_Genus_Name;","Genus_Localised":"Bacterium"},{"Genus":"$Codex_Ent_Shrubs_Genus_Name;","Genus_Localised":"Frutexa"},{"Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum"},{"Genus":"$Codex_Ent_Osseus_Genus_Name;","Genus_Localised":"Osseus"}]}""";

    private static readonly string Fss =
        $$"""{"event":"FSSBodySignals","BodyName":"{{Body}}","BodyID":12,"SystemAddress":123,"Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":4}]}""";

    private static string Organic(string type, string genus, string species) =>
        $$"""{"event":"ScanOrganic","ScanType":"{{type}}","Genus":"{{genus}}","Genus_Localised":"{{genus}}","Species":"{{species}}","Species_Localised":"{{species}}","SystemAddress":123,"Body":12}""";

    private static JournalEvent Event(int minute, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Start + TimeSpan.FromMinutes(minute), root.GetProperty("event").GetString()!, root);
    }

    /// <summary>A point <paramref name="metres"/> north of the equator on the body.</summary>
    private static SurfaceFix North(double metres) => new(metres / Radius * 180 / Math.PI, 0, Radius);

    private static GameStatus StandingAt(double metres) => new()
    {
        Latitude = North(metres).Latitude,
        Longitude = 0,
        PlanetRadius = Radius,
        BodyName = Body,
        ReadAt = Start,
    };

    /// <summary>Bacterium finished, Stratum two of three with the second taken at the equator.</summary>
    private static CommanderGameState Walking(string sampled = "Stratum", string species = "Stratum Tectonicas", bool mapped = true)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        state.Apply(Event(0, Here));
        state.Apply(Event(1, Scan));
        state.Apply(Event(2, mapped ? Mapped : Fss));
        state.Apply(Event(3, Organic("Log", "Bacterium", "Bacterium Acies")), North(-2000));
        state.Apply(Event(4, Organic("Sample", "Bacterium", "Bacterium Acies")), North(-1400));
        state.Apply(Event(5, Organic("Sample", "Bacterium", "Bacterium Acies")), North(-800));
        state.Apply(Event(6, Organic("Log", sampled, species)), North(-600));
        state.Apply(Event(7, Organic("Sample", sampled, species)), North(0));

        return state;
    }

    private static Color Published(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private sealed class Live
    {
        public GameStatus Status { get; set; } = GameStatus.Unknown;
    }

    private static (Window Window, PanelView Panel, Live Live) Open(CommanderGameState state, double? metres, long? worth = null)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var live = new Live { Status = metres is { } m ? StandingAt(m) : GameStatus.Unknown };

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableRouting(new RoutingSurface(
            () => NavRoute.None,
            () => "Blae Drye XJ-A d14",
            Commander: () => state,
            Status: () => live.Status,
            WorthIfMapped: (_, _) => worth));

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 1000,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(PanelTab.Navigation, BodyPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel, live);
    }

    private static TextBlock Text(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    private static bool Shows(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    private static bool ShowsPart(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text?.Contains(text, StringComparison.Ordinal) == true);

    private static void Capture(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        var path = Path.Combine(TestSurface.CaptureDirectory, name);
        frame.Save(path, new PngBitmapEncoderOptions());
        Assert.True(File.Exists(path));
    }

    [AvaloniaFact]
    public void OnThisBodyIsTheNavigationTabsLastRoot()
    {
        var (window, panel, _) = Open(Walking(), 340);

        var roots = panel.Nav.Roots(PanelTab.Navigation);
        Assert.Equal(BodyPage.RootKey, roots[^1].Key);
        Assert.Equal("On this body", roots[^1].Word);

        window.Close();
    }

    [AvaloniaFact]
    public void TheBodyIsCyanOverItsSignalsGeneraAndGravity()
    {
        var (window, panel, _) = Open(Walking(), 340);

        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, Body.ToUpperInvariant()).Foreground));
        Assert.True(Shows(panel, "4"));
        Assert.True(Shows(panel, "BACTERIUM · FRUTEXA · STRATUM · OSSEUS"));
        Assert.True(Shows(panel, "0.28 G"));
        Assert.True(Shows(panel, "OTHER SIGNALS"));
        Assert.False(Shows(panel, "UNMAPPED · WORTH IF MAPPED"));

        window.Close();
    }

    [AvaloniaFact]
    public void BeforeTheColonyDistanceItSaysHowFarToGo()
    {
        var (window, panel, _) = Open(Walking(), 340);

        Assert.Equal(Published(ThemeManager.WhiteKey), Ink(Text(panel, "340 M").Foreground));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "KEEP WALKING · 160 M TO GO").Foreground));
        Assert.True(Shows(panel, "OF 500 M FOR A NEW COLONY"));
        Assert.Equal(Published(ThemeManager.BlueKey), Ink(Text(panel, "SAMPLE 2 ✓").Foreground));
        Assert.Equal(Published(ThemeManager.GreyKey), Ink(Text(panel, "SAMPLE 3").Foreground));
        Assert.True(Shows(panel, BodyPage.Measured));
        Capture(window, "navigation-body-walking.png");

        window.Close();
    }

    [AvaloniaFact]
    public void PastTheColonyDistanceTheThirdSampleIsReady()
    {
        var (window, panel, _) = Open(Walking(), 520);

        Assert.Equal(Published(ThemeManager.BlueKey), Ink(Text(panel, "520 M").Foreground));
        Assert.Equal(Published(ThemeManager.BlueKey), Ink(Text(panel, "✓ FAR ENOUGH · TAKE THE THIRD SAMPLE").Foreground));
        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "SAMPLE 3 · READY").Foreground));
        Capture(window, "navigation-body-far-enough.png");

        window.Close();
    }

    [AvaloniaFact]
    public void WalkingRedrawsTheDistanceAndNothingElse()
    {
        var (window, panel, live) = Open(Walking(), 340);

        var title = Text(panel, Body.ToUpperInvariant());

        // The first pass takes the route as seen.
        panel.TickRouting();
        Assert.False(panel.TickRouting());

        live.Status = StandingAt(520);
        Assert.True(panel.TickRouting());
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(panel, "520 M"));
        Assert.True(Shows(panel, "SAMPLE 3 · READY"));
        Assert.Same(title, Text(panel, Body.ToUpperInvariant()));
        Assert.False(panel.TickRouting());

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoColonyDistanceOnlyTheLiveNumberIsShown()
    {
        var (window, panel, _) = Open(Walking("Unknownus", "Unknownus Primus"), 5000);

        Assert.Equal(Published(ThemeManager.WhiteKey), Ink(Text(panel, "5,000 M").Foreground));
        Assert.Equal(Published(ThemeManager.GreyKey), Ink(Text(panel, "COLONY DISTANCE NOT KNOWN").Foreground));
        Assert.False(ShowsPart(panel, "FOR A NEW COLONY"));
        Assert.False(ShowsPart(panel, "READY"));
        Assert.True(ShowsPart(panel, BodyPage.NoColonyDistance));

        // The closest gap d47 has seen accepted is 600 m; it is never drawn as the threshold.
        Assert.False(ShowsPart(panel, "600"));
        Capture(window, "navigation-body-no-colony-distance.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheGenusListShowsDoneSamplingAndNotStarted()
    {
        var (window, panel, _) = Open(Walking(), 340);

        Assert.Equal(Published(ThemeManager.BlueKey), Ink(Text(panel, "✓ DONE · 3 / 3").Foreground));
        Assert.True(Shows(panel, "1,000,000 CR"));
        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "SAMPLING · 2 / 3").Foreground));
        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "Stratum Tectonicas").Foreground));
        Assert.Equal(2, panel.GetVisualDescendants().OfType<TextBlock>().Count(block => block.Text == "NOT STARTED"));
        Assert.Equal(Published(ThemeManager.GreyKey), Ink(Text(panel, "NOT STARTED").Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void AnUnmappedBodyShowsWhatItIsWorthIfMapped()
    {
        var (window, panel, _) = Open(Walking(mapped: false), 340, worth: 1_240_000);

        Assert.True(Shows(panel, "UNMAPPED · WORTH IF MAPPED"));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "~1,240,000 CR").Foreground));

        window.Close();

        var (mappedWindow, mappedPanel, _) = Open(Walking(), 340, worth: 1_240_000);
        Assert.False(Shows(mappedPanel, "~1,240,000 CR"));
        mappedWindow.Close();
    }

    [AvaloniaFact]
    public void WithNoPositionTheBlockSaysSoAndDrawsNoNumber()
    {
        var (window, panel, _) = Open(Walking(), null);

        Assert.True(Shows(panel, BodyPage.NoPosition));
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text?.EndsWith(" M", StringComparison.Ordinal) == true && block.FontSize == TypeScale.Reading);

        window.Close();
    }

    [AvaloniaFact]
    public void TheFooterAsksForTheBodysBiology()
    {
        var (window, panel, _) = Open(Walking(), 340);

        Assert.True(Shows(panel, $"Say: “{BodyPage.Phrase}”"));
        Assert.Contains("biology on this body", BodyPage.Phrase, StringComparison.Ordinal);

        window.Close();
    }
}
