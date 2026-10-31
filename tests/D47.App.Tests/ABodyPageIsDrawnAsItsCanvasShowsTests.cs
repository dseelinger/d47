using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Renders System › Bodies and each section of a planet's and a star's page, for comparison with the canvas.</summary>
[Trait("Category", "Integration")]
public sealed class ABodyPageIsDrawnAsItsCanvasShowsTests
{
    private const long Address = 7786;
    private const string Placeholders = @"C:\d47\codex-placeholders";

    private static readonly DateTimeOffset Reported = new(2026, 10, 29, 21, 6, 0, TimeSpan.Zero);

    private static readonly StarSystemProfile Profile = new()
    {
        Name = "LTT 7786",
        SystemAddress = Address,
        ReportedAt = Reported,
        Bodies =
        [
            new BodyProfile
            {
                BodyId = 0,
                Name = "LTT 7786 A",
                Type = "Star",
                SubType = "K (Yellow-Orange) Star",
                SpectralClass = "K7",
                Luminosity = "V",
                IsMainStar = true,
                SolarMasses = 0.66,
                SolarRadius = 0.71,
                SurfaceTemperature = 4120,
                Age = 4210,
                AbsoluteMagnitude = 7.14,
                RotationalPeriod = 38.6,
                Rings = [new RingProfile("LTT 7786 A A Belt", "Metal Rich", new Dictionary<string, int>())],
                ReportedAt = Reported,
            },
            new BodyProfile
            {
                BodyId = 4,
                Name = "LTT 7786 A 2",
                Type = "Planet",
                SubType = "High metal content world",
                ParentId = 0,
                DistanceToArrival = 1198,
                IsLandable = true,
                Gravity = 0.41,
                SurfaceTemperature = 412,
                SurfacePressure = 0.02,
                Radius = 3112,
                EarthMasses = 0.18,
                Atmosphere = "Thin sulphur dioxide",
                Volcanism = "Minor metallic magma",
                ReserveLevel = "Pristine",
                Rings = [new RingProfile("LTT 7786 A 2 A Ring", "Metallic", new Dictionary<string, int> { ["Platinum"] = 2, ["Painite"] = 1 })],
                Signals = new Dictionary<string, int> { ["$SAA_SignalType_Biological;"] = 2, ["$SAA_SignalType_Geological;"] = 4 },
                Genera = ["Bacterium", "Fungoida"],
                AtmosphereComposition = new Dictionary<string, double> { ["Sulphur dioxide"] = 100 },
                SolidComposition = new Dictionary<string, double> { ["Rock"] = 67.2, ["Metal"] = 32.8 },
                Materials = new Dictionary<string, double>
                {
                    ["Iron"] = 21.4, ["Nickel"] = 16.2, ["Sulphur"] = 15.9, ["Carbon"] = 13.4, ["Chromium"] = 9.6,
                    ["Manganese"] = 8.8, ["Phosphorus"] = 8.6, ["Zinc"] = 5.8, ["Tellurium"] = 1.4,
                },
                OrbitalPeriod = 14.21,
                SemiMajorAxis = 0.0218,
                OrbitalEccentricity = 0.0012,
                OrbitalInclination = 0.31,
                ArgOfPeriapsis = 112.4,
                RotationalPeriod = 14.21,
                TidallyLocked = true,
                AxialTilt = 0.04,
                ReportedAt = Reported,
            },
            new BodyProfile
            {
                BodyId = 5,
                Name = "LTT 7786 A 2 a",
                Type = "Planet",
                SubType = "Icy body",
                ParentId = 4,
                DistanceToArrival = 1199,
                IsLandable = true,
                Signals = new Dictionary<string, int> { ["$SAA_SignalType_Geological;"] = 1 },
            },
            new BodyProfile
            {
                BodyId = 6,
                Name = "LTT 7786 A 3",
                Type = "Planet",
                SubType = "Gas giant with water based life",
                ParentId = 0,
                DistanceToArrival = 2880,
            },
        ],
        Stations =
        [
            new StationProfile { Name = "Brin Hub", Body = "LTT 7786 A 2", Kind = StationKind.Starport, Type = "Coriolis Starport", LargestPad = PadSize.Large, DistanceToArrival = 1204 },
            new StationProfile { Name = "Okafor Works", Body = "LTT 7786 A 2", Kind = StationKind.SurfacePort, Type = "Planetary Port", LargestPad = PadSize.Large, DistanceToArrival = 1198 },
        ],
    };

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(systemAddress == Address ? Profile : null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class NotFound : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    [AvaloniaFact]
    public void EachSectionOfAPlanetAndAStarIsCaptured()
    {
        using var look = AppLook.Put();

        var files = new MemoryFileSystem();

        foreach (var file in new[] { "stars.jpg", "terrestrial-planets.jpg", "gas-giant-planets.jpg", "organic-structures.jpg" })
        {
            files.WriteBytes(Path.Combine(Placeholders, file), Png(48));
        }

        var store = new CodexPictureStore(files, @"C:\d47\data", () => true, NullLogger.Instance, new NotFound(), commander: () => "F1");

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCodex(() => null, () => HistoryState.Done, () => Reported, new CodexPictureSet(store, () => true, files, Placeholders));
        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Address },
            () => true,
            () => { })
        {
            SurfaceMining = (_, body) => body == 4 ? Mined() : null,
        });

        var window = new Window { Content = panel, Width = 1280, Height = 1120 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(StarSystemPage.RootKey);
        Until(() => panel.GetVisualDescendants().OfType<Segment>().Any());

        var bodies = panel.GetVisualDescendants().OfType<Segment>().First();
        bodies.GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Save(window, "body-page-system-bodies.png");

        Enter(Row(panel, "LTT 7786 A 2"));
        var planet = Page(panel);

        foreach (var section in planet.Sections)
        {
            planet.Show(section);
            Save(window, $"body-page-planet-{section.ToString().ToLowerInvariant()}.png");
        }


        panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();
        Enter(Row(panel, "LTT 7786 A"));
        var star = Page(panel);

        foreach (var section in star.Sections)
        {
            star.Show(section);
            Save(window, $"body-page-star-{section.ToString().ToLowerInvariant()}.png");
        }

        window.Close();
    }

    private static SurfaceMiningSite Mined()
    {
        var at = Reported.AddDays(-2);
        var run = new SurfaceMiningRun(at, null)
        {
            Refined = ImmutableDictionary<string, RefinedMaterial>.Empty
                .Add("gold", new RefinedMaterial("gold", "Gold", 42))
                .Add("silver", new RefinedMaterial("silver", "Silver", 18))
                .Add("bertrandite", new RefinedMaterial("bertrandite", "Bertrandite", 6))
                .Add("indite", new RefinedMaterial("indite", "Indite", 3))
                .Add("gallite", new RefinedMaterial("gallite", "Gallite", 1)),
            ClosedAt = at.AddHours(1),
        };

        return new SurfaceMiningSite(Address, 4, "LTT 7786", "LTT 7786 A 2", [run, run with { OpenedAt = at.AddHours(2) }]);
    }

    private static Border Row(Control within, string name) =>
        within.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains(D47.App.Theming.ListRow.Class) && Avalonia.Automation.AutomationProperties.GetName(border) == name);

    private static SystemBodyPage Page(PanelView panel)
    {
        Until(() => panel.GetVisualDescendants().OfType<SystemBodyPage>().Any(page => page.Body is not null));

        return panel.GetVisualDescendants().OfType<SystemBodyPage>().Single();
    }

    private static void Enter(Control row)
    {
        row.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = row });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Until(Func<bool> done)
    {
        var stop = DateTime.UtcNow.AddSeconds(10);

        while (!done() && DateTime.UtcNow < stop)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    private static byte[] Png(int side)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(side, side), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var pixels = new byte[side * side * 4];

            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0x40;
                pixels[i + 1] = 0x70;
                pixels[i + 2] = 0x90;
                pixels[i + 3] = 0xFF;
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }
}
