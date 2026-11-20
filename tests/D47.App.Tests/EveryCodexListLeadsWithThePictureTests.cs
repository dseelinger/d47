using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The region entries page and the Possible firsts page draw the entry's picture, or its family placeholder, in the leftmost column.</summary>
[Trait("Category", "Integration")]
public class EveryCodexListLeadsWithThePictureTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Placeholders = @"C:\d47\codex-placeholders";

    private const int Region = 18;

    private const string Organic = "$Codex_SubCategory_Organic_Structures;";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse("# Table date: 2026-10-19\n101\tEntry 101\tBiology\tOrganic\t4:12\n");

    private readonly MemoryFileSystem _files = new();

    private static string Codex(string time, long id) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"" + Organic + "\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_18;\""
        + ",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"Latitude\":12.4512,\"Longitude\":-108.2231,\"IsNewEntry\":true}";

    private static CommanderGameState State() =>
        JournalScript.Commander("F1", "Mara Quell",
        [
            "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
            Codex("2026-10-02T10:00:00Z", 101),
            Codex("2026-10-04T10:00:00Z", 103),
        ]);

    private CodexPictureSet Pictures() =>
        new(new CodexPictureStore(_files, Folder, () => true, NullLogger.Instance, new HeldRelease(), commander: () => "F1"), () => true, _files, Placeholders);

    private void Own(string key) =>
        _files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, "F1", new CodexPictureKey(key, Region).FileName), Png());

    private void FamilyPlaceholder() =>
        _files.WriteBytes(Path.Combine(Placeholders, CodexFamilies.FileOfSymbol(Organic)!), Png());

    private static byte[] Png()
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(20, 20), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static Control Row(Control page, string name) =>
        page.GetVisualDescendants().OfType<Control>().First(control => control.Name == name);

    private static Image? Thumb(Control row) =>
        row.GetVisualDescendants().OfType<Image>().FirstOrDefault(image => image.Name == "CodexThumb");

    private static void AssertLeftOfName(Control row, Image thumb, string name)
    {
        var text = row.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == name);

        Assert.True(thumb.TranslatePoint(default, row)!.Value.X < text.TranslatePoint(default, row)!.Value.X);
    }

    [AvaloniaFact]
    public void ARegionRowShowsItsPictureElseTheFamilyPlaceholder()
    {
        Own("101");
        var state = State();
        var choice = new CodexChoice { Period = (int)CodexPeriod.Everything };

        var (window, page) = PageHost.Open(() => new CodexRegionPage(() => state, () => HistoryState.Done, () => Now, choice, Region, pictures: Pictures()));

        var own = Row(page, "CodexEntry.101");
        Assert.NotNull(Thumb(own));
        AssertLeftOfName(own, Thumb(own)!, "Entry 101");
        Assert.Null(Thumb(Row(page, "CodexEntry.103")));
        window.Close();

        FamilyPlaceholder();
        var (again, redrawn) = PageHost.Open(() => new CodexRegionPage(() => state, () => HistoryState.Done, () => Now, choice, Region, pictures: Pictures()));

        Assert.NotNull(Thumb(Row(redrawn, "CodexEntry.103")));
        again.Close();
    }

    [AvaloniaFact]
    public void APossibleFirstRowShowsTheEntrysPicture()
    {
        Own("101");
        var state = State();
        var book = new RegionFirstBook("region-firsts.json", new MemoryFileSystem(), NullLogger.Instance, work => work());
        book.Observe("F1", CodexLedger.None.With(state.CodexLedger), Table);

        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => state, book, pictures: Pictures()));

        var row = Row(page, "CodexPossibleFirst.101.18");
        Assert.NotNull(Thumb(row));
        AssertLeftOfName(row, Thumb(row)!, "Entry 101");
        window.Close();
    }
}
