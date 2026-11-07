using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

[Collection(nameof(ShipArtCollection))]
public sealed class TheCarrierPageDrawsTheModelPickedForItTests : IDisposable
{
    /// <summary>Each carrier model's 4K still, told apart by width.</summary>
    private static readonly Dictionary<string, int> Widths = new()
    {
        ["fleetcarrier"] = 30,
        ["fleetcarrier_nautilus"] = 40,
    };

    private const string Art = "/carrier-hull-art";

    private readonly (IFileSystem Files, string? Folder, string? Shipped) _was = (ShipArt.Files, ShipArt.Folder, ShipArt.Shipped);

    public void Dispose()
    {
        ShipArt.Files = _was.Files;
        ShipArt.Folder = _was.Folder;
        ShipArt.Shipped = _was.Shipped;
    }

    /// <summary>Puts each model's still in an in-memory data folder, and points the art there with nothing from the build.</summary>
    private static void PutStills()
    {
        var files = new MemoryFileSystem();

        foreach (var (symbol, width) in Widths)
        {
            using var bitmap = new WriteableBitmap(new PixelSize(width, 10), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using var png = new MemoryStream();

            bitmap.Save(png, new PngBitmapEncoderOptions());
            files.WriteBytes(Path.Combine(Art, symbol + ".4k.png"), png.ToArray());
        }

        ShipArt.Files = files;
        ShipArt.Shipped = null;
        ShipArt.Folder = Art;
    }

    private const string Stats =
        """{"timestamp":"2026-09-22T23:24:52Z","event":"CarrierStats","CarrierID":3715429376,"CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","JumpRangeCurr":500.0,"SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},"Finance":{"CarrierBalance":990302661},"Crew":[{"CrewRole":"Refuel","Activated":true,"Enabled":true}]}""";

    private static CarrierHullMemory Memory() => new(new ViewStateStore(
        new AppPaths("/carrier-hull-tests"),
        new MemoryFileSystem(),
        NullLogger<ViewStateStore>.Instance));

    private static CarrierPage Page(CarrierHullMemory memory)
    {
        Assert.True(JournalEvent.TryParse(Stats, NullLogger.Instance, out var parsed));
        var carrier = CarrierState.None.Apply(parsed!);

        return new CarrierPage(
            new CarrierSource(() => carrier, () => CarrierState.NoSquadron, () => 0),
            hullPictures: true,
            hulls: memory);
    }

    /// <summary>The model whose still the page draws.</summary>
    private static string? Drawn(Window window) =>
        window.GetVisualDescendants().OfType<HullPicture>().Single().GetVisualDescendants().OfType<Image>()
            .Select(image => image.Source as Bitmap)
            .Where(bitmap => bitmap is not null)
            .Select(bitmap => Widths.Single(model => model.Value == bitmap!.PixelSize.Width).Key)
            .SingleOrDefault();

    [AvaloniaFact]
    public void ACarrierWithNoPickIsDrawnAsTheDrake()
    {
        PutStills();
        var window = new Window { Content = Page(Memory()), Width = 924, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fleetcarrier", Drawn(window));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePickedModelIsDrawnAndKeptByANewPage()
    {
        PutStills();
        var memory = Memory();
        var window = new Window { Content = Page(memory), Width = 924, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The model picker; the still brings a size picker of its own.
        var segment = window.GetVisualDescendants().OfType<Segment>().Single(found => !found.GetVisualAncestors().OfType<HullPicture>().Any());
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fleetcarrier_nautilus", Drawn(window));

        window.Close();

        var again = new Window { Content = Page(memory), Width = 924, Height = 640 };

        again.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("fleetcarrier_nautilus", Drawn(again));

        again.Close();
    }

    [Fact]
    public void ACarrierModelIsRememberedPerCarrier()
    {
        var memory = Memory();

        memory.Remember("AAA-111", "fleetcarrier_victory");
        memory.Remember("BBB-222", "fleetcarrier_fortune");

        Assert.Equal("fleetcarrier_victory", memory.Of("AAA-111"));
        Assert.Equal("fleetcarrier_fortune", memory.Of("BBB-222"));
        Assert.Equal("fleetcarrier", memory.Of("CCC-333"));
    }

    [Fact]
    public void AnUnknownModelReadsAsTheDrake()
    {
        var memory = Memory();

        memory.Remember("AAA-111", "fleetcarrier_unheard_of");

        Assert.Equal("fleetcarrier", memory.Of("AAA-111"));
    }
}
