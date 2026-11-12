using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Choosing a capture, cropping it and removing the result, on a codex entry's page (#1370).</summary>
[Trait("Category", "Integration")]
public class TheCodexPicturePagesCropACaptureTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Captures = @"C:\d47\data\captures";

    private const string Local = @"C:\d47\data\codex-pictures\local\F1";

    private static readonly CodexPictureKey Key = new(101, 18);

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":101,\"Name\":\"$Codex_Ent_101;\",\"Name_Localised\":\"Entry 101\","
        + "\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\",\"SubCategory\":\"$Codex_SubCategory_Stars;\","
        + "\"SubCategory_Localised\":\"Stars\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"IsNewEntry\":true}",
    ];

    private sealed class Surface
    {
        public MemoryFileSystem Files { get; } = new();

        public CodexPictureStore Store { get; }

        public CodexPictureSet Pictures { get; }

        public PanelNavigator Nav { get; } = new();

        public CodexPictureWork Work { get; } = new();

        public Surface()
        {
            Store = new CodexPictureStore(Files, Folder, () => false, NullLogger.Instance, commander: () => "F1", screenshots: Captures);
            Pictures = new CodexPictureSet(Store, () => false, Files, @"C:\d47\placeholders", Captures);
            Nav.Register(PanelTab.Commander, new NavCrumb("ledgers", "Ledgers"));
            Nav.Select(PanelTab.Commander);
            Nav.Drill(CodexPage.EntryCrumb(18, 101, "Entry 101"));
        }

        public void Capture(string stem, string at, int width = 1000, int height = 600, params (long Entry, int? Region)[] entries)
        {
            Files.WriteBytes(Path.Combine(Captures, stem + ".jpg"), Jpeg(width, height));
            Files.WriteText(
                Path.Combine(Captures, stem + ".json"),
                new PictureNote(
                    DateTimeOffset.Parse(at, CultureInfo.InvariantCulture),
                    "Elite",
                    width,
                    height,
                    "Sol",
                    1,
                    "B 3",
                    18,
                    [.. entries.Select(entry => new PictureNoteEntry(entry.Entry, "x", entry.Region))]).Write());
        }

        public void OwnPicture(CodexPictureKey key)
        {
            Files.WriteBytes(Path.Combine(Local, key.FileName), Png(8));
        }

        public void OwnPictureWithSource(CodexPictureKey key, CropSquare square)
        {
            Files.WriteBytes(Path.Combine(Captures, "kept.jpg"), Jpeg(1000, 600));
            Store.SaveLocal(key, Png(8), "kept.jpg", square);
        }

        public void Stock(CodexPictureKey key)
        {
            var bytes = Png(16);
            var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
            var row = new CodexPictureRow(key.EntryId, key.RegionId, CodexPictureIndex.FirstTag, CodexPictureIndex.AssetName(key.EntryId, key.RegionId, sha), sha, bytes.Length);
            Files.WriteText(Path.Combine(Folder, CodexPictureStore.StockFolder, CodexPictureIndex.IndexName), CodexPictureIndex.Write([row]));
            Files.WriteBytes(Path.Combine(Folder, CodexPictureStore.StockFolder, row.Asset), bytes);
        }

        public bool Kept(string name) => Files.ReadBytes(Path.Combine(Local, name)) is not null;
    }

    private static byte[] Png(int side) => Encode(side, side, new PngBitmapEncoderOptions());

    private static byte[] Jpeg(int width, int height) => Encode(width, height, new JpegBitmapEncoderOptions { Quality = 90 });

    private static byte[] Encode(int width, int height, BitmapEncoderOptions options)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using (var locked = bitmap.Lock())
        {
            var row = new int[width];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    row[x] = unchecked((int)0xFF000000) | ((x * 255 / width) << 16) | ((y * 255 / height) << 8) | 0x60;
                }

                System.Runtime.InteropServices.Marshal.Copy(row, 0, locked.Address + (y * locked.RowBytes), width);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, options);

        return stream.ToArray();
    }

    private static CommanderGameState State()
    {
        return JournalScript.Commander("F735466", "John Deparagon", Lines);
    }

    private static CodexEntryPage Entry(Surface surface, int? region = 18, bool headset = false) =>
        new(State, 101, region, "Entry 101", surface.Pictures, headset, nav: surface.Nav);

    private static Button? Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name);

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<string> Actions(Control page) =>
        new[] { "CodexChoose", "CodexRecrop", "CodexReplace", "CodexUseStock", "CodexRemoveMine" }.Where(name => Named(page, name) is not null);

    [AvaloniaFact]
    public void WithNoPictureOfTheirOwnTheCommanderCanOnlyChoose()
    {
        var surface = new Surface();
        var (window, page) = PageHost.Open(() => Entry(surface));

        Assert.Equal(["CodexChoose"], Actions(page));
        Assert.Equal("Choose a picture", Named(page, "CodexChoose")!.Content);

        window.Close();
    }

    [AvaloniaFact]
    public void AnotherRegionsPictureOfTheirOwnStillOffersOnlyChoose()
    {
        var surface = new Surface();
        surface.OwnPicture(new CodexPictureKey(101, 3));

        var (window, page) = PageHost.Open(() => Entry(surface));

        Assert.Equal(["CodexChoose"], Actions(page));

        window.Close();
    }

    [AvaloniaFact]
    public void AnOwnPictureWithAStockOneOffersTheStockPictureAndRecropsOnlyWhileItsSourceIsKept()
    {
        var surface = new Surface();
        surface.OwnPicture(Key);
        surface.Stock(Key);

        var stockOn = new CodexPictureSet(
            new CodexPictureStore(surface.Files, Folder, () => true, NullLogger.Instance, commander: () => "F1", screenshots: Captures), () => true, surface.Files, @"C:\d47\placeholders", Captures);
        var (window, page) = PageHost.Open(() => new CodexEntryPage(State, 101, 18, "Entry 101", stockOn, false, null, surface.Nav));

        Assert.Equal(["CodexReplace", "CodexUseStock"], Actions(page));
        Assert.Equal("Use the stock picture", Named(page, "CodexUseStock")!.Content);

        window.Close();

        surface.OwnPictureWithSource(Key, new CropSquare(0, 0, 600));
        var (kept, withSource) = PageHost.Open(() => new CodexEntryPage(State, 101, 18, "Entry 101", stockOn, false, null, surface.Nav));

        Assert.Equal(["CodexRecrop", "CodexReplace", "CodexUseStock"], Actions(withSource));

        kept.Close();

        surface.Files.Delete(Path.Combine(Captures, "kept.jpg"));
        var (gone, withoutSource) = PageHost.Open(() => new CodexEntryPage(State, 101, 18, "Entry 101", stockOn, false, null, surface.Nav));

        Assert.Equal(["CodexReplace", "CodexUseStock"], Actions(withoutSource));

        gone.Close();
    }

    [AvaloniaFact]
    public void UsingTheStockPictureDeletesTheCommandersThreeFiles()
    {
        var surface = new Surface();
        surface.OwnPictureWithSource(Key, new CropSquare(0, 0, 600));
        surface.Stock(Key);
        var stockOn = new CodexPictureSet(
            new CodexPictureStore(surface.Files, Folder, () => true, NullLogger.Instance, commander: () => "F1", screenshots: Captures), () => true, surface.Files, @"C:\d47\placeholders", Captures);

        var (window, page) = PageHost.Open(() => new CodexEntryPage(State, 101, 18, "Entry 101", stockOn, false, null, surface.Nav));
        Press(Named(page, "CodexUseStock")!);
        Dispatcher.UIThread.RunJobs();

        Assert.False(surface.Kept(Key.FileName));
        Assert.False(surface.Kept(Key.SourceFileName));
        Assert.False(surface.Kept(Key.CropFileName));
        Assert.Equal(["CodexChoose"], Actions(page));

        window.Close();
    }

    [AvaloniaFact]
    public void AnOwnPictureWithNoStockOneOffersRemovingIt()
    {
        var surface = new Surface();
        surface.OwnPictureWithSource(Key, new CropSquare(0, 0, 600));

        var (window, page) = PageHost.Open(() => Entry(surface));

        Assert.Equal(["CodexRecrop", "CodexReplace", "CodexRemoveMine"], Actions(page));
        Assert.Equal("Remove my picture", Named(page, "CodexRemoveMine")!.Content);

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-entry-actions.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelAndAnUnnumberedRegionShowNoActions()
    {
        var surface = new Surface();
        surface.OwnPictureWithSource(Key, new CropSquare(0, 0, 600));

        var (headset, big) = PageHost.Open(() => Entry(surface, headset: true));
        Assert.Empty(Actions(big));
        headset.Close();

        var (none, unnumbered) = PageHost.Open(() => Entry(surface, region: null));
        Assert.Empty(Actions(unnumbered));
        none.Close();
    }

    [AvaloniaFact]
    public void RemovingTheCommandersPictureDeletesItsThreeFilesAndRedrawsThePage()
    {
        var surface = new Surface();
        surface.OwnPictureWithSource(Key, new CropSquare(0, 0, 600));

        var (window, page) = PageHost.Open(() => Entry(surface));
        Press(Named(page, "CodexRemoveMine")!);
        Dispatcher.UIThread.RunJobs();

        Assert.False(surface.Kept(Key.FileName));
        Assert.False(surface.Kept(Key.SourceFileName));
        Assert.False(surface.Kept(Key.CropFileName));
        Assert.Equal(["CodexChoose"], Actions(page));

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingListsTheNotedCapturesFirstNewestFirstAndOpensTheCropOnPress()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210800", "2026-10-18T21:08:00Z", entries: (101, 18));
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", entries: (101, 18));
        surface.Capture("capture-20261018-220000", "2026-10-18T22:00:00Z", entries: (101, 3));

        var (window, page) = PageHost.Open(() => new CodexChoosePage(Key, surface.Pictures, surface.Nav, surface.Work));
        var rows = page.GetVisualDescendants().OfType<Button>().Where(button => button.Name?.StartsWith("CodexCapture.", StringComparison.Ordinal) == true).ToList();

        Assert.Equal(
            ["CodexCapture.capture-20261018-210900", "CodexCapture.capture-20261018-210800", "CodexCapture.capture-20261018-220000"],
            rows.Select(row => row.Name));


        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-choose.png");
        }

        Press(rows[1]);

        Assert.Equal(Path.Combine(Captures, "capture-20261018-210800.jpg"), surface.Work.Capture);
        Assert.Equal(CodexPage.CropCrumb(Key, kept: false).Key, surface.Nav.Trail[^1].Key);

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoCapturesTheChoiceSaysHowToTakeOne()
    {
        var surface = new Surface();
        var (window, page) = PageHost.Open(() => new CodexChoosePage(Key, surface.Pictures, surface.Nav, surface.Work));

        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == CodexModel.NoCapturesText);
        Assert.Equal("No screenshots yet. Say \"take a picture\" in game, or press Elite's screenshot key, to take one.", CodexModel.NoCapturesText);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AHeadsetCaptureIsBrightenedOnThePageAndWhenSavedAndTheLiftIsKept()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        var note = Path.Combine(Captures, "capture-20261018-210900.json");
        surface.Files.WriteText(note, surface.Files.ReadText(note)!.Replace("\"source\": \"Elite\"", $"\"source\": \"{ScreenPictures.FromHeadset}\""));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: false));
        await Lifted(crop);

        Assert.True(Blue(Shown(crop.Surface!)) > 140);

        await crop.SaveAsync();
        Dispatcher.UIThread.RunJobs();

        var crops = surface.Files.ReadText(Path.Combine(Local, Key.CropFileName));
        Assert.Equal(0.5, PictureLift.Parse(crops));
        Assert.Equal(0.5, surface.Store.LocalGamma(Key));

        using (var stream = new MemoryStream(surface.Files.ReadBytes(Path.Combine(Local, Key.FileName))!))
        using (var saved = new Bitmap(stream))
        {
            Assert.True(Blue(saved) > 140);
        }

        Assert.False(surface.Kept(Key.SourceFileName));
        Assert.Equal("capture-20261018-210900.jpg", surface.Store.LocalScreenshot(Key));

        using (var stream = new MemoryStream(surface.Store.LocalSource(Key)!))
        using (var kept = new Bitmap(stream))
        {
            Assert.True(Blue(kept) < 110);
        }

        window.Close();

        surface.Nav.Drill(CodexPage.CropCrumb(Key, kept: true));
        var (again, recrop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: true));
        await Lifted(recrop);

        Assert.True(Blue(Shown(recrop.Surface!)) > 140);

        again.Close();
    }

    [AvaloniaFact]
    public void AWindowCaptureAndAnOldCropFileAreNotBrightened()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: false));

        Assert.True(Blue(Shown(crop.Surface!)) < 110);
        window.Close();

        surface.OwnPictureWithSource(Key, new CropSquare(0, 0, 600));
        Assert.Null(surface.Store.LocalGamma(Key));
    }

    [AvaloniaFact]
    public async Task TheBrightnessRowOpensAtTheCapturesStepRedrawsOnEachPressAndSavesThatGamma()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        var note = Path.Combine(Captures, "capture-20261018-210900.json");
        surface.Files.WriteText(note, surface.Files.ReadText(note)!.Replace("\"source\": \"Elite\"", $"\"source\": \"{ScreenPictures.FromHeadset}\""));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: false));
        await Lifted(crop);

        var amount = crop.GetVisualDescendants().OfType<Amount>().Single(a => a.Name == "CodexCropBrightness");
        var reset = Named(crop, "CodexCropBrightnessReset")!;
        var lifted = Blue(Shown(crop.Surface!));

        Assert.Equal(4m, amount.Value);
        Assert.False(reset.IsVisible);

        var down = amount.GetVisualDescendants().OfType<RepeatButton>().First(b => AutomationProperties.GetName(b) == "Decrease");
        Press(down);
        Press(down);
        Press(down);
        Press(down);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0m, amount.Value);
        Assert.True(reset.IsVisible);
        Assert.True(Blue(Shown(crop.Surface!)) < lifted);

        Press(reset);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(4m, amount.Value);
        Assert.False(reset.IsVisible);

        var up = amount.GetVisualDescendants().OfType<RepeatButton>().First(b => AutomationProperties.GetName(b) == "Increase");
        Press(up);
        Press(up);
        Press(up);
        Press(up);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(8m, amount.Value);

        await crop.SaveAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.25, surface.Store.LocalGamma(Key));
        window.Close();

        surface.Nav.Drill(CodexPage.CropCrumb(Key, kept: true));
        var (again, recrop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: true));
        await Lifted(recrop);
        var reopened = recrop.GetVisualDescendants().OfType<Amount>().Single(a => a.Name == "CodexCropBrightness");

        Assert.Equal(8m, reopened.Value);
        Assert.False(Named(recrop, "CodexCropBrightnessReset")!.IsVisible);
        again.Close();
    }

    [AvaloniaFact]
    public void AWindowCaptureOpensAtStepZeroAndSavesNoLift()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: false));
        var amount = crop.GetVisualDescendants().OfType<Amount>().Single(a => a.Name == "CodexCropBrightness");

        Assert.Equal(0m, amount.Value);
        Assert.Equal("0", Amount.Display(0, "+0;-0;0", null));
        Assert.Equal("+4", Amount.Display(4, "+0;-0;0", null));
        window.Close();
    }

    private static async Task Lifted(CodexCropPage page)
    {
        for (var i = 0; i < 250 && page.Surface is null; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }

        Assert.NotNull(page.Surface);
        Dispatcher.UIThread.RunJobs();
        page.UpdateLayout();
    }

    private static Bitmap Shown(CropSurface surface) =>
        (Bitmap)surface.GetVisualDescendants().OfType<Image>().First(image => image.Name == "CropPicture").Source!;

    private static int Blue(Bitmap picture)
    {
        using var stream = new MemoryStream();
        picture.Save(stream, new PngBitmapEncoderOptions());
        stream.Position = 0;
        using var copy = WriteableBitmap.Decode(stream);
        using var locked = copy.Lock();
        var row = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(locked.Address + (locked.Size.Height / 2 * locked.RowBytes) + (locked.Size.Width / 2 * 4), row, 0, 4);

        return row[0];
    }

    [AvaloniaFact]
    public async Task SavingWritesTheThreeFilesAndTheEntryPageThenShowsYourPicture()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");
        surface.Nav.Drill(CodexPage.ChooseCrumb(Key));
        surface.Nav.Drill(CodexPage.CropCrumb(Key, kept: false));

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: false));

        Assert.Equal(new CropSquare(500, 0, 1000), crop.Surface!.Square);

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-crop.png");
        }

        var start = crop.Surface.ToSurface(1000, 500);
        Assert.True(crop.Surface.Press(start));
        crop.Surface.DragTo(crop.Surface.ToSurface(1100, 500));
        crop.Surface.Release();
        Assert.Equal(new CropSquare(600, 0, 1000), crop.Surface.Square);

        await crop.SaveAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Kept(Key.FileName));
        Assert.False(surface.Kept(Key.SourceFileName));
        Assert.Equal(new CropSquare(600, 0, 1000), CropSquare.Parse(surface.Files.ReadText(Path.Combine(Local, Key.CropFileName))));
        Assert.Equal("capture-20261018-210900.jpg", surface.Store.LocalScreenshot(Key));
        Assert.Equal(surface.Files.ReadBytes(Path.Combine(Captures, "capture-20261018-210900.jpg")), surface.Store.LocalSource(Key));

        var saved = surface.Files.ReadBytes(Path.Combine(Local, Key.FileName))!;
        Assert.Equal([0xFF, 0xD8], saved[..2]);
        using (var stream = new MemoryStream(saved))
        using (var decoded = new Bitmap(stream))
        {
            Assert.Equal(new PixelSize(768, 768), decoded.PixelSize);
        }

        Assert.True(CodexPage.IsEntry(surface.Nav.Trail[^1].Key));

        window.Close();

        var (again, entry) = PageHost.Open(() => Entry(surface));
        Assert.Contains(entry.GetVisualDescendants().OfType<TextBlock>(), block => string.Equals(block.Text, "Your picture", StringComparison.OrdinalIgnoreCase));
        again.Close();
    }

    [AvaloniaTheory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task OnlyASaveFromANewCaptureAnnouncesTheEntrysPicture(bool kept, int announced)
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");
        surface.OwnPictureWithSource(Key, new CropSquare(200, 50, 500));
        surface.Nav.Drill(CodexPage.CropCrumb(Key, kept));
        List<CodexPictureKey> saved = [];
        surface.Pictures.PictureSaved += saved.Add;

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept));

        await crop.SaveAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(announced, saved.Count);
        Assert.All(saved, key => Assert.Equal(Key, key));

        window.Close();
    }

    [AvaloniaFact]
    public void RecropOpensOnTheKeptSourceWithTheSavedSquareAndCancelWritesNothing()
    {
        var surface = new Surface();
        surface.OwnPictureWithSource(Key, new CropSquare(200, 50, 500));
        surface.Nav.Drill(CodexPage.CropCrumb(Key, kept: true));
        var before = surface.Files.ReadBytes(Path.Combine(Local, Key.FileName));

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: true));

        Assert.Equal(new CropSquare(200, 50, 500), crop.Surface!.Square);

        Press(Named(crop, "CodexCropCancel")!);

        Assert.Equal(before, surface.Files.ReadBytes(Path.Combine(Local, Key.FileName)));
        Assert.True(CodexPage.IsEntry(surface.Nav.Trail[^1].Key));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACropPageOpenedAgainDrawsTheCropSurfaceAndAnEnabledSave(bool kept)
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000, (101, 18));
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");
        surface.OwnPictureWithSource(Key, new CropSquare(200, 50, 500));

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept));

        for (var visit = 0; visit < 3; visit++)
        {
            window.Content = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Null(crop.Surface);

            window.Content = crop;
            Dispatcher.UIThread.RunJobs();

            Assert.NotNull(crop.Surface);
            Assert.True(Named(crop, "CodexCropSave")!.IsEnabled);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheFrameStaysInsideTheCaptureAndNeverShrinksBelow384Pixels()
    {
        var surface = new Surface();
        surface.Capture("capture-20261018-210900", "2026-10-18T21:09:00Z", 2000, 1000);
        surface.Work.Capture = Path.Combine(Captures, "capture-20261018-210900.jpg");

        var (window, crop) = PageHost.Open(() => new CodexCropPage(Key, surface.Pictures, surface.Nav, surface.Work, kept: false));
        var area = crop.Surface!;
        var corner = area.ToSurface(1500, 1000);

        Assert.True(area.Press(corner));
        area.DragTo(corner + new Point(-5000, -5000));
        Assert.Equal(CropSquare.Floor, area.Square.Side);
        area.Release();

        var inside = area.ToSurface(area.Square.X + 200, area.Square.Y + 200);
        Assert.True(area.Press(inside));
        area.DragTo(inside + new Point(-50000, 50000));
        area.Release();
        Assert.Equal(0, area.Square.X);
        Assert.Equal(1000 - CropSquare.Floor, area.Square.Y);

        window.Close();
    }
}
