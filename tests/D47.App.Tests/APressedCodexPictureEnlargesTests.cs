using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Pressing the picture on a Codex entry page shows it full size over the window; the family placeholder opens nothing.</summary>
[Trait("Category", "Integration")]
public sealed class APressedCodexPictureEnlargesTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Placeholders = @"C:\d47\codex-placeholders";

    private const string Entry = "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":101,\"Name\":\"$Codex_Ent_101;\","
        + "\"Name_Localised\":\"Entry 101\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\","
        + "\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"SubCategory_Localised\":\"Organic structures\","
        + "\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"IsNewEntry\":true}";

    private static byte[] Png(int side)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(side, side), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static (Window Window, CodexEntryPage Page) Opened(int? ownSide, int? placeholderSide, double width = 1400, double height = 1000)
    {
        var files = new MemoryFileSystem();

        if (ownSide is { } side)
        {
            files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, new CodexPictureKey(101, 18).FileName), Png(side));
        }

        if (placeholderSide is { } family)
        {
            files.WriteBytes(Path.Combine(Placeholders, "organic-structures.jpg"), Png(family));
        }

        var store = new CodexPictureStore(files, Folder, () => true, NullLogger.Instance, new HttpClientHandlerStub());
        var pictures = new CodexPictureSet(store, () => true, files, Placeholders);

        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));
        Assert.True(JournalEvent.TryParse(Entry, NullLogger.Instance, out var parsed));
        state.Apply(parsed!);

        var look = AppLook.Put();
        var page = new CodexEntryPage(() => state, 101, 18, "Entry 101", pictures);
        var window = new Window
        {
            Content = page,
            Width = width,
            Height = height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private sealed class HttpClientHandlerStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    private static IReadOnlyList<SpeakerPictureViewer> Viewers(Visual from) =>
        [.. OverlayLayer.GetOverlayLayer(from)!.Children.OfType<SpeakerPictureViewer>()];

    private static void Press(Window window, CodexEntryPage page, string name)
    {
        var picture = page.GetVisualDescendants().OfType<Image>().Single(image => image.Name == name);
        var at = picture.TranslatePoint(new Point(10, 10), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Image Viewed(Visual from) =>
        Viewers(from).Single().GetVisualDescendants().OfType<Image>().Single(image => image.Name == "ViewedPicture");

    [AvaloniaFact]
    public void PressingTheEntryPictureShowsItOverTheWindow()
    {
        var (window, page) = Opened(ownSide: 768, placeholderSide: null);
        var picture = page.GetVisualDescendants().OfType<Image>().Single(image => image.Name == "CodexPicture");

        Assert.NotNull(picture.Cursor);
        Assert.Empty(Viewers(page));

        Press(window, page, "CodexPicture");

        Assert.Single(Viewers(page));
        window.Close();
    }

    [AvaloniaFact]
    public void EscapeAndCloseBothPutTheEnlargedEntryPictureAway()
    {
        var (window, page) = Opened(768, null);
        Press(window, page, "CodexPicture");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Viewers(page));

        Press(window, page, "CodexPicture");

        var close = Viewers(page).Single().GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ClosePicture");
        Assert.True(close.MinHeight >= 44);

        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Viewers(page));
        window.Close();
    }

    [AvaloniaFact]
    public void ASevenHundredAndSixtyEightPixelPictureIsDrawnAtFullSizeInALargeWindowAndShrunkInASmallOne()
    {
        var (large, largePage) = Opened(768, null);
        Press(large, largePage, "CodexPicture");

        Assert.Equal(768, Viewed(largePage).Bounds.Width, 0.5);
        Assert.Equal(768, Viewed(largePage).Bounds.Height, 0.5);
        large.Close();

        var (small, smallPage) = Opened(768, null, 500, 500);
        Press(small, smallPage, "CodexPicture");

        Assert.True(Viewed(smallPage).Bounds.Width < 500);
        Assert.Equal(Viewed(smallPage).Bounds.Width, Viewed(smallPage).Bounds.Height, 0.5);
        small.Close();
    }

    [AvaloniaFact]
    public void TheFamilyPlaceholderOpensNothing()
    {
        var (window, page) = Opened(ownSide: null, placeholderSide: 64);

        Press(window, page, "CodexPlaceholder");

        Assert.Empty(Viewers(page));
        window.Close();
    }
}
