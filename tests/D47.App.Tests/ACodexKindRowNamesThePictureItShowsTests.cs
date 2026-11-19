using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

/// <summary>The line under a kind's name on a catalogue page says which picture its square shows (#2608).</summary>
[Trait("Category", "Integration")]
public class ACodexKindRowNamesThePictureItShowsTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Placeholders = @"C:\d47\codex-placeholders";

    private const int Region = 18;

    private const string NoAtmosphere = "planet.rocky_body.not_terraformable.no_atmosphere";
    private const string Ammonia = "planet.rocky_body.not_terraformable.atmosphere.ammonia";
    private const string Argon = "planet.rocky_body.not_terraformable.atmosphere.argon";
    private const string Group = "planet.rocky_body.not_terraformable.atmosphere";
    private const string Terraformable = "planet.rocky_body.terraformable.no_atmosphere";

    private readonly MemoryFileSystem _files = new();
    private readonly List<CodexPictureRow> _rows = [];
    private readonly CodexPictureStore _store;

    public ACodexKindRowNamesThePictureItShowsTests() =>
        _store = new CodexPictureStore(_files, Folder, () => true, NullLogger.Instance, new HeldRelease(), commander: () => "F1");

    private void Own(string key) =>
        _files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, "F1", new CodexPictureKey(key, Region).FileName), Png());

    private void Stock(string key, bool onDisk = true)
    {
        var bytes = Png();
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var row = new CodexPictureRow(key, Region, CodexPictureIndex.FirstTag, CodexPictureIndex.AssetName(new CodexPictureKey(key, Region), sha), sha, bytes.Length);
        _rows.Add(row);
        _files.WriteText(Path.Combine(Folder, CodexPictureStore.StockFolder, CodexPictureIndex.IndexName), CodexPictureIndex.Write(_rows));

        if (onDisk)
        {
            _files.WriteBytes(Path.Combine(Folder, CodexPictureStore.StockFolder, row.Asset), bytes);
        }
    }

    private static byte[] Png()
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(20, 20), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    private (Window Window, CodexCataloguePage Page) Open()
    {
        var state = JournalScript.Commander("F1", "John Deparagon");
        var pictures = new CodexPictureSet(_store, () => true, _files, Placeholders);

        return PageHost.Open(() => new CodexCataloguePage(
            () => state, () => HistoryState.Done, new CodexCatalogueChoice(), CodexCatalogue.Tree.Node("planet.rocky_body")!, pictures: pictures));
    }

    private static string Line(Control page, string key) =>
        page.GetVisualDescendants().OfType<Button>().First(button => button.Name == "CodexKind." + key)
            .GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == "CodexPictureLine").Text ?? string.Empty;

    private static bool HasPicture(Control page, string key) =>
        page.GetVisualDescendants().OfType<Button>().First(button => button.Name == "CodexKind." + key)
            .GetVisualDescendants().OfType<Image>().Any(image => image.Name == "CodexThumb");

    private static void Choose(Control page, int segment)
    {
        page.GetVisualDescendants().OfType<Segment>().Single(item => item.Name == "CodexCatalogueSegment")
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(segment).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void EachRowReadsYourStockGroupOrFamilyPlaceholder()
    {
        Own(NoAtmosphere);
        Stock(Ammonia);
        Stock(Group);

        var (window, page) = Open();
        Choose(page, 1);

        Assert.Equal("YOUR PICTURE", Line(page, NoAtmosphere));
        Assert.Equal("STOCK PICTURE", Line(page, Ammonia));
        Assert.Equal("GROUP PICTURE", Line(page, Argon));
        Assert.True(HasPicture(page, Argon));

        Choose(page, 0);

        Assert.Equal("FAMILY PLACEHOLDER", Line(page, Terraformable));
        Assert.False(HasPicture(page, Terraformable));

        window.Close();
    }

    [AvaloniaFact]
    public void AStockPictureNotYetOnDiskReadsAsTheFamilyPlaceholder()
    {
        Stock(Ammonia, onDisk: false);

        var (window, page) = Open();
        Choose(page, 1);

        Assert.Equal("FAMILY PLACEHOLDER", Line(page, Ammonia));

        window.Close();
    }
}
