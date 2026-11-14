using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each Engineers table row shows the engineer's face; pressing it shows the whole picture full size.</summary>
[Trait("Category", "Integration")]
public sealed class AnEngineerPortraitEnlargesWhenPressedTests
{
    private static string Shipped => Path.Combine(AppSource.RepositoryRoot(), "assets", "engineers");

    private static IReadOnlyList<Image> Faces(Visual from) =>
        [.. from.GetVisualDescendants().OfType<Image>().Where(image => image.Name?.StartsWith(EngineerDirectoryPage.PortraitPrefix, StringComparison.Ordinal) == true)];

    private static IReadOnlyList<SpeakerPictureViewer> Viewers(Visual from) =>
        [.. OverlayLayer.GetOverlayLayer(from)!.Children.OfType<SpeakerPictureViewer>()];

    private static EngineersTabTests.Surface OpenWith(string? folder)
    {
        EngineerPictures.Folder = folder;

        return EngineersTabTests.Open();
    }

    [AvaloniaFact]
    public void EveryRowShowsItsEngineersFace()
    {
        var before = EngineerPictures.Folder;

        try
        {
            var surface = OpenWith(Shipped);

            Assert.Equal(EngineerDirectory.All.Count, Faces(surface.Panel).Count);
            Assert.All(Faces(surface.Panel), face => Assert.NotNull(face.Source));
            surface.Window.Close();
        }
        finally
        {
            EngineerPictures.Folder = before;
        }
    }

    [AvaloniaFact]
    public void PressingAFaceShowsThePictureFullSizeAndDoesNotOpenTheEngineer()
    {
        var before = EngineerPictures.Folder;

        try
        {
            var surface = OpenWith(Shipped);
            var trail = surface.Panel.Nav.Trail.Count;
            var face = Faces(surface.Panel)[0];
            var at = face.TranslatePoint(new Point(10, 10), surface.Window)!.Value;

            Assert.NotNull(face.Cursor);
            Assert.Empty(Viewers(surface.Panel));

            surface.Window.MouseDown(at, MouseButton.Left);
            surface.Window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(Viewers(surface.Panel));
            Assert.Equal(trail, surface.Panel.Nav.Trail.Count);

            surface.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(Viewers(surface.Panel));
            surface.Window.Close();
        }
        finally
        {
            EngineerPictures.Folder = before;
        }
    }

    [AvaloniaFact]
    public void AMissingPictureLeavesAnEmptySlabAndThrowsNothing()
    {
        var before = EngineerPictures.Folder;

        try
        {
            var surface = OpenWith(Path.Combine(Path.GetTempPath(), "d47-no-such-engineers"));

            Assert.Empty(Faces(surface.Panel));
            Assert.Equal(EngineerDirectory.All.Count, surface.Panel.GetVisualDescendants().OfType<Button>()
                .Count(button => button.Classes.Contains(D47.App.Theming.ListRow.Class)));
            surface.Window.Close();
        }
        finally
        {
            EngineerPictures.Folder = before;
        }
    }

    [AvaloniaFact]
    [Trait("Category", "Gate")]
    public void EveryEngineerShipsAWholePictureAndASquareFace()
    {
        foreach (var engineer in EngineerDirectory.All)
        {
            using var whole = new Bitmap(Path.Combine(Shipped, engineer.Id + ".jpg"));
            using var face = new Bitmap(Path.Combine(Shipped, engineer.Id + ".face.jpg"));

            Assert.True(Math.Max(whole.PixelSize.Width, whole.PixelSize.Height) <= 1024, engineer.Name);
            Assert.Equal(face.PixelSize.Width, face.PixelSize.Height);
        }
    }
}
