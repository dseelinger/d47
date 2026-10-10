using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Panel;
using D47.Core;
using D47.Core.Hulls;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>Points the static <c>ShipArt</c> at a file system and folders, so nothing may run beside it.</summary>
[CollectionDefinition(nameof(ShipArtCollection), DisableParallelization = true)]
public class ShipArtCollection;

/// <summary>
/// <c>ShipArt</c> finds a hull's files by symbol, however the journal spelled the hull, in the data folder before
/// the build's, and refuses a symbol that is a path (#247).
/// </summary>
[Collection(nameof(ShipArtCollection))]
public sealed class HullArtIsLookedUpBySymbolTests : IDisposable
{
    private readonly MemoryFileSystem _files = new();

    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "d47-hull-art"), Path.Combine(Path.GetTempPath(), "d47-hull-art", "build"));

    public HullArtIsLookedUpBySymbolTests()
    {
        ShipArt.Files = _files;
        ShipArt.Shipped = null;
        ShipArt.Folder = _paths.Ships;
    }

    public void Dispose()
    {
        ShipArt.Folder = null;
        ShipArt.Shipped = null;
        ShipArt.Files = new DiskFileSystem();
    }

    /// <summary>A small picture; encoding it needs the headless platform, so only an Avalonia test calls it.</summary>
    private static byte[] Picture() => TheCommandersPictureReplacesTheDefaultTests.Jpeg(16, 16, Colors.SteelBlue);

    /// <summary>Puts a picture in the data folder under each name.</summary>
    private void Stock(params string[] names)
    {
        foreach (var name in names)
        {
            _files.WriteBytes(Path.Combine(_paths.Ships, name), Picture());
        }
    }

    [AvaloniaFact]
    public void AHullIsFoundWhateverCaseTheJournalWroteIt()
    {
        Stock("corsair.png");

        // The journal writes "Corsair" and the file is "corsair.png"; a lookup that did not normalise would
        // find nothing and the miss would be silent.
        Assert.NotNull(ShipArt.For("Corsair"));
        Assert.NotNull(ShipArt.For("  CORSAIR "));
        Assert.Null(ShipArt.For("a_hull_that_does_not_exist"));
        Assert.Null(ShipArt.For(null));
    }

    /// <summary>The spelling that cost nine cards of twelve.</summary>
    [AvaloniaFact]
    public void AHullIsFoundBySpellingAsWellAsBySymbol()
    {
        Stock("corsair.png", "python_nx.png", "type8.png", "panthermkii.png", "lakonminer.png");

        Assert.NotNull(ShipArt.For("python_nx"));
        Assert.NotNull(ShipArt.For("Python Mk II"));
        Assert.NotNull(ShipArt.For("Python MkII"));
        Assert.NotNull(ShipArt.For("Type-8 Transporter"));
        Assert.NotNull(ShipArt.For("Panther Clipper Mk II"));
        Assert.NotNull(ShipArt.For("Type-11 Prospector"));

        // And a hull nothing knows still works from the string itself, which is what the drop-in folder is
        // for: a ship Frontier shipped this morning draws before the tables hear of it.
        Assert.Null(ShipArt.For("Some Ship Nobody Has"));
    }

    [AvaloniaFact]
    public void AHullTheTablesHaveNeverHeardOfStillDrawsFromItsFile()
    {
        Stock("newhull01_nx.png");

        // No table anywhere knows this symbol, and the file is still found because it is a plain symbol and a
        // plain file name.
        Assert.NotNull(ShipArt.For("NewHull01_NX"));
    }

    /// <summary>
    /// A symbol reaches <c>ShipArt</c> from the journal and becomes part of a path, so anything that is
    /// not a plain symbol is refused rather than sanitised.
    /// </summary>
    [AvaloniaFact]
    public void AHullNameThatIsAPathIsRefused()
    {
        Stock("corsair.png");

        Assert.Null(ShipArt.For("../../settings"));
        Assert.Null(ShipArt.For("corsair.png"));
        Assert.Null(ShipArt.SpinFile("..\\corsair"));
    }

    [AvaloniaFact]
    public void AStillThatCameWithTheBuildIsFoundWithNothingInTheDataFolder()
    {
        _files.WriteBytes(Path.Combine(_paths.ShippedShips, "corsair.png"), Picture());

        ShipArt.Shipped = _paths.ShippedShips;

        // The reason for the still shipping: a fresh installation has fetched nothing and still draws
        // every hull.
        Assert.NotNull(ShipArt.For("Corsair"));
    }

    [AvaloniaFact]
    public void AStillInTheDataFolderWinsOverTheOneThatShipped()
    {
        // The same name in both folders.
        _files.WriteBytes(Path.Combine(_paths.ShippedShips, "corsair.spin.mp4"), [0]);
        _files.WriteBytes(Path.Combine(_paths.Ships, "corsair.spin.mp4"), [0]);

        ShipArt.Shipped = _paths.ShippedShips;

        // A drawing dropped in by hand still wins, which is what keeps the folder worth having now that every
        // hull's card still arrives with the build.
        Assert.Equal(Path.Combine(_paths.Ships, "corsair.spin.mp4"), ShipArt.SpinFile("Corsair"));
    }

    /// <summary>
    /// The memory ceiling the issue asks to be asserted rather than intended: a 4K picture is 33 MB of
    /// pixels, so a third one held would be a hundred megabytes of hulls nobody is looking at.
    /// </summary>
    [AvaloniaFact]
    public void NoMoreThanTwoLargePicturesAreHeldAtOnce()
    {
        Stock("corsair.png", "corsair.4k.png", "anaconda.4k.png", "adder.4k.png");

        Assert.NotNull(ShipArt.Close4K("Corsair"));
        Assert.Equal(1, ShipArt.Held);

        Assert.NotNull(ShipArt.Close4K("Anaconda"));
        Assert.Equal(2, ShipArt.Held);

        Assert.NotNull(ShipArt.Close4K("Adder"));

        // Two, and the literal is the point: this is 66 MB of pixels and a third would be a hundred.
        Assert.Equal(2, ShipArt.CloseHeld);
        Assert.Equal(2, ShipArt.Held);
    }

    [Fact]
    public void AHullMeshIsReadAndACorruptOneIsNot()
    {
        var mesh = new HullMesh(
            [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
            [new(0, 0, 1), new(0, 0, 1), new(0, 0, 1)],
            [0, 1, 2],
            [new HullPart("hull", 0, 1, false)],
            1f,
            new System.Numerics.Vector3(0, 0.6f, 0.8f));

        using (var written = new MemoryStream())
        {
            mesh.Write(written);
            _files.WriteBytes(Path.Combine(_paths.Ships, "corsair.mesh"), written.ToArray());
        }

        _files.WriteBytes(Path.Combine(_paths.Ships, "adder.mesh"), [1, 2, 3]);

        Assert.NotNull(ShipArt.Mesh("Corsair"));
        Assert.Null(ShipArt.Mesh("Adder"));
        Assert.Null(ShipArt.Mesh("Anaconda"));
    }
}
