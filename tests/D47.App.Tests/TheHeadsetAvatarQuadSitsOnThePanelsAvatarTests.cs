using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Vr;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The avatar quad covers the panel's avatar as drawn, zoom included, plays the panel's clip over the panel's
/// still frame of it, and steps aside when something covers it (#1130).
/// </summary>
[Trait("Category", "Integration")]
public class TheHeadsetAvatarQuadSitsOnThePanelsAvatarTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-headset-avatar-quad-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public TheHeadsetAvatarQuadSitsOnThePanelsAvatarTests()
    {
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
        Directory.CreateDirectory(_paths.AvatarClips);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Fixture() =>
        Path.Combine(AppSource.RepositoryRoot(), "tests", "fixtures", "avatars", "clip.mp4");

    /// <summary>The full panel 1250×690 at zoom 80 and the mini 792×280 at 100, with every state's clip.</summary>
    private (VrPanelSurface Panel, VrAvatarSurface Avatar) Headset(string mode)
    {
        foreach (var state in Enum.GetValues<LoopState>())
        {
            File.Copy(Fixture(), Path.Combine(_paths.AvatarClips, CoreClips.FileName("covas", state)), overwrite: true);
        }

        var (settings, _, _) = TestSurface.Create();

        settings.Replace(
            "test",
            current => current with
            {
                Vr = current.Vr with
                {
                    Mode = mode,
                    Panel = current.Vr.Panel with { Pixels = "1250x690", Zoom = 80 },
                    Mini = current.Vr.Mini with { Pixels = "792x280", Zoom = 100 },
                },
            });

        var panel = new VrPanelSurface(
            new PanelViewModel { CoreId = "covas" },
            settings,
            _ => null,
            avatars: AvatarLibrary.Load(new DiskFileSystem(), _paths))
        {
            Enabled = true,
        };

        Serve(panel);

        return (panel, new VrAvatarSurface(panel));
    }

    private static void Serve(VrPanelSurface panel)
    {
        panel.Configure();
        panel.ApplyMode();

        Dispatcher.UIThread.RunJobs();

        Draw(panel);
    }

    private static void Draw(IVrSurfaceSource source)
    {
        var (width, height) = source.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                source.Draw((IntPtr)pixels, width * 4);
            }
        }
    }

    [AvaloniaFact]
    public void OnAZoomedFullPanelTheAreaIsTheZoomedHeaderAvatar()
    {
        var (panel, _) = Headset("full");

        var area = Assert.IsType<VrPixelRect>(panel.AvatarArea);
        var side = PanelView.HeaderAvatarExtent * 0.8;

        Assert.Equal((1250, 690), panel.DrawnSize);
        Assert.Equal(side, area.Width, 1);
        Assert.Equal(side, area.Height, 1);
        Assert.InRange(area.X + area.Width, 1250 - (2 * side), 1250);
        Assert.InRange(area.Y, 0, side);
    }

    [AvaloniaFact]
    public void OnTheMiniTheAreaIsTheRail()
    {
        var (panel, _) = Headset("mini");

        Assert.Equal(new VrPixelRect(0, 0, 280, 280), panel.AvatarArea);
    }

    [AvaloniaFact]
    public void TheQuadPlaysThePanelsClipOverItsStill()
    {
        var (panel, avatar) = Headset("full");

        avatar.Advance(Start);

        Assert.True(avatar.Visible);
        Assert.True(avatar.IsDirty);
        Assert.False(panel.IsDirty, "the panel keeps its still under the quad");

        Draw(avatar);
        avatar.Advance(Start.AddMilliseconds(10));

        Assert.False(avatar.IsDirty, "the same frame inside one clip frame");

        var placement = avatar.Placement;
        var area = panel.AvatarArea!.Value;

        Assert.Equal(panel.Placement.Sane().WidthMetres * area.Width / 1250f, placement.WidthMetres, 5);
        Assert.NotNull(placement.Offset);
    }

    [AvaloniaFact]
    public void TheResizeHandlesTakeTheQuadDown()
    {
        var (panel, avatar) = Headset("full");

        avatar.Advance(Start);
        Draw(panel);

        panel.ShowHandles(true, VrHandle.None);
        avatar.Advance(Start.AddMilliseconds(100));

        Assert.True(panel.AvatarCovered);
        Assert.False(avatar.Visible);
    }
}

