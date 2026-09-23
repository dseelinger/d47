using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Media;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Ships index draws each hull's own artwork on its card, the ship's own page draws it large, and
/// the Hull pictures setting puts both away again (#247).
/// </summary>
public class TheFleetCardsCarryTheirHullTests
{
    /// <summary>The repo's own hull art.</summary>
    private static string Assets
    {
        get
        {
            var at = new DirectoryInfo(AppContext.BaseDirectory);

            while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "assets", "ships")))
            {
                at = at.Parent;
            }

            return at is null
                ? throw new DirectoryNotFoundException("assets/ships not found above the test binary")
                : Path.Combine(at.FullName, "assets", "ships");
        }
    }

    /// <summary>Stand-ins for the large art, beside the tests that need it.</summary>
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ships");

    /// <summary>
    /// Copies named files into a folder, from the fixtures where there is one and from the repo's own
    /// art otherwise.
    /// </summary>
    private static void Stock(string folder, params string[] files)
    {
        Directory.CreateDirectory(folder);

        foreach (var file in files)
        {
            var stand = Path.Combine(Fixtures, file);
            var source = File.Exists(stand) ? stand : Path.Combine(Assets, file);

            File.Copy(source, Path.Combine(folder, file), overwrite: true);
        }
    }

    /// <summary>Puts one hull's art where the app reads it, and points it there.</summary>
    private static string Stocked(D47.Core.AppPaths paths, params string[] files)
    {
        Stock(paths.Ships, files);

        ShipArt.Shipped = null;
        ShipArt.Folder = paths.Ships;

        return paths.Ships;
    }

    /// <summary>A stocked folder for the tests that ask ShipArt directly, with no page involved.</summary>
    private static string Stocked(params string[] files)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-fleet-card-art-store"));

        paths.EnsureCreated();

        return Stocked(paths, files.Length == 0 ? ["corsair.png"] : files);
    }

    private static (PanelView Panel, Action<bool> SetHullArt) Fleet(
        bool drawings = true, params string[] files)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-fleet-card-art-tests"));

        paths.EnsureCreated();

        Stocked(paths, files.Length == 0 ? ["corsair.png"] : files);

        // The rotation is static and outlives a page on purpose — it has to, or opening a ship would stop the
        // video the opening started.
        HullTurntable.Stop();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        // A hull that has been captured, and one that has not.
        ships.BuildFor(12, "Corsair", "Reaper");
        ships.BuildFor(13, "Type8", "Cartage");

        // Read at draw time, the same way the real Hull pictures setting is (#247) — a plain closure over a
        // local rather than a settings store, since nothing here cares what else is on the record.
        var hullArt = drawings;

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableLoadout(ships, checklists, () => null, null, null, () => hullArt);

        var window = new Window { Content = panel, Width = 1400, Height = 700 };

        window.Show();
        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (panel, value =>
        {
            hullArt = value;

            // The redraw a Commander gets from flipping the Settings toggle, without a restart (#247).
            panel.InvalidateLoadout();
            Dispatcher.UIThread.RunJobs();
        });
    }

    private static List<Image> Drawings(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Image>().Where(image => image.Source is Bitmap)];

    /// <summary>Opens the card for a ship, which is what the Commander does to get to its page.</summary>
    private static void Open(PanelView panel, string named)
    {
        // Case-insensitive: the card's own name is drawn upper case (#278).
        var card = panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(block => (block.Text ?? string.Empty).Contains(named, StringComparison.OrdinalIgnoreCase)));

        card.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ACapturedHullIsDrawnOnItsCard()
    {
        var (panel, _) = Fleet();

        // One picture, not two: the Corsair has a capture and the Type-8 does not, and the card for a hull
        // with no drawing has to be a card rather than a gap.
        Assert.Single(Drawings(panel));
    }

    [AvaloniaFact]
    public void AHullWithNoCaptureStillHasItsCard()
    {
        var (panel, _) = Fleet();

        var names = panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        // Case-insensitive: the card's own name is drawn upper case (#278).
        Assert.Contains(names, text => text.Contains("Cartage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, text => text.Contains("Reaper", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void HullPicturesOffPutsThePicturesAway()
    {
        var (panel, _) = Fleet(drawings: false);

        Assert.Empty(Drawings(panel));
    }

    /// <summary>There is one switch for hull artwork, and it is the Settings one (#247).</summary>
    [AvaloniaFact]
    public void TheIndexHasNoLargeCardsSwitchOfItsOwn()
    {
        var (panel, _) = Fleet();

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<CheckBox>(), box => box.Name == "FleetToggle");
    }

    [AvaloniaFact]
    public void ChangingHullPicturesRedrawsTheOpenPageWithoutARestart()
    {
        var (panel, setHullArt) = Fleet();

        Assert.Single(Drawings(panel));

        setHullArt(false);

        Assert.Empty(Drawings(panel));

        setHullArt(true);

        Assert.Single(Drawings(panel));
    }

    [AvaloniaFact]
    public void AHullIsFoundWhateverCaseTheJournalWroteIt()
    {
        Stocked();

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
        Stocked("corsair.png", "python_nx.png", "type8.png", "panthermkii.png", "lakonminer.png");

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
        var folder = Stocked("corsair.png");

        File.Copy(
            Path.Combine(folder, "corsair.png"),
            Path.Combine(folder, "newhull01_nx.png"),
            overwrite: true);

        ShipArt.Folder = folder;

        // The fallback that keeps the folder worth having: no table anywhere knows this symbol, and the file
        // is still found because it is a plain symbol and a plain file name.
        Assert.NotNull(ShipArt.For("NewHull01_NX"));
    }

    /// <summary>
    /// A symbol reaches <c>ShipArt</c> from the journal and becomes part of a path, so anything that is
    /// not a plain symbol is refused rather than sanitised.
    /// </summary>
    [AvaloniaFact]
    public void AHullNameThatIsAPathIsRefused()
    {
        Stocked();

        Assert.Null(ShipArt.For("../../settings"));
        Assert.Null(ShipArt.For("corsair.png"));
        Assert.Null(ShipArt.SpinFile("..\\corsair"));
    }

    [AvaloniaFact]
    public void AStillThatCameWithTheBuildIsFoundWithNothingInTheDataFolder()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-shipped-art"));

        paths.EnsureCreated();
        Stock(paths.ShippedShips, "corsair.png");

        ShipArt.Folder = paths.Ships;
        ShipArt.Shipped = paths.ShippedShips;

        // The reason for the still shipping: a fresh installation has fetched nothing and still draws
        // every hull.
        Assert.NotNull(ShipArt.For("Corsair"));
    }

    [AvaloniaFact]
    public void AStillInTheDataFolderWinsOverTheOneThatShipped()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-shipped-art-beaten"));

        paths.EnsureCreated();

        // The same name in both folders.
        Stock(paths.ShippedShips, "corsair.spin.mp4");
        Stock(paths.Ships, "corsair.spin.mp4");

        ShipArt.Folder = paths.Ships;
        ShipArt.Shipped = paths.ShippedShips;

        // A drawing dropped in by hand still wins, which is what keeps the folder worth having now that every
        // hull's card still arrives with the build.
        Assert.Equal(
            Path.Combine(paths.Ships, "corsair.spin.mp4"), ShipArt.SpinFile("Corsair"));
    }

    /// <summary>Whether a ship's own page is drawing its hull, and at what size.</summary>
    private static List<Image> Large(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<HullPicture>()
            .SelectMany(picture => picture.GetVisualDescendants().OfType<Image>())
            .Where(image => image.Source is Bitmap)];

    [AvaloniaFact]
    public void TheLargePictureIsDrawnOnTheShipsOwnPage()
    {
        var (panel, _) = Fleet(drawings: true, "corsair.png", "corsair.4k.png");

        Open(panel, "Reaper");

        Assert.Single(Large(panel));

        // The figures go with it, wherever it puts them: a page that drew the picture and dropped what the
        // page is about would be a worse page than the one before this existed.
        Assert.NotEmpty(
            panel.GetVisualDescendants()
                .OfType<HullPicture>()
                .Single()
                .GetVisualDescendants()
                .OfType<TextBlock>());
    }

    [AvaloniaFact]
    public void AHullWithNoLargePictureLeavesThePageAsItWas()
    {
        var (panel, _) = Fleet(drawings: true, "corsair.png");

        Open(panel, "Reaper");

        // Built either way, so a fetch that lands later can fill it in — but drawing nothing, which is what
        // "the page as it is today" means.
        Assert.Empty(Large(panel));
        Assert.Contains(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains("Corsair", StringComparison.Ordinal));
    }

    /// <summary>
    /// Hull pictures off means no picture and no turntable on a ship's own page, even for a hull whose
    /// files are already on disk (#247).
    /// </summary>
    [AvaloniaFact]
    public void HullPicturesOffLeavesTheShipsOwnPageWithoutAPictureOrATurntable()
    {
        var (panel, _) = Fleet(drawings: false, "corsair.png", "corsair.4k.png", "corsair.spin.mp4");

        Open(panel, "Reaper");

        Assert.Empty(panel.GetVisualDescendants().OfType<HullPicture>());
        Assert.Empty(Turning(panel));
        Assert.Contains(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains("Corsair", StringComparison.Ordinal));
    }

    /// <summary>
    /// The three marks, and that the first two change where the figures go (the Commander's amendment,
    /// 2026-09-04).
    /// </summary>
    [AvaloniaFact]
    public void ThePictureIsBesideOrWideAndTheFiguresMoveWithIt()
    {
        var (panel, _) = Fleet(drawings: true, "corsair.png", "corsair.4k.png");

        Open(panel, "Reaper");

        var picture = panel.GetVisualDescendants().OfType<HullPicture>().Single();

        Assert.Single(
            picture.GetVisualDescendants().OfType<Button>(),
            button => button.Content as string == "ZOOM");

        // The segment is rebuilt with every size change, so it is looked up again before each press.
        void Press(string size)
        {
            picture.GetVisualDescendants().OfType<RadioButton>().Single(choice => choice.Content as string == size)
                .IsChecked = true;
            Dispatcher.UIThread.RunJobs();
        }

        // Pressed rather than assumed, because the size is kept for the session: a test that opened by
        // asserting the default would pass or fail on which test ran before it.
        Press("WIDE");
        Press("BESIDE");

        // Half the pane: two columns, the figures in one and the picture in the other.
        Assert.Equal(2, picture.ColumnDefinitions.Count);

        Press("WIDE");

        // The width of the pane: one column, the figures under it.
        Assert.Empty(picture.ColumnDefinitions);
        Assert.Equal(2, picture.RowDefinitions.Count);
        Assert.Single(Large(panel));

        Press("BESIDE");

        Assert.Equal(2, picture.ColumnDefinitions.Count);
    }

    /// <summary>
    /// The memory ceiling the issue asks to be asserted rather than intended: a 4K picture is 33 MB of
    /// pixels, so a third one held would be a hundred megabytes of hulls nobody is looking at.
    /// </summary>
    [AvaloniaFact]
    public void NoMoreThanTwoLargePicturesAreHeldAtOnce()
    {
        Stocked("corsair.png", "corsair.4k.png", "anaconda.4k.png", "adder.4k.png");

        Assert.NotNull(ShipArt.Close4K("Corsair"));
        Assert.Equal(1, ShipArt.Held);

        Assert.NotNull(ShipArt.Close4K("Anaconda"));
        Assert.Equal(2, ShipArt.Held);

        Assert.NotNull(ShipArt.Close4K("Adder"));

        // Two, and the literal is the point: this is 66 MB of pixels and a third would be a hundred.
        Assert.Equal(2, ShipArt.CloseHeld);
        Assert.Equal(2, ShipArt.Held);
    }

    [AvaloniaFact]
    public void AHullWithNoTurntableSimplyDoesNotTurn()
    {
        Stocked("corsair.png");

        Assert.Null(ShipArt.SpinFile("Corsair"));
        Assert.False(HullTurntable.Ready("Corsair"));
    }

    [AvaloniaFact]
    public void ATurntableThatIsThereIsReadyToPlay()
    {
        Stocked("corsair.png", "corsair.spin.mp4");

        Assert.NotNull(ShipArt.SpinFile("Corsair"));
        Assert.True(HullTurntable.Ready("Corsair"));
    }

    /// <summary>The decoder, against a real turntable.</summary>
    [AvaloniaFact]
    public void ATurntableDecodesToFramesTheCardCanDraw()
    {
        var folder = Stocked("corsair.png", "corsair.spin.mp4");

        using var video = VideoFrames.Open(Path.Combine(folder, "corsair.spin.mp4"));

        if (video is null)
        {
            return;
        }

        Assert.Equal(1280, video.Size.Width);
        Assert.Equal(720, video.Size.Height);

        var frame = video.Frame();

        Assert.True(video.Next(frame));
        Assert.True(video.Next(frame));
        Assert.False(video.Ended);
    }

    /// <summary>One click, on the card that is still on screen afterwards (reported twice, 2026-09-04).</summary>
    [AvaloniaFact]
    public void OneClickTurnsTheCardThatIsStillOnScreen()
    {
        var (panel, _) = Fleet(drawings: true, "corsair.png", "corsair.spin.mp4");

        Assert.Empty(Turning(panel));

        Open(panel, "Reaper");

        // Skipped where Media Foundation is not installed — a Server SKU can be built without it, and the
        // card keeping its still is a case this code already has an answer for.
        if (VideoFrames.Open(Path.Combine(Fixtures, "corsair.spin.mp4")) is null)
        {
            return;
        }

        Assert.Single(Turning(panel));

        HullTurntable.Stop();

        Assert.Empty(Turning(panel));
    }

    /// <summary>Images that are on screen and showing decoded frames rather than their still.</summary>
    private static List<Image> Turning(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<Image>()
            .Where(image => image.GetVisualParent() is not null && image.Source is WriteableBitmap)];

    /// <summary>The ship whose page is open is filled solid on its card, the way a row is (#110).</summary>
    [AvaloniaFact]
    public void TheShipBeingShownIsFilledOnItsCard()
    {
        var (panel, _) = Fleet();

        Assert.Empty(Outlined(panel));

        Open(panel, "Reaper");

        var card = Assert.Single(Outlined(panel));

        Assert.Contains(
            card.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains("Reaper", StringComparison.OrdinalIgnoreCase));
    }

    private static List<Button> Outlined(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains(ListRow.SelectedClass))];
}
