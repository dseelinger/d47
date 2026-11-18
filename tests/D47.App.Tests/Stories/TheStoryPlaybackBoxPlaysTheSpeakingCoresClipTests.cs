using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Voice;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>A line a ship's core spoke shows that core's speaking clip in the playback box, moving while the line is heard.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryPlaybackBoxPlaysTheSpeakingCoresClipTests : IDisposable
{
    private static readonly Story Run = new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = "-",
        PickedAt = new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero),
        State = StoryState.Running,
        Chapters = ["ch1"],
    };

    private readonly HeadsetAvatarRig _rig = new("d47-playback-clips-");

    public void Dispose() => _rig.Dispose();

    private static StoryRunLine Line(int number, string from, string? picture = null) => new()
    {
        Sent = Run.PickedAt.AddMinutes(number),
        From = from,
        Picture = picture,
        Key = $"line {number}",
        Body = $"Line number {number} of the run.",
        Clip = $"line-{number}.opus",
    };

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private string Place(string core)
    {
        var path = Path.Combine(_rig.Paths.AvatarClips, CoreClips.FileName(core, LoopState.Speaking));
        File.Copy(HeadsetAvatarRig.Fixture(), path, overwrite: true);

        return path;
    }

    private (Window Window, StoryRunPlayer Player, StoryPlaybackDialog Box) Open(bool stillClips, params StoryRunLine[] lines)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var player = new StoryRunPlayer(
            new AudioArbiter(new SteppedSink(), NullLogger<AudioArbiter>.Instance).Start(),
            (_, line) => new AudioClip(line.Body, new byte[960], AudioFormat.Standard),
            work => work());
        var window = new Window { Content = new Border(), Width = 1600, Height = 1000 };
        window.Show();

        player.Play(Run, lines, 0);

        var box = new StoryPlaybackDialog(
            player,
            () => new TrackProgress(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(11)),
            _ => "Chapter 1 · The Beacon Job",
            null,
            avatars: AvatarLibrary.Load(new DiskFileSystem(), _rig.Paths),
            stillClips: stillClips);
        _ = box.Over(window);
        Jobs();

        return (window, player, box);
    }

    private static AvatarView Core(StoryPlaybackDialog box) => box.GetVisualDescendants().OfType<AvatarView>().Single();

    private static Viewbox CoreBox(StoryPlaybackDialog box) =>
        box.GetVisualDescendants().OfType<Viewbox>().Single(found => found.Name == StoryPlaybackDialog.CoreName);

    private static Image Picture(StoryPlaybackDialog box) =>
        box.GetVisualDescendants().OfType<Image>().Single(found => found.Name == StoryPlaybackDialog.PictureName);

    private static byte[] Pixels(AvatarView view)
    {
        var bitmap = Assert.IsType<WriteableBitmap>(view.Picture.Source);

        using var frame = bitmap.Lock();

        var pixels = new byte[frame.RowBytes * frame.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(frame.Address, pixels, 0, pixels.Length);

        return pixels;
    }

    /// <summary>Whether the drawn clip changes while the UI thread runs for up to a second.</summary>
    private static bool Moves(AvatarView view)
    {
        var before = Pixels(view);

        return UiWait.Running(() => !Pixels(view).SequenceEqual(before), seconds: 1);
    }

    [AvaloniaFact]
    public void ACoresLineShowsItsSpeakingClipMovingAcrossThePictureArea()
    {
        var clip = Place("kex");
        var (window, _, box) = Open(stillClips: false, Line(1, "kex"));

        Assert.True(CoreBox(box).IsVisible);
        Assert.False(Picture(box).IsVisible);
        Assert.Equal(clip, Core(box).ShowingClip);
        Assert.True(Moves(Core(box)));

        var stage = (Control)CoreBox(box).GetVisualParent()!;
        var picture = Core(box).Picture;
        var across = new Rect(picture.Bounds.Size).TransformToAABB(picture.TransformToVisual(stage)!.Value);
        var fits = Math.Min(stage.Bounds.Width, stage.Bounds.Height);

        Assert.InRange(Math.Min(across.Width, across.Height), fits - 2, fits + 2);

        window.SaveFrame("story-playback-box-core-speaking.png");
        window.Close();
    }

    [AvaloniaFact]
    public void ALineSomeoneElseSpokeShowsNoCoreClip()
    {
        Place("kex");
        var (window, _, box) = Open(stillClips: false, Line(1, D47.Core.Messages.MessageStore.Narrator), Line(2, "kex"));

        Assert.False(CoreBox(box).IsVisible);
        Assert.True(Picture(box).IsVisible);
        Assert.Null(Core(box).ShowingClip);

        window.Close();
    }

    [AvaloniaFact]
    public void TheNextCoresLineChangesTheClip()
    {
        Place("kex");
        var second = Place("cartographer");
        var (window, player, box) = Open(stillClips: false, Line(1, "kex"), Line(2, "cartographer"));

        player.Skip();
        Jobs();

        Assert.Equal(second, Core(box).ShowingClip);
        Assert.True(Moves(Core(box)));

        window.Close();
    }

    [AvaloniaFact]
    public void PausingHoldsTheClipAndPlayMovesItAgain()
    {
        Place("kex");
        var (window, player, box) = Open(stillClips: false, Line(1, "kex"), Line(2, "kex"));

        player.Pause(StoryRunPause.Commander);
        Jobs();

        Assert.True(CoreBox(box).IsVisible);
        Assert.False(Moves(Core(box)));

        player.Resume();
        Jobs();

        Assert.True(Moves(Core(box)));

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetsBoxShowsTheClipsFirstFrameAndStartsNoTimer()
    {
        var clip = Place("kex");
        var (window, _, box) = Open(stillClips: true, Line(1, "kex"));

        Assert.True(CoreBox(box).IsVisible);
        Assert.Equal(clip, Core(box).ShowingClip);
        Assert.False(Moves(Core(box)));

        window.Close();
    }

    [AvaloniaFact]
    public void ACoreWithNoClipOnDiskShowsItsMark()
    {
        var (window, _, box) = Open(stillClips: false, Line(1, "kex"));

        Assert.True(CoreBox(box).IsVisible);
        Assert.Null(Core(box).ShowingClip);
        Assert.False(Core(box).Picture.IsVisible);

        window.Close();
    }
}
