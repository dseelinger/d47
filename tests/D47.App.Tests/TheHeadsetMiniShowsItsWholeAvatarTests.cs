using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Storage;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The headset's mini draws its avatar as a square the panel's height in the rail, whatever came before (#862).</summary>
[Trait("Category", "Integration")]
public class TheHeadsetMiniShowsItsWholeAvatarTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-headset-avatar-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public TheHeadsetMiniShowsItsWholeAvatarTests()
    {
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
        Directory.CreateDirectory(_paths.AvatarClips);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "tests", "fixtures", "avatars", "clip.mp4");
    }

    /// <summary>A headset with the maintainer's sizes: the full panel 1250×690 at zoom 80, the mini 792×280 at 100.</summary>
    private (VrPanelSurface Panel, PanelView View, SettingsService Settings) Headset(string mode, string? dumpTo = null)
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
            avatars: AvatarLibrary.Load(new DiskFileSystem(), _paths),
            dumpTo: dumpTo);

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        Serve(panel);

        return (panel, view, settings);
    }

    /// <summary>One tick as <c>VrHost</c> runs it: configure, apply the mode, and draw one frame.</summary>
    private static void Serve(VrPanelSurface panel)
    {
        panel.Configure();
        panel.ApplyMode();

        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }
    }

    private static void Switch(SettingsService settings, string mode) =>
        settings.Replace("test", current => current with { Vr = current.Vr with { Mode = mode } });

    private static void AssertInTheRail(PanelView view, double side)
    {
        var avatar = view.GetControl<AvatarView>("Avatar");

        Assert.Same(view.GetControl<Border>("MiniRail"), avatar.Parent);
        Assert.Equal(new Rect(0, 0, side, side), avatar.Bounds);
        Assert.Equal(side, view.GetControl<Border>("MiniRail").Bounds.Width);
    }

    [AvaloniaFact]
    public void SwitchingFromAZoomedFullPanelToMiniFitsTheAvatarToTheRail()
    {
        var (panel, view, settings) = Headset("full");

        Switch(settings, "mini");
        Serve(panel);

        AssertInTheRail(view, 280);
    }

    [AvaloniaFact]
    public void StartingInMiniFitsTheAvatarToTheRail()
    {
        var (_, view, _) = Headset("mini", dumpTo: _root);

        AssertInTheRail(view, 280);

        if (TestSurface.CapturesWanted)
        {
            File.Copy(
                Path.Combine(_root, "vr-PanelMini.png"),
                Path.Combine(TestSurface.CaptureDirectory, "headset-mini-avatar.png"),
                overwrite: true);
        }
    }

    [AvaloniaFact]
    public void AReshapedMiniKeepsItsAvatarSquareAtTheNewHeight()
    {
        var (panel, view, _) = Headset("mini");

        panel.Reshape(0.6f, (900, 320));
        Serve(panel);

        AssertInTheRail(view, 320);
    }

    [AvaloniaFact]
    public void SwitchingBackToFullPutsTheAvatarInTheHeader()
    {
        var (panel, view, settings) = Headset("full");

        Switch(settings, "mini");
        Serve(panel);
        Switch(settings, "full");
        Serve(panel);

        var avatar = view.GetControl<AvatarView>("Avatar");

        Assert.Same(view.GetControl<Grid>("PageHeader"), avatar.Parent);
        Assert.Equal(PanelView.HeaderAvatarExtent, avatar.Bounds.Width);
        Assert.Equal(PanelView.HeaderAvatarExtent, avatar.Bounds.Height);
    }
}
