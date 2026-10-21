using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers › Codex › a region › Discoveries: the square thumbnail on each row (#1464).</summary>
[Trait("Category", "Integration")]
public class TheCodexDiscoveriesPageShowsEntryThumbnailsTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Placeholders = @"C:\d47\codex-placeholders";

    private const int Region = 18;

    private static readonly CodexTable Table = CodexTable.Parse(
        "1\tAlpha tree\tBiological and Geological\tOrganic structures\t18:50\n"
        + "2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n"
        + "3\tGamma tree\tBiological and Geological\tOrganic structures\t18:10\n"
        + "4\tDelta tree\tBiological and Geological\tOrganic structures\t18:5\n");

    private sealed class Release : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public TaskCompletionSource Hold { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Serve(CodexPictureRow row, byte[] bytes) =>
            _files[$"/{CodexPictureIndex.Repository}/releases/download/{row.Tag}/{row.Asset}"] = bytes;

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
        private readonly List<CodexPictureRow> _rows = [];

        public Surface() => Store = new CodexPictureStore(Files, Folder, () => StockOn, NullLogger.Instance, Release);

        public MemoryFileSystem Files { get; } = new();

        public Release Release { get; } = new();

        public bool StockOn { get; set; } = true;

        public CodexPictureStore Store { get; }

        public CodexPictureSet Pictures => new(Store, () => StockOn, Files, Placeholders);

        public void OwnPicture(long entry, int side) =>
            Files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, new CodexPictureKey(entry, Region).FileName), Png(side));

        public CodexPictureRow StockRow(long entry, bool onDisk, int side)
        {
            var bytes = Png(side);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var row = new CodexPictureRow(entry, Region, "codex-pictures-1", CodexPictureIndex.AssetName(entry, Region, sha), sha, bytes.Length);
            _rows.Add(row);
            Files.WriteText(Path.Combine(Folder, CodexPictureStore.StockFolder, CodexPictureIndex.IndexName), CodexPictureIndex.Write(_rows));

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

        public void Placeholder(int side) => Files.WriteBytes(Path.Combine(Placeholders, "organic-structures.jpg"), Png(side));
    }

    private static byte[] Png(int side)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(side, side), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static (Window Window, CodexDiscoveriesPage Page) Open(Surface surface, bool headset = false)
    {
        var look = AppLook.Put();
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));
        var page = new CodexDiscoveriesPage(() => state, () => HistoryState.Done, Region, Table, headset, pictures: surface.Pictures);
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

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static Button RowOf(Control page, long entry) =>
        In<Button>(page).First(button => button.Name == "CodexMissing." + entry);

    private static Image? Thumb(Control page, long entry) =>
        In<Image>(RowOf(page, entry)).FirstOrDefault(image => image.Name?.StartsWith("CodexThumb", StringComparison.Ordinal) == true);

    [AvaloniaFact]
    public void ARowShowsTheCommandersPictureThenStockThenTheFamilyPlaceholder()
    {
        var surface = new Surface();
        surface.OwnPicture(1, 10);
        surface.StockRow(1, onDisk: true, side: 20);
        surface.StockRow(2, onDisk: true, side: 30);
        surface.Placeholder(40);

        var (window, page) = Open(surface);

        Assert.Equal("CodexThumbLocal", Thumb(page, 1)!.Name);
        Assert.Equal("CodexThumbStock", Thumb(page, 2)!.Name);
        Assert.Equal("CodexThumbPlaceholder", Thumb(page, 4)!.Name);

        window.Close();
    }

    [AvaloniaFact]
    public void ThumbnailsAreFortyFourSquareAndTheRowStaysAtItsMinimumHeight()
    {
        var surface = new Surface();
        surface.OwnPicture(1, 10);
        surface.Placeholder(40);

        var (window, page) = Open(surface);

        foreach (var entry in new long[] { 1, 4 })
        {
            var thumb = Thumb(page, entry)!;
            Assert.Equal(TypeScale.MinimumTarget, thumb.Bounds.Width);
            Assert.Equal(TypeScale.MinimumTarget, thumb.Bounds.Height);
            Assert.Equal(TypeScale.MinimumTarget, RowOf(page, entry).Bounds.Height);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void WithStockPicturesOffNoStockThumbnailShows()
    {
        var surface = new Surface { StockOn = false };
        surface.StockRow(2, onDisk: true, side: 30);
        surface.Placeholder(40);

        var (window, page) = Open(surface);

        Assert.Equal("CodexThumbPlaceholder", Thumb(page, 2)!.Name);

        window.Close();
    }

    [AvaloniaFact]
    public void DrawingTheListStartsNoPictureFetch()
    {
        var surface = new Surface();
        var missing = surface.StockRow(3, onDisk: false, side: 30);
        surface.Placeholder(40);

        var (window, page) = Open(surface);

        Assert.Equal("CodexThumbPlaceholder", Thumb(page, 3)!.Name);
        Assert.True(surface.Store.Fetched(missing.Key).IsCompleted);

        window.Close();
    }

    [AvaloniaFact]
    public void ReadingAStockPictureOffDiskNeverStartsAFetchButReadingItDoes()
    {
        var surface = new Surface();
        var row = surface.StockRow(3, onDisk: false, side: 30);

        Assert.Null(surface.Store.StockOnDisk(row));
        Assert.True(surface.Store.Fetched(row.Key).IsCompleted);

        Assert.Null(surface.Store.Stock(row));
        Assert.False(surface.Store.Fetched(row.Key).IsCompleted);

        surface.Release.Hold.SetResult();
    }

    [AvaloniaFact]
    public void ThePictureColumnHasNoLabelAndDoesNotSort()
    {
        var heads = CodexModel.MissingHeads();

        Assert.Equal(string.Empty, heads[0].Label);
        Assert.False(heads[0].Sortable);
        Assert.Equal("Reported in", heads[CodexModel.MissingDefaultColumn].Label);

        var (window, page) = Open(new Surface());

        Assert.IsNotType<Button>(In<Control>(page).First(control => control.Name == "CodexMissingHead."));

        window.Close();
    }

    [AvaloniaFact]
    public void ComingBackShowsAStockPictureTheEntryPageFetched()
    {
        var surface = new Surface();
        surface.Release.Hold.SetResult();
        var row = surface.StockRow(3, onDisk: false, side: 30);
        surface.Placeholder(40);

        var (window, page) = Open(surface);

        Assert.Equal("CodexThumbPlaceholder", Thumb(page, 3)!.Name);

        Assert.Null(surface.Store.Stock(row));
        Assert.True(surface.Store.Fetched(row.Key).Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Content = page;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("CodexThumbStock", Thumb(page, 3)!.Name);

        window.Close();
    }
}
