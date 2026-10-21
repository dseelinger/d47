using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

/// <summary>Commander › Ledgers › Codex › a region › an entry: the picture, where it came from, the facts and the loggings (#1369).</summary>
[Trait("Category", "Integration")]
public class TheCodexEntryPageShowsOneEntryTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Placeholders = @"C:\d47\codex-placeholders";

    private const string Organic = "$Codex_SubCategory_Organic_Structures;";

    private const int Local = 8;

    private const int Stock = 16;

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static string Codex(string time, long id, int region, string symbol, string sub, string system, bool isNew) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"" + symbol + "\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"$Codex_RegionName_" + region
        + ";\",\"System\":\"" + system + "\",\"SystemAddress\":1,\"BodyID\":5,\"Latitude\":12.4512,\"Longitude\":-108.2231"
        + (isNew ? ",\"IsNewEntry\":true" : string.Empty) + "}";

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        Codex("2026-10-02T10:00:00Z", 101, 18, Organic, "Organic structures", "Sol", true),
        Codex("2026-10-04T10:00:00Z", 102, 18, "$Codex_SubCategory_Stars;", "Stars", "Alpha", true),
        Codex("2026-10-05T10:00:00Z", 101, 18, Organic, "Organic structures", "Beta", false),
    ];

    private sealed class Release : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public void Serve(CodexPictureRow row, byte[] bytes) =>
            _files[$"/{CodexPictureIndex.Repository}/releases/download/{row.Tag}/{row.Asset}"] = bytes;

        /// <summary>Holds every picture response until it is set.</summary>
        public TaskCompletionSource Hold { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith(CodexPictureIndex.IndexName, StringComparison.Ordinal))
            {
                await Hold.Task.ConfigureAwait(false);
            }

            return _files.TryGetValue(request.RequestUri.AbsolutePath, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class Surface
    {
        public MemoryFileSystem Files { get; } = new();

        public Release Release { get; } = new();

        public bool StockOn { get; set; } = true;

        public CodexPictureStore Store { get; }

        public CodexPictureSet Pictures { get; }

        public Surface(bool hold = false)
        {
            if (!hold)
            {
                Release.Hold.SetResult();
            }


            Store = new CodexPictureStore(Files, Folder, () => StockOn, NullLogger.Instance, Release);
            Pictures = new CodexPictureSet(Store, () => StockOn, Files, Placeholders);
        }

        public void OwnPicture(long entry, int region, int side = Local) =>
            Files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, new CodexPictureKey(entry, region).FileName), Png(side));

        public CodexPictureRow StockRow(long entry, int region, bool onDisk, int side = Stock)
        {
            var bytes = Png(side);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var row = new CodexPictureRow(entry, region, "codex-pictures-1", CodexPictureIndex.AssetName(entry, region, sha), sha, bytes.Length);
            Files.WriteText(Path.Combine(Folder, CodexPictureStore.StockFolder, CodexPictureIndex.IndexName), CodexPictureIndex.Write(Rows.Append(row)));
            Rows.Add(row);

            if (onDisk)
            {
                Files.WriteBytes(Path.Combine(Folder, CodexPictureStore.StockFolder, row.Asset), bytes);
            }
            else
            {
                Release.Serve(row, bytes);
            }

            return row;
        }

        public void Placeholder(string file, int side) => Files.WriteBytes(Path.Combine(Placeholders, file), Png(side));

        private List<CodexPictureRow> Rows { get; } = [];
    }

    private static byte[] Png(int side)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(side, side), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static CommanderGameState State(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static (Window Window, T Page) Open<T>(Func<T> make)
        where T : Control
    {
        var look = AppLook.Put();
        var page = make();
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static CodexEntryPage Entry(Surface surface, long entryId = 101, int? region = 18, params string[] lines) =>
        new(() => State(lines.Length == 0 ? Lines : lines), entryId, region, "Entry " + entryId, surface.Pictures);

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) =>
        In<TextBlock>(root).Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    private static int? Side(Control root, string name) =>
        In<Image>(root).FirstOrDefault(image => image.Name == name)?.Source is Bitmap bitmap ? bitmap.PixelSize.Width : null;

    [AvaloniaFact]
    public void TheCommandersOwnPictureIsDrawnSquareAndSaysSo()
    {
        var surface = new Surface();
        surface.OwnPicture(101, 18);
        surface.StockRow(101, 18, onDisk: true);

        var (window, page) = Open(() => Entry(surface));

        Assert.Equal(Local, Side(page, "CodexPicture"));
        Assert.True(Shows(page, "Your picture"));
        Assert.False(Shows(page, CodexModel.NoPictureText));

        var picture = In<Image>(page).First(image => image.Name == "CodexPicture");
        Assert.Equal(picture.Width, picture.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void AStockPictureForThisRegionSaysStockPicture()
    {
        var surface = new Surface();
        surface.StockRow(101, 18, onDisk: true);

        var (window, page) = Open(() => Entry(surface));

        Assert.Equal(Stock, Side(page, "CodexPicture"));
        Assert.True(Shows(page, "Stock picture"));

        window.Close();
    }

    [AvaloniaFact]
    public void APictureFromAnotherRegionNamesThatRegion()
    {
        var own = new Surface();
        own.OwnPicture(101, 35);

        var (first, ownPage) = Open(() => Entry(own));

        Assert.Equal(Local, Side(ownPage, "CodexPicture"));
        Assert.True(Shows(ownPage, "Your picture · from " + CodexModel.RegionName(35)));
        first.Close();

        var stock = new Surface();
        stock.StockRow(101, 35, onDisk: true);

        var (second, stockPage) = Open(() => Entry(stock));

        Assert.Equal(Stock, Side(stockPage, "CodexPicture"));
        Assert.True(Shows(stockPage, "Stock picture · from " + CodexModel.RegionName(35)));
        second.Close();
    }

    [AvaloniaFact]
    public void WithNoPictureAnywhereThePageSaysSoAndShowsTheFamilyPlaceholder()
    {
        var surface = new Surface();
        surface.Placeholder("organic-structures.jpg", 24);

        var (window, page) = Open(() => Entry(surface));

        Assert.Null(Side(page, "CodexPicture"));
        Assert.Equal(24, Side(page, "CodexPlaceholder"));
        Assert.True(Shows(page, CodexModel.NoPictureText));

        window.Close();
    }

    [AvaloniaFact]
    public void TheShippedOrganicStructuresPlaceholderDecodesAndFillsTheSquare()
    {
        var root = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(root, "d47.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("d47.slnx not found");
        }

        var surface = new Surface();
        surface.Files.WriteBytes(
            Path.Combine(Placeholders, "organic-structures.jpg"),
            File.ReadAllBytes(Path.Combine(root, "assets", "codex-placeholders", "organic-structures.jpg")));

        var (window, page) = Open(() => Entry(surface));

        Assert.Equal(768, Side(page, "CodexPlaceholder"));
        Assert.True(Shows(page, CodexModel.NoPictureText));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-entry-placeholder.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void WithStockPicturesOffThePageSaysSoAndStillShowsThePlaceholder()
    {
        var surface = new Surface { StockOn = false };
        surface.Placeholder("organic-structures.jpg", 24);

        var (window, page) = Open(() => Entry(surface));

        Assert.Equal(24, Side(page, "CodexPlaceholder"));
        Assert.True(Shows(page, CodexModel.StockOffText));
        Assert.False(Shows(page, CodexModel.NoPictureText));

        window.Close();
    }

    [AvaloniaFact]
    public void ASubcategoryOutsideTheSevenShowsNoPlaceholder()
    {
        var surface = new Surface();

        foreach (var file in new[] { "stars.jpg", "organic-structures.jpg", "guardian-objects.jpg" })
        {
            surface.Placeholder(file, 24);
        }

        var (window, page) = Open(() => Entry(
            surface, 301, 18, "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"LoadGame\"}",
            Codex("2026-10-02T10:00:00Z", 301, 18, "$Codex_SubCategory_X;", "Something else", "Sol", true)));

        Assert.Null(Side(page, "CodexPlaceholder"));
        Assert.True(Shows(page, CodexModel.NoPictureText));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("$Codex_SubCategory_Stars;", "stars.jpg")]
    [InlineData("$Codex_SubCategory_Gas_Giants;", "gas-giant-planets.jpg")]
    [InlineData("$Codex_SubCategory_Terrestrials;", "terrestrial-planets.jpg")]
    [InlineData("$Codex_SubCategory_Geology_and_Anomalies;", "geology-and-anomalies.jpg")]
    [InlineData("$Codex_SubCategory_Organic_Structures;", "organic-structures.jpg")]
    [InlineData("$Codex_SubCategory_Guardian;", "guardian-objects.jpg")]
    public void EachFamilyShowsItsOwnPlaceholderWhateverLanguageNamesIt(string symbol, string file)
    {
        var surface = new Surface();
        var files = new[]
        {
            "stars.jpg", "gas-giant-planets.jpg", "terrestrial-planets.jpg", "geology-and-anomalies.jpg", "organic-structures.jpg",
            "guardian-objects.jpg",
        };

        for (var i = 0; i < files.Length; i++)
        {
            surface.Placeholder(files[i], 20 + i);
        }

        var (window, page) = Open(() => Entry(
            surface, 301, 18, Codex("2026-10-02T10:00:00Z", 301, 18, symbol, "Strukturen", "Sol", true)));

        Assert.Equal(20 + Array.IndexOf(files, file), Side(page, "CodexPlaceholder"));

        window.Close();
    }

    [AvaloniaFact]
    public void AStockPictureBeingFetchedSaysSoThenDrawsWhenItArrives()
    {
        var surface = new Surface(hold: true);
        var row = surface.StockRow(101, 18, onDisk: false);

        var (window, page) = Open(() => Entry(surface));

        Assert.Null(Side(page, "CodexPicture"));
        Assert.True(Shows(page, CodexModel.FetchingText));

        surface.Release.Hold.SetResult();
        Assert.True(surface.Store.Fetched(row.Key).Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Stock, Side(page, "CodexPicture"));
        Assert.False(Shows(page, CodexModel.FetchingText));
        Assert.True(Shows(page, "Stock picture"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheFactsAndTheLoggingsInThisRegionAreListedNewestFirst()
    {
        var surface = new Surface();
        var (window, page) = Open(() => Entry(surface));

        Assert.True(Shows(page, "Biological and Geological"));
        Assert.True(Shows(page, "Organic structures"));
        Assert.True(Shows(page, CodexModel.LoggedHereHead));
        Assert.True(Shows(page, "2"));

        var times = In<TextBlock>(page).Select(block => block.Text ?? string.Empty).Where(text => text.StartsWith("2026-", StringComparison.Ordinal)).ToList();

        Assert.Equal(["2026-10-05 10:00", "2026-10-02 10:00"], times);
        Assert.Single(In<TextBlock>(page), block => block.Text == "NEW");
        Assert.Contains("Beta", In<TextBlock>(page).Select(block => block.Text));
        Assert.Contains("Sol", In<TextBlock>(page).Select(block => block.Text));

        window.Close();
    }

    [AvaloniaFact]
    public void AnEntryNotLoggedInThisRegionSaysSo()
    {
        var surface = new Surface();
        var (window, page) = Open(() => Entry(surface, 101, 35));

        Assert.True(Shows(page, CodexModel.NotHereText));
        Assert.DoesNotContain(In<TextBlock>(page), block => block.Text?.StartsWith("2026-", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAnEntryRowOnTheRegionPageDrillsToItsPage()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);
        nav.Drill(CodexPage.RegionCrumb(18));

        var state = State(Lines);
        var (window, page) = Open(() => new CodexRegionPage(
            () => state, () => HistoryState.Done, () => Now, new CodexChoice { Period = (int)CodexPeriod.Everything }, 18, false, nav));

        In<Button>(page).First(button => button.Name == "CodexEntry.101").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("codex-entry-18-101", nav.Trail[^1].Key);
        Assert.Equal("Entry 101", nav.Trail[^1].Word);
        Assert.True(CodexPage.IsEntry(nav.Trail[^1].Key));
        Assert.False(CodexPage.IsRegion(nav.Trail[^1].Key));
        Assert.Equal((18, 101L), CodexPage.EntryOf(nav.Trail[^1].Key));
        Assert.Equal((null, 7L), CodexPage.EntryOf(CodexPage.EntryCrumb(null, 7, "x").Key));
        Assert.Equal(["Inner Orion Spur", "Entry 101"], nav.Trail.Select(crumb => crumb.Word).TakeLast(2));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAMissingRowOnTheDiscoveriesPageDrillsToItsPage()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);
        nav.Drill(CodexPage.RegionCrumb(18));
        nav.Drill(CodexPage.DiscoveriesCrumb(18));

        var state = State(Lines);
        var table = D47.Core.Knowledge.CodexTable.Parse(new StringReader(
            "# test\n101\tEntry 101\tBiological and Geological\tOrganic structures\t18:3\n205\tAlpha tree\tBiological and Geological\tOrganic structures\t18:9\n"));
        var (window, page) = Open(() => new CodexDiscoveriesPage(() => state, () => HistoryState.Done, 18, table, nav: nav));

        In<Button>(page).First(button => button.Name == "CodexMissing.205").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("codex-entry-18-205", nav.Trail[^1].Key);
        Assert.Equal("Alpha tree", nav.Trail[^1].Word);

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelOpensTheEntryUnderItsRegion()
    {
        using var look = AppLook.Put();

        var surface = new Surface();
        surface.OwnPicture(101, 18);

        var root = TestSurface.MemoryFolder("d47-codex-entry-tests");
        var checklists = new D47.Core.Checklists.ChecklistService(
            new D47.Core.Checklists.ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<D47.Core.Checklists.ChecklistStore>.Instance),
            new D47.Core.Checklists.ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<D47.Core.Checklists.ChecklistProposalStore>.Instance),
            () => null);

        var state = State(Lines);
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices
        {
            Checklists = checklists,
            GameState = () => state,
            HistoryState = () => HistoryState.Done,
            Commanders = new CommanderRoster(() => null, () => 0, () => null, () => false, _ => { }),
            CodexPictures = surface.Pictures,
        }.Furnish(view);

        Assert.True(view.Nav.Show(CodexPage.PageKey));
        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        In<Button>(view).First(button => button.Name == "CodexRegion.18").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        In<Button>(view).First(button => button.Name == "CodexEntry.101").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex", "Inner Orion Spur", "Entry 101"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CodexEntryPage>(view));
        Assert.Empty(In<CodexRegionPage>(view));
        Assert.Equal(Local, Side(view, "CodexPicture"));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-entry-big-panel.png");
        }

        window.Close();
    }
}
