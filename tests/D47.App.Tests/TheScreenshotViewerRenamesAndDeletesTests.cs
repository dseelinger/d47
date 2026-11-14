using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>The viewer's Rename and Delete dialogs, over a screenshots folder held in memory.</summary>
[Trait("Category", "Integration")]
public sealed class TheScreenshotViewerRenamesAndDeletesTests
{
    private const string Folder = @"C:\Pictures\Elite Dangerous\D47";

    private const string Elite = @"C:\Pictures\Elite Dangerous";

    private const string Thumbs = @"C:\d47\data\screenshot-thumbnails";

    private static readonly string[] Names = ["2026-11-01 10-00-00 Sol.jpg", "2026-11-02 10-00-00 Colonia.jpg"];

    private static byte[] Jpeg()
    {
        using var bitmap = new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new JpegBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static PictureNote Noted(DateTimeOffset at, string? bmp, FileState? state = null) =>
        new(at, ScreenPictures.FromScreenshot, 8, 8, "Sol", null, null, null, [new PictureNoteEntry(101, "Alpha", 1)], bmp)
        {
            BmpLength = state?.Length,
            BmpWritten = state is { } s ? new DateTimeOffset(s.Written.ToUniversalTime()) : null,
        };

    private sealed record Rig(
        Window Window, ScreenshotsPage Page, Visual Viewer, MemoryFileSystem Files, List<string> Recycled, List<(string, string)> Renamed, List<CodexPictureKey> Removed);

    private static Rig Open(bool bmp, bool cut)
    {
        var files = new MemoryFileSystem();
        var jpeg = Jpeg();

        if (bmp)
        {
            files.WriteBytes(Path.Combine(Elite, "Screenshot_0001.bmp"), [1]);
        }

        var bmpState = files.Stat(Path.Combine(Elite, "Screenshot_0001.bmp"));

        for (var i = 0; i < Names.Length; i++)
        {
            files.WriteBytes(Path.Combine(Folder, Names[i]), jpeg);
            files.WriteText(
                Path.Combine(Folder, Path.ChangeExtension(Names[i], ".json")),
                Noted(new DateTimeOffset(2026, 11, 1 + i, 10, 0, 0, TimeSpan.Zero), i == 0 ? "Screenshot_0001.bmp" : null, i == 0 ? bmpState : null).Write());
        }

        var recycled = new List<string>();
        var renamed = new List<(string, string)>();
        var removed = new List<CodexPictureKey>();
        var links = new CodexLinks(
            name => cut && name == Names[0] ? [new CodexPictureKey(101, 1)] : [],
            (from, to) =>
            {
                renamed.Add((from, to));

                return 1;
            },
            removed.Add);
        var set = new ScreenshotSet(
            files,
            Folder,
            Thumbs,
            recycle: path =>
            {
                recycled.Add(path);
                files.Delete(path);

                return true;
            },
            bmpFolder: Elite,
            codex: links);

        var page = new ScreenshotsPage(set, () => null);
        var window = new Window { Content = page, Width = 1280, Height = 800 };
        window.Show();
        Until(() => Buttons(page).Any(b => b.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true), "the page did not draw");

        Press(Buttons(page).Single(b => b.Name == "Screenshot." + Path.GetFileNameWithoutExtension(Names[0])));

        var viewer = OverlayLayer.GetOverlayLayer(page)!.Children.OfType<Grid>().Single(g => g.GetType().Name == "ScreenshotViewer");

        return new Rig(window, page, viewer, files, recycled, renamed, removed);
    }

    private static List<Button> Buttons(Visual root) => [.. root.GetVisualDescendants().OfType<Button>()];

    private static T Find<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Press(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Until(Func<bool> done, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(20);

        while (!done() && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done(), what);
    }

    private static string Position(Visual viewer) => Find<TextBlock>(viewer, "ScreenshotPosition").Text!;

    [AvaloniaFact]
    public void RenamingFromTheViewerRenamesTheFilesAndTheCodexRecordAndKeepsTheOrder()
    {
        var rig = Open(bmp: false, cut: true);

        Press(Find<Button>(rig.Viewer, "RenameScreenshot"));
        var name = Find<TextBox>(rig.Viewer, RenameScreenshotDialog.NameFieldName);
        name.Text = "Sol at dawn";
        Press(Find<Button>(rig.Viewer, RenameScreenshotDialog.ConfirmName));

        Until(() => rig.Files.Stat(Path.Combine(Folder, "Sol at dawn.jpg")) is not null, "the file was not renamed");
        Until(() => !rig.Viewer.GetVisualDescendants().OfType<TextBox>().Any(), "the dialog did not close");

        Assert.Null(rig.Files.Stat(Path.Combine(Folder, Names[0])));
        Assert.NotNull(rig.Files.Stat(Path.Combine(Folder, "Sol at dawn.json")));
        Assert.Equal([(Names[0], "Sol at dawn.jpg")], rig.Renamed);
        Assert.Equal("2 of 2", Position(rig.Viewer));
    }

    [AvaloniaFact]
    public void ATakenOrEmptyNameShowsTheFieldErrorAndRenamesNothing()
    {
        var rig = Open(bmp: false, cut: false);

        Press(Find<Button>(rig.Viewer, "RenameScreenshot"));
        var name = Find<TextBox>(rig.Viewer, RenameScreenshotDialog.NameFieldName);

        foreach (var typed in new[] { "", "2026-11-02 10-00-00 Colonia" })
        {
            name.Text = typed;
            Press(Find<Button>(rig.Viewer, RenameScreenshotDialog.ConfirmName));
            Until(() => !string.IsNullOrEmpty(D47.App.Theming.FieldMessage.GetText(name)), "no field error");
        }

        Assert.NotNull(rig.Files.Stat(Path.Combine(Folder, Names[0])));
        Assert.Empty(rig.Renamed);
    }

    [AvaloniaFact]
    public void TheBmpBoxIsShownOnlyWhileEliteStillHasTheBmp()
    {
        var without = Open(bmp: false, cut: false);
        Press(Find<Button>(without.Viewer, "DeleteScreenshot"));
        Until(() => without.Viewer.GetVisualDescendants().OfType<Button>().Any(b => b.Name == DeleteScreenshotDialog.ConfirmName), "no dialog");

        Assert.DoesNotContain(without.Viewer.GetVisualDescendants().OfType<CheckBox>(), c => c.Name == DeleteScreenshotDialog.BmpBoxName);

        var with = Open(bmp: true, cut: false);
        Press(Find<Button>(with.Viewer, "DeleteScreenshot"));
        Until(() => with.Viewer.GetVisualDescendants().OfType<Button>().Any(b => b.Name == DeleteScreenshotDialog.ConfirmName), "no dialog");

        Assert.False(Find<CheckBox>(with.Viewer, DeleteScreenshotDialog.BmpBoxName).IsChecked);
    }

    [AvaloniaFact]
    public void DeletingRecyclesTheJpegAndTheTickedBmpRemovesTheCodexPictureAndClosesAfterTheLast()
    {
        var rig = Open(bmp: true, cut: true);

        Press(Find<Button>(rig.Viewer, "DeleteScreenshot"));
        Until(() => rig.Viewer.GetVisualDescendants().OfType<Button>().Any(b => b.Name == DeleteScreenshotDialog.ConfirmName), "no dialog");

        Assert.Contains("Alpha", Find<TextBlock>(rig.Viewer, DeleteScreenshotDialog.CodexLineName).Text, StringComparison.Ordinal);

        Find<CheckBox>(rig.Viewer, DeleteScreenshotDialog.BmpBoxName).IsChecked = true;
        Press(Find<Button>(rig.Viewer, DeleteScreenshotDialog.ConfirmName));

        Until(() => rig.Removed.Count == 1, "the Codex picture was not removed");
        Until(() => !OverlayLayer.GetOverlayLayer(rig.Page)!.Children.Contains((Control)rig.Viewer), "the viewer did not close after the last picture");

        Assert.Equal([Path.Combine(Elite, "Screenshot_0001.bmp"), Path.Combine(Folder, Names[0])], rig.Recycled);
        Assert.Null(rig.Files.Stat(Path.Combine(Folder, Path.ChangeExtension(Names[0], ".json"))));
        Assert.Equal(new CodexPictureKey(101, 1), rig.Removed[0]);
    }

    [Fact]
    public void TheCodexLineNamesThreeEntriesAndCountsTheRest()
    {
        Assert.Contains("A, B, C and 2 more", DeleteScreenshotDialog.CodexLine(["A", "B", "C", "D", "E"]), StringComparison.Ordinal);
        Assert.Contains("A, B, C.", DeleteScreenshotDialog.CodexLine(["A", "B", "C"]), StringComparison.Ordinal);
    }
}
