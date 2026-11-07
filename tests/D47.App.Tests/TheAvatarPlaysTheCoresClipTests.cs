using Xunit;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using D47.App.Panel;
using D47.Core.Storage;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Interface;
using Shape = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class TheAvatarPlaysTheCoresClipTests : IDisposable
{
    private readonly HeadsetAvatarRig _rig = new("d47-clips-");

    public void Dispose() => _rig.Dispose();

    private string Place(string core, LoopState state, byte[]? bytes = null)
    {
        var path = Path.Combine(_rig.Paths.AvatarClips, CoreClips.FileName(core, state));
        File.WriteAllBytes(path, bytes ?? File.ReadAllBytes(HeadsetAvatarRig.Fixture()));

        return path;
    }

    private AvatarView Avatar(string core, LoopState state, bool still = false)
    {
        var view = new AvatarView { Library = AvatarLibrary.Load(new DiskFileSystem(), _rig.Paths), Still = still };

        view.Show(state);
        view.Core = core;

        return view;
    }

    private static Image Picture(AvatarView view) => view.GetLogicalDescendants().OfType<Image>().Single();

    private static Shape Mark(AvatarView view) => view.GetLogicalDescendants().OfType<Shape>().Single();

    /// <summary>Whether the drawn picture changes while the UI thread runs for up to a second.</summary>
    private static bool Moves(AvatarView view)
    {
        var before = Pixels(Picture(view));

        return UiWait.Running(() => !Pixels(Picture(view)).SequenceEqual(before), seconds: 1);
    }

    private static byte[] Pixels(Image picture)
    {
        var bitmap = Assert.IsType<WriteableBitmap>(picture.Source);

        using var frame = bitmap.Lock();

        var pixels = new byte[frame.RowBytes * frame.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(frame.Address, pixels, 0, pixels.Length);

        return pixels;
    }

    [AvaloniaFact]
    public void AMissingClipDrawsTheMark()
    {
        var view = Avatar("covas", LoopState.Thinking);

        Assert.True(Mark(view).IsVisible);
        Assert.False(Picture(view).IsVisible);
    }

    [AvaloniaFact]
    public void AZeroByteClipDrawsTheMark()
    {
        Place("covas", LoopState.Thinking, []);

        var view = Avatar("covas", LoopState.Thinking);

        Assert.True(Mark(view).IsVisible);
        Assert.False(Picture(view).IsVisible);
    }

    [AvaloniaFact]
    public void ATruncatedClipDrawsTheMark()
    {
        Place("covas", LoopState.Thinking, [.. File.ReadAllBytes(HeadsetAvatarRig.Fixture()).Take(100)]);

        var view = Avatar("covas", LoopState.Thinking);

        Assert.True(Mark(view).IsVisible);
        Assert.False(Picture(view).IsVisible);
    }

    [AvaloniaFact]
    public void APresentClipShowsTheImage()
    {
        Place("covas", LoopState.Thinking);

        var view = Avatar("covas", LoopState.Thinking);

        Assert.True(Picture(view).IsVisible);
        Assert.NotNull(Picture(view).Source);
        Assert.False(Mark(view).IsVisible);
        Assert.True(Moves(view));
    }

    [AvaloniaFact]
    public void AStateMissingFromACoreWithOtherClipsDrawsTheMark()
    {
        Place("covas", LoopState.Thinking);

        var view = Avatar("covas", LoopState.Speaking);

        Assert.True(Mark(view).IsVisible);
        Assert.False(Picture(view).IsVisible);
    }

    [AvaloniaFact]
    public void AStillClipStartsNoTimer()
    {
        Place("covas", LoopState.Thinking);

        var view = Avatar("covas", LoopState.Thinking, still: true);

        Assert.NotNull(Picture(view).Source);
        Assert.False(Moves(view));
    }

    [AvaloniaFact]
    public void ALeftBehindClipStopsItsTimer()
    {
        Place("covas", LoopState.Thinking);

        var window = new Window { Content = Avatar("covas", LoopState.Thinking) };
        window.Show();

        var view = (AvatarView)window.Content!;
        Assert.True(Moves(view));

        window.Content = null;

        Assert.Null(view.ShowingClip);
        Assert.Null(Picture(view).Source);

        window.Close();
    }

    [AvaloniaFact]
    public void ClipsOpenedOneAfterAnotherAllPlay()
    {
        var path = Place("covas", LoopState.Thinking);

        // The basic video processor crashed the process within a few of these.
        for (var i = 0; i < 40; i++)
        {
            using var video = D47.App.Media.VideoFrames.Open(path)!;
            var frame = video.Frame();
            var frames = 0;

            while (video.Next(frame))
            {
                frames++;
            }

            Assert.True(frames > 1);
        }
    }

    [AvaloniaFact]
    public void TheAvatarLoopsAClipPastItsEndWithoutDrawingTheMark()
    {
        Place("covas", LoopState.Thinking);

        var view = Avatar("covas", LoopState.Thinking);
        var tick = typeof(AvatarView).GetMethod(
            "AdvanceClip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        for (var i = 0; i < 40; i++)
        {
            tick.Invoke(view, null);
        }

        Assert.True(Picture(view).IsVisible);
        Assert.False(Mark(view).IsVisible);
        Assert.True(Moves(view));
    }
}
